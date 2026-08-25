using UnityEngine;

namespace CustomMinecraft
{
    public enum NoiseLayerOperation
    {
        /// <summary>Adds noise (-1..1) times amplitude to the accumulated relief.</summary>
        Add,
        /// <summary>Multiplies the accumulated relief by the remapped 0..1 noise value.</summary>
        Multiply,
    }

    /// <summary>
    /// One layer of the terrain height calculation. Layers are evaluated in the
    /// order they appear in the settings' list: Add layers stack relief on top of
    /// each other, Multiply layers modulate everything accumulated before them
    /// (e.g. flattening plains and exaggerating mountain regions).
    /// </summary>
    [CreateAssetMenu(menuName = "Custom Minecraft/Noise Layer", fileName = "NewNoiseLayer")]
    public sealed class NoiseLayerDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier deriving this layer's noise seed. Never reuse across layers; changing it re-rolls this layer's terrain.")]
        [SerializeField] private int salt;
        [SerializeField] private NoiseLayerOperation operation = NoiseLayerOperation.Add;
        [Tooltip("Constant height in blocks added wherever this layer applies, scaled by the region mask.")]
        [SerializeField] private float heightOffset;

        [Header("Noise")]
        [Tooltip("Horizontal zoom. Larger = wider, smoother features.")]
        [SerializeField, Min(0.01f)] private float noiseScale = 45f;
        [SerializeField, Range(1, 8)] private int octaves = 3;
        [Tooltip("How strongly finer octaves show through.")]
        [SerializeField, Range(0.05f, 1f)] private float persistence = 0.5f;

        [Header("Add layers")]
        [Tooltip("Vertical swing in blocks.")]
        [SerializeField, Min(0f)] private float amplitude = 10f;

        [Header("Multiply layers")]
        [Tooltip("The 0..1 noise value is remapped into [min, max] before multiplying.")]
        [SerializeField, Range(0f, 2f)] private float remapMin = 0.25f;
        [SerializeField, Range(0f, 2f)] private float remapMax = 1f;

        [Header("Region mask")]
        [Tooltip("Approximate fraction of the world this layer affects. 1 = everywhere (no mask).")]
        [SerializeField, Range(0f, 1f)] private float coverage = 1f;
        [Tooltip("Horizontal size of the affected regions, in blocks.")]
        [SerializeField, Min(1f)] private float regionSize = 150f;
        [Tooltip("Width of the smooth border between affected and unaffected areas, as a fraction of the mask's value range.")]
        [SerializeField, Range(0.01f, 0.5f)] private float regionFalloff = 0.1f;

        public int Salt => salt;
        public NoiseLayerOperation Operation => operation;
        public float HeightOffset => heightOffset;
        public float NoiseScale => noiseScale;
        public int Octaves => octaves;
        public float Persistence => persistence;
        public float Amplitude => amplitude;
        public float RemapMin => remapMin;
        public float RemapMax => remapMax;
        public float Coverage => coverage;
        public float RegionSize => regionSize;
        public float RegionFalloff => regionFalloff;
    }
}
