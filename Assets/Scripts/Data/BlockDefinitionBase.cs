using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// Base of all block types: identity, rendering, and world generation data.
    /// Behavior differences live in subclasses — see <see cref="BlockDefinitionOrdinary"/>
    /// for the standard hold-to-mine block.
    /// </summary>
    public abstract class BlockDefinitionBase : ScriptableObject
    {
        [Tooltip("Stable identifier stored in world data. Never reuse or renumber a shipped id.")]
        [SerializeField, Min(0)] private int id;
        [SerializeField] private string displayName;
        [Tooltip("Rendering material for this block type; shared by every chunk.")]
        [SerializeField] private Material material;
        [Tooltip("Seconds the mine button must be held to destroy this block. -1 = unbreakable.")]
        [SerializeField, Min(-1f)] private float mineDuration = 1f;

        [Header("Generation")]
        [Tooltip("Lowest world Y (inclusive) where this block can generate.")]
        [SerializeField, Min(0)] private int minHeight;
        [Tooltip("Highest world Y (inclusive) where this block can generate.")]
        [SerializeField, Min(0)] private int maxHeight;
        [Tooltip("Relative vote strength where height ranges overlap. 1 = normal.")]
        [SerializeField, Min(0.01f)] private float generationWeight = 1f;

        public int Id => id;
        public string DisplayName => displayName;
        public Material Material => material;
        public int MinHeight => minHeight;
        public int MaxHeight => maxHeight;
        public float GenerationWeight => generationWeight;

        public float MineDuration => mineDuration;
        public bool IsUnbreakable => mineDuration < 0f;

        public bool ContainsHeight(int y) => y >= minHeight && y <= maxHeight;

        /// <summary>
        /// Whether this block may generate at this cell, on top of the height
        /// range. Must be deterministic: a pure function of position and seed.
        /// </summary>
        public virtual bool CanGenerateAt(int x, int y, int z, int seed) => true;

        /// <summary>Called after a block of this type has been mined.</summary>
        public virtual void OnMined(World world, Vector3Int cell)
        {
        }
    }
}
