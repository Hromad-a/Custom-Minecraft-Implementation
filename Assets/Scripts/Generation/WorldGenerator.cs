using System;

namespace CustomMinecraft.Generation
{
    /// <summary>
    /// Builds a <see cref="WorldData"/> from settings and a seed. Pure function of
    /// its inputs: the same settings and seed always produce a bit-identical world,
    /// regardless of call order or platform.
    /// </summary>
    public static class WorldGenerator
    {
        // Salts for deriving independent randomness streams from the master seed.
        private const int HeightmapSalt = 1;
        private const int TypeVariationSalt = 2;
        private const int MaskSalt = 3;

        // A block never fully loses its vote inside its own range; keeps the
        // dithered transition from developing hard cutoff lines.
        private const float MinEdgeWeight = 0.05f;

        /// <summary>The seed feeding the height layers, derived from the world seed.</summary>
        public static int HeightmapSeed(int seed) => DeterministicHash.DeriveSeed(seed, HeightmapSalt);

        /// <summary>Turns the configured seed into the seed actually used (0 = randomize).</summary>
        public static int ResolveSeed(int configuredSeed)
        {
            if (configuredSeed != 0)
                return configuredSeed;

            int random = unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode();
            return random == 0 ? 1 : random;
        }

        /// <summary>
        /// Generates the cells of one chunk. Every value is a pure function of the
        /// absolute world position and the seed, so chunks can be generated in any
        /// order and always come out identical. Settings are assumed valid — the
        /// World validates before any generation happens.
        /// </summary>
        public static BlockData[] GenerateChunk(WorldGenerationSettings settings, int seed, int chunkX, int chunkZ)
        {
            int size = settings.ChunkSize;
            int height = settings.WorldHeight;
            var cells = new BlockData[size * size * height];
            int heightmapSeed = HeightmapSeed(seed);
            int typeSeed = DeterministicHash.DeriveSeed(seed, TypeVariationSalt);

            for (int localZ = 0; localZ < size; localZ++)
            {
                for (int localX = 0; localX < size; localX++)
                {
                    int worldX = chunkX * size + localX;
                    int worldZ = chunkZ * size + localZ;
                    int columnHeight = ColumnHeight(settings, heightmapSeed, worldX, worldZ);
                    for (int y = 0; y < height; y++)
                    {
                        int typeId = PickTypeId(settings, worldX, y, worldZ, typeSeed);
                        cells[localX + localZ * size + y * size * size] =
                            new BlockData(y <= columnHeight, typeId);
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// Terrain surface height for one column, in [1, worldHeight - 1]:
        /// the noise layers evaluated in order on top of the base height.
        /// </summary>
        public static int ColumnHeight(WorldGenerationSettings settings, int heightmapSeed, int x, int z)
        {
            float relief = 0f;
            foreach (NoiseLayerDefinition layer in settings.NoiseLayers)
            {
                int layerSeed = DeterministicHash.DeriveSeed(heightmapSeed, layer.Salt);
                float weight = RegionWeight(layer, layerSeed, x, z);
                if (weight <= 0f)
                    continue;

                float noise = PerlinNoise2D.Fbm(
                    x / layer.NoiseScale, z / layer.NoiseScale, layerSeed, layer.Octaves, layer.Persistence);

                if (layer.Operation == NoiseLayerOperation.Add)
                {
                    relief += noise * layer.Amplitude * weight;
                }
                else
                {
                    float factor = layer.RemapMin + (layer.RemapMax - layer.RemapMin) * (noise * 0.5f + 0.5f);
                    // Fades toward a neutral x1 outside the layer's regions.
                    relief *= 1f + (factor - 1f) * weight;
                }
                relief += layer.HeightOffset * weight;
            }

            int height = (int)MathF.Round(settings.BaseHeight + relief);
            return Math.Clamp(height, 1, settings.WorldHeight - 1);
        }

        // 1 deep inside the layer's regions, 0 outside, smooth across the border.
        // A slow mask noise decides where regions are; coverage sets how much of
        // the mask's value range counts as inside.
        private static float RegionWeight(NoiseLayerDefinition layer, int layerSeed, int x, int z)
        {
            if (layer.Coverage >= 1f)
                return 1f;
            if (layer.Coverage <= 0f)
                return 0f;

            int maskSeed = DeterministicHash.DeriveSeed(layerSeed, MaskSalt);
            float mask = PerlinNoise2D.Fbm(
                x / layer.RegionSize, z / layer.RegionSize, maskSeed, octaves: 2, persistence: 0.5f) * 0.5f + 0.5f;

            float t = Math.Clamp((layer.Coverage - mask) / layer.RegionFalloff, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // The deterministic type of a cell: every block whose height range contains
        // y votes with a weight (fading toward its range edges, scaled by its
        // generation weight), and a position-hashed roll picks the winner.
        private static int PickTypeId(WorldGenerationSettings settings, int x, int y, int z, int typeSeed)
        {
            float totalWeight = 0f;
            foreach (BlockDefinitionBase block in settings.Blocks)
            {
                if (block.ContainsHeight(y) && block.CanGenerateAt(x, y, z, typeSeed))
                    totalWeight += VoteWeight(block, y);
            }

            // No block is eligible here: fall back to the nearest height range,
            // so the deepest block extends downward and the highest upward.
            if (totalWeight == 0f)
                return NearestBlock(settings, x, y, z, typeSeed).Id;

            float roll = DeterministicHash.Value01(x, y, z, typeSeed) * totalWeight;
            int typeId = 0;
            foreach (BlockDefinitionBase block in settings.Blocks)
            {
                if (!block.ContainsHeight(y) || !block.CanGenerateAt(x, y, z, typeSeed))
                    continue;
                typeId = block.Id;
                roll -= VoteWeight(block, y);
                if (roll < 0f)
                    break;
            }
            return typeId;
        }

        private static BlockDefinitionBase NearestBlock(WorldGenerationSettings settings, int x, int y, int z, int typeSeed)
        {
            BlockDefinitionBase nearest = null;
            int bestDistance = int.MaxValue;
            foreach (BlockDefinitionBase block in settings.Blocks)
            {
                if (!block.CanGenerateAt(x, y, z, typeSeed))
                    continue;
                int distance = y < block.MinHeight ? block.MinHeight - y
                    : y > block.MaxHeight ? y - block.MaxHeight
                    : 0;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = block;
                }
            }
            // Every block restricted itself away from this cell: any block beats
            // an untyped cell, so take the first.
            return nearest != null ? nearest : settings.Blocks[0];
        }

        private static float VoteWeight(BlockDefinitionBase block, int y) =>
            EdgeFade(y, block.MinHeight, block.MaxHeight) * block.GenerationWeight;

        private static float EdgeFade(int y, int minHeight, int maxHeight)
        {
            if (minHeight == maxHeight)
                return 1f;

            float t = (y - minHeight) / (float)(maxHeight - minHeight);
            float tent = 1f - MathF.Abs(2f * t - 1f);
            return MathF.Max(tent, MinEdgeWeight);
        }
    }
}
