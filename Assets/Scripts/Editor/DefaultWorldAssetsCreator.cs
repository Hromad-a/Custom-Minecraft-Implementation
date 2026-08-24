using UnityEditor;
using UnityEngine;

namespace CustomMinecraft.EditorTools
{
    /// <summary>
    /// One-click scaffolding for the default data assets (rock/grass/snow block
    /// definitions plus wired-up generation settings) so a fresh checkout is
    /// playable without hand-filling inspectors.
    /// </summary>
    public static class DefaultWorldAssetsCreator
    {
        private const string DataFolder = "Assets/Data";
        private const string SettingsPath = DataFolder + "/WorldGenerationSettings.asset";

        [MenuItem("Tools/Custom Minecraft/Create Default World Assets")]
        public static void Create()
        {
            EnsureFolder();
            EnsureHighlightMaterial();

            var existingSettings = AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsPath);
            if (existingSettings != null)
            {
                EnsureReliefLayer(existingSettings);
                Debug.Log($"Default assets already exist at {DataFolder}; nothing created.");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsPath);
                return;
            }

            BlockDefinition rock = CreateBlock("Rock", 1, new Color(0.42f, 0.42f, 0.45f),
                mineDuration: 3f, minHeight: 0, maxHeight: 20);
            BlockDefinition grass = CreateBlock("Grass", 2, new Color(0.36f, 0.63f, 0.27f),
                mineDuration: 1.5f, minHeight: 17, maxHeight: 34);
            BlockDefinition snow = CreateBlock("Snow", 3, new Color(0.94f, 0.95f, 0.96f),
                mineDuration: 0.75f, minHeight: 31, maxHeight: 63);

            var settings = ScriptableObject.CreateInstance<WorldGenerationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);

            var serialized = new SerializedObject(settings);
            SerializedProperty blocks = serialized.FindProperty("blocks");
            blocks.arraySize = 3;
            blocks.GetArrayElementAtIndex(0).objectReferenceValue = rock;
            blocks.GetArrayElementAtIndex(1).objectReferenceValue = grass;
            blocks.GetArrayElementAtIndex(2).objectReferenceValue = snow;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EnsureReliefLayer(settings);

            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            Debug.Log($"Created default world assets in {DataFolder}.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets", "Data");
        }

        // Creates the default relief noise layer and wires it into the settings if
        // its layer list is empty. Runs for fresh and existing settings alike, so
        // older setups migrate to the layered generation automatically.
        private static void EnsureReliefLayer(WorldGenerationSettings settings)
        {
            const string layerPath = DataFolder + "/ReliefLayer.asset";
            var layer = AssetDatabase.LoadAssetAtPath<NoiseLayerDefinition>(layerPath);
            if (layer == null)
            {
                layer = ScriptableObject.CreateInstance<NoiseLayerDefinition>();
                AssetDatabase.CreateAsset(layer, layerPath);
                var serializedLayer = new SerializedObject(layer);
                serializedLayer.FindProperty("salt").intValue = 1;
                serializedLayer.ApplyModifiedPropertiesWithoutUndo();
            }

            if (settings.NoiseLayers.Count == 0)
            {
                var serializedSettings = new SerializedObject(settings);
                SerializedProperty layers = serializedSettings.FindProperty("noiseLayers");
                layers.arraySize = 1;
                layers.GetArrayElementAtIndex(0).objectReferenceValue = layer;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }

        // Translucent white overlay for the block targeting highlight. Created even
        // when the other assets already exist, so older setups can pick it up.
        private static void EnsureHighlightMaterial()
        {
            const string path = DataFolder + "/Highlight.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                return;

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = new Color(1f, 1f, 1f, 0.3f),
            };
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
        }

        private static BlockDefinition CreateBlock(
            string blockName, int id, Color color, float mineDuration, int minHeight, int maxHeight)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            material.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(material, $"{DataFolder}/{blockName}.mat");

            var block = ScriptableObject.CreateInstance<BlockDefinition>();
            AssetDatabase.CreateAsset(block, $"{DataFolder}/{blockName}.asset");

            var serialized = new SerializedObject(block);
            serialized.FindProperty("id").intValue = id;
            serialized.FindProperty("displayName").stringValue = blockName;
            serialized.FindProperty("material").objectReferenceValue = material;
            serialized.FindProperty("mineDuration").floatValue = mineDuration;
            serialized.FindProperty("minHeight").intValue = minHeight;
            serialized.FindProperty("maxHeight").intValue = maxHeight;
            serialized.FindProperty("generationWeight").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return block;
        }
    }
}
