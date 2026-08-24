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

        // A block never fully loses its vote inside its own range; keeps the
        // dithered transition from developing hard cutoff lines.
        private const float MinEdgeWeight = 0.05f;

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
            int heightmapSeed = DeterministicHash.DeriveSeed(seed, HeightmapSalt);
            int typeSeed = DeterministicHash.DeriveSeed(seed, TypeVariationSalt);
            TypeCandidates[] typeTable = BuildTypeTable(settings);

            for (int z = 0; z < settings.WorldSizeZ; z++)
            {
                for (int x = 0; x < settings.WorldSizeX; x++)
                {
                    int columnHeight = ColumnHeight(settings, heightmapSeed, x, z);
                    for (int y = 0; y < settings.WorldHeight; y++)
                    {
                        int typeId = PickTypeId(typeTable[y], x, y, z, typeSeed);
                        data[x, y, z] = new BlockData(y <= columnHeight, typeId);
                    }
                }
            }

            return data;
        }

        /// <summary>Terrain surface height for one column, in [1, worldHeight - 1].</summary>
        public static int ColumnHeight(WorldGenerationSettings settings, int heightmapSeed, int x, int z)
        {
            float noise = PerlinNoise2D.Fbm(
                x / settings.NoiseScale,
                z / settings.NoiseScale,
                heightmapSeed,
                settings.Octaves,
                settings.Persistence);

            float normalized = noise * 0.5f + 0.5f;
            int height = (int)MathF.Round(settings.BaseHeight + normalized * settings.Amplitude);
            return Math.Clamp(height, 1, settings.WorldHeight - 1);
        }

        // The deterministic type of a cell: sole candidate wins outright, overlaps
        // are resolved by a position-hashed weighted pick.
        private static int PickTypeId(in TypeCandidates candidates, int x, int y, int z, int typeSeed)
        {
            if (candidates.Ids.Length == 1)
                return candidates.Ids[0];

            float roll = DeterministicHash.Value01(x, y, z, typeSeed) * candidates.TotalWeight;
            for (int i = 0; i < candidates.Ids.Length; i++)
            {
                roll -= candidates.Weights[i];
                if (roll < 0f)
                    return candidates.Ids[i];
            }
            return candidates.Ids[^1];
        }

        // Precomputes, per world Y, which block types are eligible and their weights.
        // Weight = tent curve over the block's own range (fades toward its edges)
        // times the per-definition generation weight.
        private static TypeCandidates[] BuildTypeTable(WorldGenerationSettings settings)
        {
            var table = new TypeCandidates[settings.WorldHeight];
            var ids = new List<int>();
            var weights = new List<float>();

            for (int y = 0; y < settings.WorldHeight; y++)
            {
                ids.Clear();
                weights.Clear();

                foreach (BlockDefinition block in settings.Blocks)
                {
                    if (block == null || !block.ContainsHeight(y))
                        continue;
                    ids.Add(block.Id);
                    weights.Add(EdgeFade(y, block.MinHeight, block.MaxHeight) * block.GenerationWeight);
                }

                table[y] = new TypeCandidates(ids.ToArray(), weights.ToArray());
            }

            return table;
        }

        private static float EdgeFade(int y, int minHeight, int maxHeight)
        {
            if (minHeight == maxHeight)
                return 1f;

            float t = (y - minHeight) / (float)(maxHeight - minHeight);
            float tent = 1f - MathF.Abs(2f * t - 1f);
            return MathF.Max(tent, MinEdgeWeight);
        }

        private readonly struct TypeCandidates
        {
            public readonly int[] Ids;
            public readonly float[] Weights;
            public readonly float TotalWeight;

            public TypeCandidates(int[] ids, float[] weights)
            {
                Ids = ids;
                Weights = weights;
                TotalWeight = 0f;
                for (int i = 0; i < weights.Length; i++)
                    TotalWeight += weights[i];
            }
        }
    }
}
