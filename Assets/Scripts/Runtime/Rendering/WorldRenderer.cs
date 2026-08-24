using UnityEngine;

namespace CustomMinecraft.Rendering
{
    /// <summary>
    /// Renders the world as one mesh per chunk. Meshes are disposable derivations
    /// of <see cref="WorldData"/>: rebuilt in full on regeneration, and per chunk
    /// via <see cref="RebuildChunkAt"/> when a block changes. Every chunk shares
    /// the same material array, one URP Lit material per block type.
    /// </summary>
    [RequireComponent(typeof(World))]
    public sealed class WorldRenderer : MonoBehaviour
    {
        private World world;
        private Material[] materials;
        private MeshFilter[,] chunks;
        private int chunkSize;

        private void Awake()
        {
            world = GetComponent<World>();
            world.Regenerated += RebuildAll;
        }

        private void Start()
        {
            // World generates in its own Awake, before we could subscribe.
            if (world.Data != null)
                RebuildAll();
        }

        private void OnDestroy()
        {
            world.Regenerated -= RebuildAll;
        }

        public void RebuildAll()
        {
            EnsureMaterials();
            EnsureChunks();
            for (int cx = 0; cx < chunks.GetLength(0); cx++)
            {
                for (int cz = 0; cz < chunks.GetLength(1); cz++)
                {
                    chunks[cx, cz].GetComponent<MeshRenderer>().sharedMaterials = materials;
                    RebuildChunk(cx, cz);
                }
            }
        }

        /// <summary>
        /// Rebuilds the chunk containing cell (x, z), plus adjacent chunks when the
        /// cell lies on a chunk border (their buried faces may just have been exposed).
        /// </summary>
        public void RebuildChunkAt(int x, int z)
        {
            int cx = x / chunkSize;
            int cz = z / chunkSize;
            RebuildChunk(cx, cz);

            int localX = x - cx * chunkSize;
            int localZ = z - cz * chunkSize;
            if (localX == 0 && cx > 0) RebuildChunk(cx - 1, cz);
            if (localX == chunkSize - 1 && cx < chunks.GetLength(0) - 1) RebuildChunk(cx + 1, cz);
            if (localZ == 0 && cz > 0) RebuildChunk(cx, cz - 1);
            if (localZ == chunkSize - 1 && cz < chunks.GetLength(1) - 1) RebuildChunk(cx, cz + 1);
        }

        private void RebuildChunk(int cx, int cz)
        {
            ChunkMeshBuilder.Build(world.Data, world.Settings, cx, cz, chunks[cx, cz].sharedMesh);
        }

        private void EnsureMaterials()
        {
            var blocks = world.Settings.Blocks;
            materials = new Material[blocks.Count];
            for (int i = 0; i < blocks.Count; i++)
            {
                materials[i] = blocks[i].Material;
                if (materials[i] == null)
                    Debug.LogError($"Block '{blocks[i].DisplayName}' has no material assigned.", blocks[i]);
            }
        }

        private void EnsureChunks()
        {
            chunkSize = world.Settings.ChunkSize;
            int countX = Mathf.CeilToInt(world.Data.sizeX / (float)chunkSize);
            int countZ = Mathf.CeilToInt(world.Data.sizeZ / (float)chunkSize);
            if (chunks != null && chunks.GetLength(0) == countX && chunks.GetLength(1) == countZ)
                return;

            DestroyChunks();
            chunks = new MeshFilter[countX, countZ];
            for (int cx = 0; cx < countX; cx++)
            {
                for (int cz = 0; cz < countZ; cz++)
                {
                    var chunkObject = new GameObject($"Chunk_{cx}_{cz}");
                    chunkObject.transform.SetParent(transform, false);
                    chunkObject.transform.localPosition = new Vector3(cx * chunkSize, 0f, cz * chunkSize);

                    var filter = chunkObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = new Mesh { name = chunkObject.name };
                    chunkObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
                    chunks[cx, cz] = filter;
                }
            }
        }

        private void DestroyChunks()
        {
            if (chunks == null)
                return;
            foreach (MeshFilter chunk in chunks)
            {
                Destroy(chunk.sharedMesh);
                Destroy(chunk.gameObject);
            }
            chunks = null;
        }
    }
}
