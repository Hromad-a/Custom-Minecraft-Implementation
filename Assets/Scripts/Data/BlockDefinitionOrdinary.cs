using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// The standard block: mined by holding the mine button for a fixed duration.
    /// </summary>
    [CreateAssetMenu(menuName = "Custom Minecraft/Basic Block", fileName = "NewBlock")]
    public sealed class BlockDefinitionOrdinary : BlockDefinitionBase
    {
        [Tooltip("Seconds the mine button must be held to destroy this block.")]
        [SerializeField, Min(0.05f)] private float mineDuration = 1f;

        public override float MineDuration => mineDuration;
    }
}
