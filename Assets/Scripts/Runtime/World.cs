using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CustomMinecraft.Generation;
using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// Scene-side owner of the world state. Generates on startup and exposes
    /// <see cref="Regenerated"/> so later systems (renderer, player) can react
    /// to the world being rebuilt.
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
            Data = WorldGenerator.Generate(settings, CurrentSeed);
            Debug.Log(BuildSummary(), this);
            Regenerated?.Invoke();
        }

        [ContextMenu("Export To JSON")]
        public void ExportToJson()
        {
            if (Data == null)
            {
                Debug.LogWarning("No world data yet; generate a world first.", this);
                return;
            }

            string path = Path.Combine(
                Application.persistentDataPath,
                $"world_{CurrentSeed}_{DateTime.Now:HHmmss}.json");
            File.WriteAllText(path, JsonUtility.ToJson(Data, prettyPrint: true));
            Debug.Log($"World exported to {path}", this);
        }

        private string BuildSummary()
        {
            var countsByType = new Dictionary<int, int>();
            int presentTotal = 0;

            for (int y = 0; y < Data.sizeY; y++)
            {
                for (int z = 0; z < Data.sizeZ; z++)
                {
                    for (int x = 0; x < Data.sizeX; x++)
                    {
                        BlockData cell = Data[x, y, z];
                        if (!cell.IsPresent)
                            continue;
                        presentTotal++;
                        countsByType.TryGetValue(cell.BlockTypeId, out int count);
                        countsByType[cell.BlockTypeId] = count + 1;
                    }
                }
            }

            var summary = new StringBuilder()
                .Append($"World generated: {Data.sizeX}x{Data.sizeY}x{Data.sizeZ}, seed {CurrentSeed}, ")
                .Append($"{presentTotal:N0} blocks.");
            foreach (KeyValuePair<int, int> entry in countsByType)
            {
                BlockDefinition definition = settings.BlockForId(entry.Key);
                string label = definition != null ? definition.DisplayName : $"id {entry.Key}";
                summary.Append($" {label}: {entry.Value:N0}.");
            }
            return summary.ToString();
        }
    }
}
