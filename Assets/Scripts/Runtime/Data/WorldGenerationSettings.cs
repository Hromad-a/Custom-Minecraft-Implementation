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
        [Tooltip("Vertical size of the world; also the build ceiling (exclusive). Horizontally the world is infinite.")]
        [SerializeField, Min(2)] private int worldHeight = 64;
        [Tooltip("Horizontal size of one chunk, the unit of generation and rendering.")]
        [SerializeField, Min(1)] private int chunkSize = 16;

        [Header("Streaming")]
        [Tooltip("How many chunks around the viewer get meshes.")]
        [SerializeField, Range(2, 32)] private int viewDistance = 8;
        [Tooltip("Milliseconds per frame the streamer may spend generating and meshing chunks. Lower = smoother but slower terrain fill-in.")]
        [SerializeField, Range(0.5f, 10f)] private float streamingBudgetMs = 2f;
        [Tooltip("Blocks above the origin column's surface where the player spawns.")]
        [SerializeField, Min(1f)] private float spawnHeightOffset = 2f;

        [Header("Terrain")]
        [Tooltip("0 = random seed on every regeneration; any other value reproduces the exact same world.")]
        [SerializeField] private int seed;
        [Tooltip("Column height where the accumulated relief of all layers is zero.")]
        [SerializeField, Min(0)] private int baseHeight = 24;
        [Tooltip("Evaluated in order; Add layers stack relief, Multiply layers modulate what came before them.")]
        [SerializeField] private List<NoiseLayerDefinition> noiseLayers = new();

        [Header("Block types")]
        [SerializeField] private List<BlockDefinition> blocks = new();

        public int WorldHeight => worldHeight;
        public int ChunkSize => chunkSize;
        public int ViewDistance => viewDistance;
        public float StreamingBudgetMs => streamingBudgetMs;
        public float SpawnHeightOffset => spawnHeightOffset;
        public int Seed => seed;
        public int BaseHeight => baseHeight;
        public IReadOnlyList<NoiseLayerDefinition> NoiseLayers => noiseLayers;
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
        /// Returns true when the settings are valid. Height ranges may overlap or
        /// leave gaps; uncovered heights use the nearest block's type.
        /// </summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;

            if (blocks.Count == 0)
                errors.Add("At least one block definition is required.");

            if (noiseLayers.Count == 0)
                errors.Add("At least one noise layer is required.");
            var seenSalts = new HashSet<int>();
            foreach (NoiseLayerDefinition layer in noiseLayers)
            {
                if (layer == null)
                {
                    errors.Add("Noise layer list contains an empty entry.");
                    continue;
                }
                if (!seenSalts.Add(layer.Salt))
                    errors.Add($"Duplicate noise layer salt {layer.Salt} ('{layer.name}').");
                if (layer.Operation == NoiseLayerOperation.Multiply && layer.RemapMin > layer.RemapMax)
                    errors.Add($"Noise layer '{layer.name}' has remap min above remap max.");
            }

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

            return errors.Count == before;
        }
    }
}
