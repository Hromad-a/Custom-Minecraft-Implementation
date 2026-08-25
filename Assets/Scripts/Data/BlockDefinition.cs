using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// Data definition of one block type. All gameplay-relevant numbers live here
    /// so new block variants are pure data, no code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Custom Minecraft/Block Definition", fileName = "NewBlockDefinition")]
    public sealed class BlockDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier stored in world data. Never reuse or renumber a shipped id.")]
        [SerializeField, Min(0)] private int id;
        [SerializeField] private string displayName;
        [Tooltip("Rendering material for this block type; shared by every chunk.")]
        [SerializeField] private Material material;

        [Tooltip("Seconds the mine button must be held to destroy this block.")]
        [SerializeField, Min(0.05f)] private float mineDuration = 1f;

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
        public float MineDuration => mineDuration;
        public int MinHeight => minHeight;
        public int MaxHeight => maxHeight;
        public float GenerationWeight => generationWeight;

        public bool ContainsHeight(int y) => y >= minHeight && y <= maxHeight;
    }
}
