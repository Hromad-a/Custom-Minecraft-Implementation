using System;

namespace CustomMinecraft
{
    /// <summary>
    /// The single source of truth for world state: a dense 3D grid of
    /// <see cref="BlockData"/> stored as a flat array (x + z * sizeX + y * sizeX * sizeZ).
    /// Directly JSON-serializable via JsonUtility; two worlds generated from the
    /// same seed and settings produce byte-identical JSON.
    /// </summary>
    [Serializable]
    public sealed class WorldData
    {
        public int sizeX;
        public int sizeY;
        public int sizeZ;
        public int seed;
        public BlockData[] cells;

        public WorldData(int sizeX, int sizeY, int sizeZ, int seed)
        {
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
                throw new ArgumentOutOfRangeException(
                    $"World dimensions must be positive, got ({sizeX}, {sizeY}, {sizeZ}).");

            this.sizeX = sizeX;
            this.sizeY = sizeY;
            this.sizeZ = sizeZ;
            this.seed = seed;
            cells = new BlockData[sizeX * sizeY * sizeZ];
        }

        public BlockData this[int x, int y, int z]
        {
            get => cells[IndexOf(x, y, z)];
            set => cells[IndexOf(x, y, z)] = value;
        }

        public bool InBounds(int x, int y, int z) =>
            x >= 0 && x < sizeX &&
            y >= 0 && y < sizeY &&
            z >= 0 && z < sizeZ;

        /// <summary>True when the cell exists and currently holds a block.</summary>
        public bool IsSolid(int x, int y, int z) =>
            InBounds(x, y, z) && cells[IndexUnchecked(x, y, z)].IsPresent;

        /// <summary>
        /// Flips presence of a block without touching its generation-assigned type.
        /// Mining passes false, placing passes true.
        /// </summary>
        public void SetPresence(int x, int y, int z, bool present)
        {
            cells[IndexOf(x, y, z)].IsPresent = present;
        }

        private int IndexOf(int x, int y, int z)
        {
            if (!InBounds(x, y, z))
                throw new ArgumentOutOfRangeException(
                    $"({x}, {y}, {z}) is outside the world ({sizeX}x{sizeY}x{sizeZ}).");
            return IndexUnchecked(x, y, z);
        }

        private int IndexUnchecked(int x, int y, int z) => x + z * sizeX + y * sizeX * sizeZ;
    }
}
