using System.Collections.Generic;
using UnityEngine;

namespace CustomMinecraft.Rendering
{
    /// <summary>
    /// Streams chunk meshes around the viewer: chunks within the view distance
    /// get one GameObject and mesh each, built within a per-frame time budget
    /// nearest-first; chunks left behind are destroyed. Meshes stay disposable
    /// derivations of <see cref="WorldData"/> — <see cref="RebuildChunkAt"/>
    /// re-derives one after a block edit. Every chunk shares the same material
    /// array, one material per block type. Scene fog is matched to the view
    /// distance to hide the streamed edge.
    /// </summary>
    [RequireComponent(typeof(World))]
    public sealed class WorldRenderer : MonoBehaviour
    {
        [Tooltip("Chunks stream around this transform; defaults to the main camera.")]
        [SerializeField] private Transform viewer;

        private World world;
        private Material[] materials;
        private readonly Dictionary<Vector2Int, MeshFilter> activeChunks = new();
        private readonly List<Vector2Int> buildQueue = new();
        private readonly List<Vector2Int> unloadBuffer = new();
        private Vector2Int viewerChunk;
        private bool streamingDirty = true;

        private void Awake()
        {
            world = GetComponent<World>();
            world.Regenerated += OnWorldRegenerated;
        }

        private void Start()
        {
            if (viewer == null && Camera.main != null)
                viewer = Camera.main.transform;
            EnsureMaterials();
            ApplyFog();
        }

        private void OnDestroy()
        {
            world.Regenerated -= OnWorldRegenerated;
        }

        private void Update()
        {
            if (world.Data == null || viewer == null)
                return;

            Vector2Int current = ChunkCoordAt(viewer.position);
            if (current != viewerChunk || streamingDirty)
            {
                viewerChunk = current;
                streamingDirty = false;
                RefreshStreaming();
            }
            BuildQueuedChunks();
        }

        /// <summary>
        /// Rebuilds the chunk containing cell (x, z), plus adjacent chunks when the
        /// cell lies on a chunk border (their buried faces may just have been exposed).
        /// </summary>
        public void RebuildChunkAt(int x, int z)
        {
            int chunkSize = world.Data.chunkSize;
            int cx = WorldData.FloorDiv(x, chunkSize);
            int cz = WorldData.FloorDiv(z, chunkSize);
            RebuildIfLoaded(cx, cz);

            int localX = x - cx * chunkSize;
            int localZ = z - cz * chunkSize;
            if (localX == 0) RebuildIfLoaded(cx - 1, cz);
            if (localX == chunkSize - 1) RebuildIfLoaded(cx + 1, cz);
            if (localZ == 0) RebuildIfLoaded(cx, cz - 1);
            if (localZ == chunkSize - 1) RebuildIfLoaded(cx, cz + 1);
        }

        private void OnWorldRegenerated()
        {
            foreach (MeshFilter chunk in activeChunks.Values)
            {
                Destroy(chunk.sharedMesh);
                Destroy(chunk.gameObject);
            }
            activeChunks.Clear();
            buildQueue.Clear();
            EnsureMaterials();
            ApplyFog();
            streamingDirty = true;
        }

        // Recomputed whenever the viewer crosses a chunk border: drop chunks that
        // fell out of range, queue missing ones nearest-first.
        private void RefreshStreaming()
        {
            int radius = world.Settings.ViewDistance;

            unloadBuffer.Clear();
            foreach (KeyValuePair<Vector2Int, MeshFilter> entry in activeChunks)
            {
                Vector2Int offset = entry.Key - viewerChunk;
                if (Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) > radius + 1)
                    unloadBuffer.Add(entry.Key);
            }
            foreach (Vector2Int coord in unloadBuffer)
            {
                Destroy(activeChunks[coord].sharedMesh);
                Destroy(activeChunks[coord].gameObject);
                activeChunks.Remove(coord);
            }

            buildQueue.Clear();
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    var coord = new Vector2Int(viewerChunk.x + dx, viewerChunk.y + dz);
                    if (!activeChunks.ContainsKey(coord))
                        buildQueue.Add(coord);
                }
            }
            buildQueue.Sort((a, b) =>
                (a - viewerChunk).sqrMagnitude.CompareTo((b - viewerChunk).sqrMagnitude));
        }

        // Works through the queue in small units — one chunk's data generation or
        // one mesh build per step — until the frame's time budget is spent. Data
        // for a chunk and its neighbors is prepared in earlier frames than its
        // mesh, so no single frame pays for everything at once.
        private void BuildQueuedChunks()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            float budget = world.Settings.StreamingBudgetMs;

            while (buildQueue.Count > 0 && timer.Elapsed.TotalMilliseconds < budget)
            {
                Vector2Int coord = buildQueue[0];
                if (activeChunks.ContainsKey(coord))
                {
                    buildQueue.RemoveAt(0);
                    continue;
                }

                if (GenerateOneMissingDataChunk(coord))
                    continue;

                CreateChunk(coord);
                buildQueue.RemoveAt(0);
            }
        }

        // Meshing a chunk reads its own data plus all four neighbors (border face
        // checks). Generating one missing piece per step keeps each unit small.
        private bool GenerateOneMissingDataChunk(Vector2Int coord)
        {
            Vector2Int[] needed =
            {
                coord, new(coord.x + 1, coord.y), new(coord.x - 1, coord.y),
                new(coord.x, coord.y + 1), new(coord.x, coord.y - 1),
            };
            foreach (Vector2Int c in needed)
            {
                if (!world.Data.HasChunk(c.x, c.y))
                {
                    world.Data.EnsureChunk(c.x, c.y);
                    return true;
                }
            }
            return false;
        }

        private void CreateChunk(Vector2Int coord)
        {
            int chunkSize = world.Data.chunkSize;
            var chunkObject = new GameObject($"Chunk_{coord.x}_{coord.y}");
            chunkObject.transform.SetParent(transform, false);
            chunkObject.transform.localPosition = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);

            var filter = chunkObject.AddComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = chunkObject.name };
            chunkObject.AddComponent<MeshRenderer>().sharedMaterials = materials;

            ChunkMeshBuilder.Build(world.Data, world.Settings, coord.x, coord.y, filter.sharedMesh);
            activeChunks.Add(coord, filter);
        }

        private void RebuildIfLoaded(int cx, int cz)
        {
            if (activeChunks.TryGetValue(new Vector2Int(cx, cz), out MeshFilter filter))
                ChunkMeshBuilder.Build(world.Data, world.Settings, cx, cz, filter.sharedMesh);
        }

        private Vector2Int ChunkCoordAt(Vector3 position)
        {
            int chunkSize = world.Data.chunkSize;
            return new Vector2Int(
                WorldData.FloorDiv(Mathf.FloorToInt(position.x), chunkSize),
                WorldData.FloorDiv(Mathf.FloorToInt(position.z), chunkSize));
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

        // Distant chunks fade into fog instead of visibly popping in at the edge
        // of the streamed area.
        private void ApplyFog()
        {
            float viewDistance = world.Settings.ViewDistance * world.Settings.ChunkSize;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = viewDistance * 0.5f;
            RenderSettings.fogEndDistance = viewDistance * 0.95f;
        }
    }
}
