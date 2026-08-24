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
            if (AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsPath) != null)
            {
                Debug.Log($"Default assets already exist at {DataFolder}; nothing created.");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsPath);
                return;
            }

            EnsureFolder();

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

            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            Debug.Log($"Created default world assets in {DataFolder}.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets", "Data");
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
