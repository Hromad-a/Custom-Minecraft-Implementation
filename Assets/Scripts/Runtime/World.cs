using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomMinecraft.Generation;
using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// Scene-side owner of the world state. Creates the (lazily generated) world
    /// on startup and exposes <see cref="Regenerated"/> so other systems
    /// (renderer, player) can react to the world being rebuilt.
    /// </summary>
    public sealed class World : MonoBehaviour
    {
        [SerializeField] private WorldGenerationSettings settings;

        public WorldGenerationSettings Settings => settings;
        public WorldData Data { get; private set; }
        public int CurrentSeed { get; private set; }

        public event Action Regenerated;

        private void Awake()
        {
            Regenerate();
        }

        [ContextMenu("Regenerate")]
        public void Regenerate()
        {
            if (settings == null)
            {
                Debug.LogError("World has no WorldGenerationSettings assigned.", this);
                return;
            }

            var errors = new List<string>();
            if (!settings.Validate(errors))
            {
                Debug.LogError(
                    $"World generation settings are invalid:\n - {string.Join("\n - ", errors)}",
                    settings);
                return;
            }

            CurrentSeed = WorldGenerator.ResolveSeed(settings.Seed);
            Data = new WorldData(settings, CurrentSeed);
            Debug.Log($"World ready: seed {CurrentSeed}, height {settings.WorldHeight}, chunks generate on demand.", this);
            Regenerated?.Invoke();
        }

        /// <summary>Terrain surface height of the column at (x, z).</summary>
        public int SurfaceHeight(int x, int z) =>
            WorldGenerator.ColumnHeight(settings, WorldGenerator.HeightmapSeed(CurrentSeed), x, z);

        public bool CanMine(Vector3Int cell) =>
            Data != null
            && Data.InBounds(cell.x, cell.y, cell.z)
            && cell.y > 0
            && Data[cell.x, cell.y, cell.z].IsPresent;

        public bool TryMine(Vector3Int cell)
        {
            if (!CanMine(cell))
                return false;
            Data.SetPresence(cell.x, cell.y, cell.z, false);
            return true;
        }

        public bool CanPlace(Vector3Int cell) =>
            Data != null
            && Data.InBounds(cell.x, cell.y, cell.z)
            && !Data[cell.x, cell.y, cell.z].IsPresent;

        public bool TryPlace(Vector3Int cell)
        {
            if (!CanPlace(cell))
                return false;
            Data.SetPresence(cell.x, cell.y, cell.z, true);
            return true;
        }

        [ContextMenu("Export To JSON")]
        public void ExportToJson()
        {
            if (Data == null)
            {
                Debug.LogWarning("No world data yet; generate a world first.", this);
                return;
            }

            var export = new WorldExport
            {
                seed = Data.seed,
                chunkSize = Data.chunkSize,
                sizeY = Data.sizeY,
            };
            foreach (var coord in Data.Chunks.Keys.OrderBy(c => c.x).ThenBy(c => c.y))
            {
                export.chunks.Add(new ChunkExport
                {
                    chunkX = coord.x,
                    chunkZ = coord.y,
                    cells = Data.Chunks[coord],
                });
            }

            string path = Path.Combine(
                Application.persistentDataPath,
                $"world_{CurrentSeed}_{DateTime.Now:HHmmss}.json");
            File.WriteAllText(path, JsonUtility.ToJson(export, prettyPrint: true));
            Debug.Log($"Exported {export.chunks.Count} generated chunks to {path}", this);
        }

        // JSON shape of an export: the generated chunks in a stable sorted order,
        // so two exports of the same seed and area are diffable files.
        [Serializable]
        private sealed class WorldExport
        {
            public int seed;
            public int chunkSize;
            public int sizeY;
            public List<ChunkExport> chunks = new();
        }

        [Serializable]
        private sealed class ChunkExport
        {
            public int chunkX;
            public int chunkZ;
            public BlockData[] cells;
        }
    }
}
