using System;
using System.Collections.Generic;
using CustomMinecraft.Generation;
using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// World state as a grid of lazily generated chunks, unbounded horizontally.
    /// Reading any cell generates its chunk on demand, so callers can query
    /// freely — generation is deterministic per position, so a chunk's content
    /// never depends on when it was first touched. The vertical range is fixed:
    /// y in [0, sizeY).
    /// </summary>
    public sealed class WorldData
    {
        public readonly int chunkSize;
        public readonly int sizeY;
        public readonly int seed;

        private readonly WorldGenerationSettings settings;
        private readonly Dictionary<Vector2Int, BlockData[]> chunks = new();

        public WorldData(WorldGenerationSettings settings, int seed)
        {
            this.settings = settings;
            this.seed = seed;
            chunkSize = settings.ChunkSize;
            sizeY = settings.WorldHeight;
        }

        /// <summary>Every chunk generated so far, keyed by chunk coordinate.</summary>
        public IReadOnlyDictionary<Vector2Int, BlockData[]> Chunks => chunks;

        public BlockData this[int x, int y, int z]
        {
            get
            {
                RequireVerticalBounds(y);
                return Chunk(x, z)[CellIndex(x, y, z)];
            }
            set
            {
                RequireVerticalBounds(y);
                Chunk(x, z)[CellIndex(x, y, z)] = value;
            }
        }

        /// <summary>Horizontally the world is infinite; only Y is bounded.</summary>
        public bool InBounds(int x, int y, int z) => y >= 0 && y < sizeY;

        /// <summary>True when the cell exists and currently holds a block.</summary>
        public bool IsSolid(int x, int y, int z) =>
            y >= 0 && y < sizeY && this[x, y, z].IsPresent;

        /// <summary>
        /// Flips presence of a block without touching its generation-assigned type.
        /// Mining passes false, placing passes true.
        /// </summary>
        public void SetPresence(int x, int y, int z, bool present)
        {
            RequireVerticalBounds(y);
            Chunk(x, z)[CellIndex(x, y, z)].IsPresent = present;
        }

        public bool HasChunk(int chunkX, int chunkZ) =>
            chunks.ContainsKey(new Vector2Int(chunkX, chunkZ));

        /// <summary>
        /// The chunk's raw cell array (generated on demand). Lets hot loops like
        /// mesh building index cells directly instead of paying a dictionary
        /// lookup per cell access.
        /// </summary>
        public BlockData[] GetChunkCells(int chunkX, int chunkZ)
        {
            EnsureChunk(chunkX, chunkZ);
            return chunks[new Vector2Int(chunkX, chunkZ)];
        }

        /// <summary>Generates the chunk's data now if it does not exist yet.</summary>
        public void EnsureChunk(int chunkX, int chunkZ)
        {
            var coord = new Vector2Int(chunkX, chunkZ);
            if (!chunks.ContainsKey(coord))
                chunks.Add(coord, WorldGenerator.GenerateChunk(settings, seed, chunkX, chunkZ));
        }

        /// <summary>Floor division, so negative coordinates map to chunks correctly.</summary>
        public static int FloorDiv(int value, int size) =>
            value >= 0 ? value / size : (value + 1) / size - 1;

        private BlockData[] Chunk(int x, int z)
        {
            int chunkX = FloorDiv(x, chunkSize);
            int chunkZ = FloorDiv(z, chunkSize);
            EnsureChunk(chunkX, chunkZ);
            return chunks[new Vector2Int(chunkX, chunkZ)];
        }

        private int CellIndex(int x, int y, int z)
        {
            int localX = x - FloorDiv(x, chunkSize) * chunkSize;
            int localZ = z - FloorDiv(z, chunkSize) * chunkSize;
            return localX + localZ * chunkSize + y * chunkSize * chunkSize;
        }

        private void RequireVerticalBounds(int y)
        {
            if (y < 0 || y >= sizeY)
                throw new ArgumentOutOfRangeException(nameof(y), $"{y} is outside the world height [0, {sizeY}).");
        }
    }
}
