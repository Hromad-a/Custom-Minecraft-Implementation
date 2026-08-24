using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CustomMinecraft.EditorTools
{
    /// <summary>
    /// Shows validation problems directly on the settings asset and provides the
    /// regenerate-world debug button for live tuning in play mode.
    /// </summary>
    [CustomEditor(typeof(WorldGenerationSettings))]
    public sealed class WorldGenerationSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var settings = (WorldGenerationSettings)target;
            var errors = new List<string>();
            bool valid = settings.Validate(errors);

            EditorGUILayout.Space();
            if (!valid)
            {
                EditorGUILayout.HelpBox(
                    "Invalid settings:\n - " + string.Join("\n - ", errors),
                    MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Regenerate world"))
                    RegenerateSceneWorld();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Outside play mode the button regenerates data only; visuals appear once the game runs.",
                    MessageType.Info);
            }
        }

        private static void RegenerateSceneWorld()
        {
            var world = Object.FindFirstObjectByType<World>();
            if (world == null)
            {
                Debug.LogWarning("No World component found in the open scene.");
                return;
            }
            world.Regenerate();
        }
    }
}
