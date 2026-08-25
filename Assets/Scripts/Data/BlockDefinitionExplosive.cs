using CustomMinecraft.Generation;
using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// A block that explodes when mined, instantly mining everything in range
    /// (except the unbreakable world floor). Explosions chain: an explosive block
    /// caught in the blast explodes too. Spawning is rare and deterministic: each
    /// region of the world gets a fixed number of potential deposit spots, hashed
    /// from the seed.
    /// </summary>
    [CreateAssetMenu(menuName = "Custom Minecraft/Explosive Block", fileName = "NewExplosiveBlock")]
    public sealed class BlockDefinitionExplosive : BlockDefinitionBase
    {
        [Tooltip("Blocks within this radius are mined instantly when it explodes.")]
        [SerializeField, Min(1f)] private float explosionRadius = 3f;

        [Header("Spawning")]
        [Tooltip("Horizontal size in blocks of one spawn region.")]
        [SerializeField, Min(4)] private int spawnRegionSize = 32;
        [Tooltip("Deposit spots rolled per region. Spots above the terrain surface stay empty, so treat this as an upper bound.")]
        [SerializeField, Min(0)] private int spawnsPerRegion = 2;

        public override void OnMined(World world, Vector3Int cell)
        {
            int range = Mathf.CeilToInt(explosionRadius);
            float radiusSquared = explosionRadius * explosionRadius;
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    for (int dz = -range; dz <= range; dz++)
                    {
                        if (dx * dx + dy * dy + dz * dz > radiusSquared)
                            continue;
                        // TryMine enforces the unbreakable floor, and mining a
                        // caught explosive re-enters here — the chain reaction.
                        world.TryMine(cell + new Vector3Int(dx, dy, dz));
                    }
                }
            }
        }

        // The cell is a deposit spot if it matches one of the region's hashed
        // spawn positions. Pure position+seed math, so generation order and
        // chunk boundaries do not matter.
        public override bool CanGenerateAt(int x, int y, int z, int seed)
        {
            int regionX = WorldData.FloorDiv(x, spawnRegionSize);
            int regionZ = WorldData.FloorDiv(z, spawnRegionSize);
            int regionSeed = DeterministicHash.DeriveSeed(
                DeterministicHash.DeriveSeed(DeterministicHash.DeriveSeed(seed, Id), regionX), regionZ);

            int heightSpan = MaxHeight - MinHeight + 1;
            for (int spot = 0; spot < spawnsPerRegion; spot++)
            {
                int spotX = regionX * spawnRegionSize
                    + (int)(DeterministicHash.Value01(spot, 0, 0, regionSeed) * spawnRegionSize);
                int spotZ = regionZ * spawnRegionSize
                    + (int)(DeterministicHash.Value01(spot, 1, 0, regionSeed) * spawnRegionSize);
                int spotY = MinHeight
                    + (int)(DeterministicHash.Value01(spot, 2, 0, regionSeed) * heightSpan);
                if (x == spotX && y == spotY && z == spotZ)
                    return true;
            }
            return false;
        }
    }
}
