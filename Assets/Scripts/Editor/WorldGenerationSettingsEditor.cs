using System.Collections.Generic;
using CustomMinecraft.Generation;
using UnityEditor;
using UnityEngine;

namespace CustomMinecraft.EditorTools
{
    /// <summary>
    /// Shows validation problems directly on the settings asset, provides the
    /// regenerate-world debug button for live tuning in play mode, and renders a
    /// top-down heightmap preview so noise layers can be tuned without playing.
    /// </summary>
    [CustomEditor(typeof(WorldGenerationSettings))]
    public sealed class WorldGenerationSettingsEditor : Editor
    {
        private const int PreviewSize = 128;

        private Texture2D heightPreview;

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
                if (GUILayout.Button("Update height preview"))
                    heightPreview = RenderHeightPreview(settings);
            }

            if (heightPreview != null)
            {
                Rect rect = GUILayoutUtility.GetAspectRect(1f);
                GUI.DrawTexture(rect, heightPreview, ScaleMode.ScaleToFit);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Outside play mode the regenerate button regenerates data only; visuals appear once the game runs.",
                    MessageType.Info);
            }
        }

        // Top-down view of a PreviewSize x PreviewSize column area from the origin,
        // black = world bottom, white = world ceiling. Seed 0 previews as seed 1.
        private static Texture2D RenderHeightPreview(WorldGenerationSettings settings)
        {
            int seed = settings.Seed != 0 ? settings.Seed : 1;
            int heightmapSeed = WorldGenerator.HeightmapSeed(seed);

            var texture = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };
            for (int z = 0; z < PreviewSize; z++)
            {
                for (int x = 0; x < PreviewSize; x++)
                {
                    int height = WorldGenerator.ColumnHeight(settings, heightmapSeed, x, z);
                    float gray = Mathf.InverseLerp(1f, settings.WorldHeight - 1f, height);
                    texture.SetPixel(x, z, new Color(gray, gray, gray));
                }
            }
            texture.Apply();
            return texture;
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
