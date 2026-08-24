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
    /// list, so every chunk shares the same material array.
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

        // Reused across builds so remeshing does not allocate.
        private static readonly List<Vector3> Vertices = new();
        private static readonly List<List<int>> TrianglesPerType = new();
        private static readonly Dictionary<int, int> SubmeshByTypeId = new();

        public static void Build(WorldData data, WorldGenerationSettings settings, int chunkX, int chunkZ, Mesh mesh)
        {
            IReadOnlyList<BlockDefinition> blocks = settings.Blocks;
            PrepareBuffers(blocks);

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

                        int submesh = SubmeshByTypeId[cell.BlockTypeId];
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
            mesh.subMeshCount = blocks.Count;
            for (int i = 0; i < blocks.Count; i++)
                mesh.SetTriangles(TrianglesPerType[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static void PrepareBuffers(IReadOnlyList<BlockDefinition> blocks)
        {
            Vertices.Clear();
            while (TrianglesPerType.Count < blocks.Count)
                TrianglesPerType.Add(new List<int>());

            SubmeshByTypeId.Clear();
            for (int i = 0; i < blocks.Count; i++)
            {
                TrianglesPerType[i].Clear();
                SubmeshByTypeId[blocks[i].Id] = i;
            }
        }

        private static void AddFace(int face, int localX, int localY, int localZ, int submesh)
        {
            int baseIndex = Vertices.Count;
            var origin = new Vector3(localX, localY, localZ);
            foreach (Vector3 corner in FaceCorners[face])
                Vertices.Add(origin + corner);

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
