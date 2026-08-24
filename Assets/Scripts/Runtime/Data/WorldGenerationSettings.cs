using System.Collections.Generic;
using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// All tunables for world generation in one asset. The regenerate workflow
    /// edits this asset and rebuilds the world; nothing generation-related is
    /// hardcoded elsewhere.
    /// </summary>
    [CreateAssetMenu(menuName = "Custom Minecraft/World Generation Settings", fileName = "WorldGenerationSettings")]
    public sealed class WorldGenerationSettings : ScriptableObject
    {
        [Header("World dimensions")]
        [SerializeField, Min(1)] private int worldSizeX = 128;
        [SerializeField, Min(1)] private int worldSizeZ = 128;
        [Tooltip("Vertical size of the world; also the build ceiling (exclusive).")]
        [SerializeField, Min(2)] private int worldHeight = 64;
        [Tooltip("Horizontal size of one render chunk, used from Step 2 on.")]
        [SerializeField, Min(1)] private int chunkSize = 16;

        [Header("Terrain noise")]
        [Tooltip("0 = random seed on every regeneration; any other value reproduces the exact same world.")]
        [SerializeField] private int seed;
        [Tooltip("Horizontal zoom of the noise. Larger = wider, smoother hills.")]
        [SerializeField, Min(0.01f)] private float noiseScale = 45f;
        [SerializeField, Range(1, 8)] private int octaves = 3;
        [Tooltip("How strongly finer octaves show through.")]
        [SerializeField, Range(0.05f, 1f)] private float persistence = 0.5f;
        [Tooltip("Vertical swing of the terrain in blocks, applied on top of the base height.")]
        [SerializeField, Min(0f)] private float amplitude = 20f;
        [Tooltip("Column height where the noise value is at its minimum.")]
        [SerializeField, Min(0)] private int baseHeight = 14;

        [Header("Block types")]
        [SerializeField] private List<BlockDefinition> blocks = new();

        public int WorldSizeX => worldSizeX;
        public int WorldSizeZ => worldSizeZ;
        public int WorldHeight => worldHeight;
        public int ChunkSize => chunkSize;
        public int Seed => seed;
        public float NoiseScale => noiseScale;
        public int Octaves => octaves;
        public float Persistence => persistence;
        public float Amplitude => amplitude;
        public int BaseHeight => baseHeight;
        public IReadOnlyList<BlockDefinition> Blocks => blocks;

        public BlockDefinition BlockForId(int id)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] != null && blocks[i].Id == id)
                    return blocks[i];
            }
            return null;
        }

        /// <summary>
        /// Appends every configuration problem to <paramref name="errors"/>.
        /// Returns true when the settings are valid. Overlapping height ranges are
        /// allowed by design; gaps in vertical coverage are not.
        /// </summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;

            if (blocks.Count == 0)
                errors.Add("At least one block definition is required.");

            var seenIds = new HashSet<int>();
            foreach (BlockDefinition block in blocks)
            {
                if (block == null)
                {
                    errors.Add("Block list contains an empty entry.");
                    continue;
                }
                if (!seenIds.Add(block.Id))
                    errors.Add($"Duplicate block id {block.Id} ('{block.DisplayName}').");
                if (block.MinHeight > block.MaxHeight)
                    errors.Add($"'{block.DisplayName}' has min height {block.MinHeight} above max height {block.MaxHeight}.");
            }

            for (int y = 0; y < worldHeight; y++)
            {
                if (!AnyBlockCovers(y))
                {
                    errors.Add($"No block definition covers height {y}; every Y in [0, {worldHeight - 1}] needs at least one.");
                    break;
                }
            }

            return errors.Count == before;
        }

        private bool AnyBlockCovers(int y)
        {
            foreach (BlockDefinition block in blocks)
            {
                if (block != null && block.ContainsHeight(y))
                    return true;
            }
            return false;
        }
    }
}
