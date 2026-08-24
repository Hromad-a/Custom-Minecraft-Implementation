using System;
using System.Collections.Generic;

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

        public static WorldData Generate(WorldGenerationSettings settings, int seed)
        {
            var errors = new List<string>();
            if (!settings.Validate(errors))
                throw new InvalidOperationException(
                    "Cannot generate world, settings are invalid:\n - " + string.Join("\n - ", errors));

            var data = new WorldData(settings.WorldSizeX, settings.WorldHeight, settings.WorldSizeZ, seed);
            int heightmapSeed = HeightmapSeed(seed);
            int typeSeed = DeterministicHash.DeriveSeed(seed, TypeVariationSalt);

            for (int z = 0; z < settings.WorldSizeZ; z++)
            {
                for (int x = 0; x < settings.WorldSizeX; x++)
                {
                    int columnHeight = ColumnHeight(settings, heightmapSeed, x, z);
                    for (int y = 0; y < settings.WorldHeight; y++)
                    {
                        int typeId = PickTypeId(settings, x, y, z, typeSeed);
                        data[x, y, z] = new BlockData(y <= columnHeight, typeId);
                    }
                }
            }

            return data;
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
            foreach (BlockDefinition block in settings.Blocks)
            {
                if (block.ContainsHeight(y))
                    totalWeight += VoteWeight(block, y);
            }

            // No range covers this height: fall back to the nearest one, so the
            // deepest block extends downward and the highest extends upward.
            if (totalWeight == 0f)
                return NearestBlock(settings, y).Id;

            float roll = DeterministicHash.Value01(x, y, z, typeSeed) * totalWeight;
            int typeId = 0;
            foreach (BlockDefinition block in settings.Blocks)
            {
                if (!block.ContainsHeight(y))
                    continue;
                typeId = block.Id;
                roll -= VoteWeight(block, y);
                if (roll < 0f)
                    break;
            }
            return typeId;
        }

        private static BlockDefinition NearestBlock(WorldGenerationSettings settings, int y)
        {
            BlockDefinition nearest = null;
            int bestDistance = int.MaxValue;
            foreach (BlockDefinition block in settings.Blocks)
            {
                int distance = y < block.MinHeight ? block.MinHeight - y
                    : y > block.MaxHeight ? y - block.MaxHeight
                    : 0;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = block;
                }
            }
            return nearest;
        }

        private static float VoteWeight(BlockDefinition block, int y) =>
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
