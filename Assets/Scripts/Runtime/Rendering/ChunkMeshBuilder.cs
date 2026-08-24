using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomMinecraft.Rendering
{
    /// <summary>
    /// Turns one chunk-sized region of <see cref="WorldData"/> into a mesh.
    /// Emits only faces adjacent to air (or the world boundary); triangles go into
    /// one submesh per block type, using the fixed order of the settings' block
    /// list, so every chunk shares the same material array. Face UVs map into a
    /// horizontal three-tile texture atlas: top | side | bottom.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        private static readonly Vector3Int[] FaceDirections =
        {
            new(0, 1, 0), new(0, -1, 0), new(1, 0, 0), new(-1, 0, 0), new(0, 0, 1), new(0, 0, -1),
        };

        // Quad corners per face, wound clockwise seen from outside the block.
        private static readonly Vector3[][] FaceCorners =
        {
            new Vector3[] { new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0) },
            new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1) },
            new Vector3[] { new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1) },
            new Vector3[] { new(0, 0, 1), new(0, 1, 1), new(0, 1, 0), new(0, 0, 0) },
            new Vector3[] { new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), new(0, 0, 1) },
            new Vector3[] { new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0) },
        };

        // Matches the corner order above; on side faces V follows world Y, so
        // vertical texture streaks stay vertical.
        private static readonly Vector2[] UvPattern = { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };

        // Keeps sampling away from tile borders so mipmaps do not bleed
        // neighboring tiles in.
        private const float TileInset = 0.002f;

        // Reused across builds so remeshing does not allocate.
        private static readonly List<Vector3> Vertices = new();
        private static readonly List<Vector2> Uvs = new();
        private static readonly List<List<int>> TrianglesPerType = new();

        public static void Build(WorldData data, WorldGenerationSettings settings, int chunkX, int chunkZ, Mesh mesh)
        {
            IReadOnlyList<BlockDefinition> blocks = settings.Blocks;
            PrepareBuffers(blocks.Count);

            int startX = chunkX * settings.ChunkSize;
            int startZ = chunkZ * settings.ChunkSize;
            int endX = Math.Min(startX + settings.ChunkSize, data.sizeX);
            int endZ = Math.Min(startZ + settings.ChunkSize, data.sizeZ);

            for (int x = startX; x < endX; x++)
            {
                for (int z = startZ; z < endZ; z++)
                {
                    for (int y = 0; y < data.sizeY; y++)
                    {
                        BlockData cell = data[x, y, z];
                        if (!cell.IsPresent)
                            continue;

                        int submesh = SubmeshFor(blocks, cell.BlockTypeId);
                        for (int face = 0; face < FaceDirections.Length; face++)
                        {
                            Vector3Int d = FaceDirections[face];
                            if (data.IsSolid(x + d.x, y + d.y, z + d.z))
                                continue;
                            AddFace(face, x - startX, y, z - startZ, submesh);
                        }
                    }
                }
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetUVs(0, Uvs);
            mesh.subMeshCount = blocks.Count;
            for (int i = 0; i < blocks.Count; i++)
                mesh.SetTriangles(TrianglesPerType[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static void PrepareBuffers(int typeCount)
        {
            Vertices.Clear();
            Uvs.Clear();
            while (TrianglesPerType.Count < typeCount)
                TrianglesPerType.Add(new List<int>());
            for (int i = 0; i < typeCount; i++)
                TrianglesPerType[i].Clear();
        }

        private static int SubmeshFor(IReadOnlyList<BlockDefinition> blocks, int typeId)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].Id == typeId)
                    return i;
            }
            return 0;
        }

        private static void AddFace(int face, int localX, int localY, int localZ, int submesh)
        {
            int baseIndex = Vertices.Count;
            var origin = new Vector3(localX, localY, localZ);
            foreach (Vector3 corner in FaceCorners[face])
                Vertices.Add(origin + corner);

            // Face 0 is up, face 1 is down, the rest are sides.
            int tile = face == 0 ? 0 : face == 1 ? 2 : 1;
            float uMin = tile / 3f + TileInset;
            float uMax = (tile + 1) / 3f - TileInset;
            foreach (Vector2 pattern in UvPattern)
            {
                Uvs.Add(new Vector2(
                    Mathf.Lerp(uMin, uMax, pattern.x),
                    Mathf.Lerp(TileInset, 1f - TileInset, pattern.y)));
            }

            List<int> triangles = TrianglesPerType[submesh];
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }
    }
}
