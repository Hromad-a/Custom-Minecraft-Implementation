using System;
using System.Collections.Generic;
using System.IO;
using CustomMinecraft.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CustomMinecraft
{
    /// <summary>
    /// Single-slot save/load: F5 saves, F9 loads. A save is the seed, the player
    /// transform, and the world diff (mined/placed cells) — loading regenerates
    /// from the seed and reapplies the diff, with generate-on-access pulling in
    /// any chunk a modification touches. Written as readable JSON next to the
    /// world exports in persistentDataPath.
    /// </summary>
    [RequireComponent(typeof(World))]
    public sealed class SaveSystem : MonoBehaviour
    {
        private const int SaveVersion = 1;

        private World world;
        private VoxelPlayerController player;

        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        private void Awake()
        {
            world = GetComponent<World>();
        }

        private void Start()
        {
            player = FindFirstObjectByType<VoxelPlayerController>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.f5Key.wasPressedThisFrame)
                Save();
            if (keyboard.f9Key.wasPressedThisFrame)
                Load();
        }

        public void Save()
        {
            if (world.Data == null)
            {
                Debug.LogWarning("Nothing to save; no world exists.", this);
                return;
            }

            var save = new SaveData { version = SaveVersion, seed = world.CurrentSeed };
            if (player != null)
            {
                save.hasPlayer = true;
                save.playerPosition = player.transform.position;
                save.playerYaw = player.Yaw;
                save.playerPitch = player.Pitch;
            }
            foreach (KeyValuePair<Vector3Int, bool> entry in world.Modifications)
            {
                save.modifications.Add(new ModifiedCell
                {
                    x = entry.Key.x,
                    y = entry.Key.y,
                    z = entry.Key.z,
                    isPresent = entry.Value,
                });
            }

            File.WriteAllText(SavePath, JsonUtility.ToJson(save, prettyPrint: true));
            Debug.Log($"Saved seed {save.seed} with {save.modifications.Count} modifications to {SavePath}", this);
        }

        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log($"No save file found at {SavePath}", this);
                return;
            }

            SaveData save;
            try
            {
                save = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not read the save file: {exception.Message}", this);
                return;
            }
            if (save == null || save.version != SaveVersion)
            {
                Debug.LogError("The save file has an unsupported version; not loading it.", this);
                return;
            }

            world.RegenerateWithSeed(save.seed);
            if (world.Data == null)
                return;

            foreach (ModifiedCell cell in save.modifications)
                world.RestoreModification(new Vector3Int(cell.x, cell.y, cell.z), cell.isPresent);
            if (save.hasPlayer && player != null)
                player.Teleport(save.playerPosition, save.playerYaw, save.playerPitch);

            Debug.Log($"Loaded seed {save.seed} with {save.modifications.Count} modifications.", this);
        }

        [Serializable]
        private sealed class SaveData
        {
            public int version;
            public int seed;
            public bool hasPlayer;
            public Vector3 playerPosition;
            public float playerYaw;
            public float playerPitch;
            public List<ModifiedCell> modifications = new();
        }

        [Serializable]
        private sealed class ModifiedCell
        {
            public int x;
            public int y;
            public int z;
            public bool isPresent;
        }
    }
}
