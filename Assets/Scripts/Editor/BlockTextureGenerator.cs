using System.IO;
using CustomMinecraft.Generation;
using UnityEditor;
using UnityEngine;

namespace CustomMinecraft.EditorTools
{
    /// <summary>
    /// Generates a seamless grayscale texture atlas per block type and assigns it
    /// to the block's material as the base map. Each atlas is a horizontal strip
    /// of three tiles: top | side | bottom. The noise lattice wraps at the tile
    /// border (noise on a torus), so the tiles tile perfectly across faces; the
    /// material's color provides the block's tint.
    /// </summary>
    public static class BlockTextureGenerator
    {
        private const int TileSize = 128;
        private const int Octaves = 4;
        private const int NoiseSeed = 777;
        private const string TextureFolder = "Assets/Data/Textures";

        // Pattern character per block, chosen by display name in PresetFor.
        private enum Style
        {
            Rocky,    // ridged noise: sharp veins and cracks
            Speckled, // soft base with scattered dark speckles
            Soft,     // gentle base with sparse bright glints
        }

        // period = feature size (higher = finer grain), contrast = how harsh the
        // value swings, brightness = the mid gray.
        private readonly struct Preset
        {
            public readonly int Period;
            public readonly float Contrast;
            public readonly float Brightness;
            public readonly Style Style;

            public Preset(int period, float contrast, float brightness, Style style)
            {
                Period = period;
                Contrast = contrast;
                Brightness = brightness;
                Style = style;
            }
        }

        private enum Tile
        {
            Top = 0,
            Side = 1,
            Bottom = 2,
        }

        [MenuItem("Tools/Custom Minecraft/Generate Block Textures")]
        public static void Generate()
        {
            string[] guids = AssetDatabase.FindAssets("t:WorldGenerationSettings");
            if (guids.Length == 0)
            {
                Debug.LogError("No WorldGenerationSettings asset found; create the world assets first.");
                return;
            }
            var settings = AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

            if (!AssetDatabase.IsValidFolder(TextureFolder))
                AssetDatabase.CreateFolder("Assets/Data", "Textures");

            foreach (BlockDefinition block in settings.Blocks)
            {
                if (block == null || block.Material == null)
                {
                    Debug.LogWarning($"Skipping block without material: {block}");
                    continue;
                }

                // Seeded per block id so every block gets its own pattern layout.
                int blockSeed = DeterministicHash.DeriveSeed(NoiseSeed, block.Id);
                string path = $"{TextureFolder}/{block.DisplayName}Atlas.png";
                WriteAtlas(path, PresetFor(block.DisplayName), blockSeed);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    importer.SaveAndReimport();
                }
                block.Material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                EditorUtility.SetDirty(block.Material);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Block texture atlases generated in {TextureFolder}.");
        }

        private static Preset PresetFor(string blockName) => blockName switch
        {
            "Rock" => new Preset(5, 0.7f, 0.48f, Style.Rocky),
            "Grass" => new Preset(9, 0.45f, 0.55f, Style.Speckled),
            "Snow" => new Preset(12, 0.2f, 0.8f, Style.Soft),
            _ => new Preset(8, 0.4f, 0.55f, Style.Speckled),
        };

        private static void WriteAtlas(string path, Preset preset, int blockSeed)
        {
            var atlas = new Texture2D(TileSize * 3, TileSize, TextureFormat.RGBA32, false);
            FillTile(atlas, Tile.Top, preset, blockSeed);
            FillTile(atlas, Tile.Side, preset, blockSeed);
            FillTile(atlas, Tile.Bottom, preset, blockSeed);
            atlas.Apply();
            File.WriteAllBytes(path, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
        }

        private static void FillTile(Texture2D atlas, Tile tile, Preset preset, int blockSeed)
        {
            int tileSeed = DeterministicHash.DeriveSeed(blockSeed, (int)tile);
            for (int py = 0; py < TileSize; py++)
            {
                for (int px = 0; px < TileSize; px++)
                {
                    float u = (px + 0.5f) / TileSize;
                    float v = (py + 0.5f) / TileSize;
                    float value = StyledValue(u, v, preset, tileSeed);

                    if (tile == Tile.Side)
                    {
                        // Vertical streaks: fine detail across, stretched along V
                        // (which the mesh maps to world up).
                        float streaks = FbmTileable(u, v, preset.Period * 3, 2,
                            DeterministicHash.DeriveSeed(tileSeed, 99));
                        value = value * 0.6f + streaks * 0.4f;
                    }

                    float brightness = preset.Brightness - TileDarkening(tile);
                    float gray = Mathf.Clamp01(brightness + (value - 0.5f) * preset.Contrast);
                    atlas.SetPixel((int)tile * TileSize + px, py, new Color(gray, gray, gray, 1f));
                }
            }
        }

        // Sides sit a bit darker than tops, bottoms darker still.
        private static float TileDarkening(Tile tile) =>
            tile == Tile.Side ? 0.06f : tile == Tile.Bottom ? 0.12f : 0f;

        private static float StyledValue(float u, float v, Preset preset, int seed)
        {
            switch (preset.Style)
            {
                case Style.Rocky:
                    return RidgedFbm(u, v, preset.Period, seed);

                case Style.Speckled:
                {
                    // Scattered dark dots punched into the base pattern.
                    float baseValue = FbmTileable(u, v, preset.Period, preset.Period, seed);
                    float dots = ValueNoiseTileable(u, v, preset.Period * 5, preset.Period * 5,
                        DeterministicHash.DeriveSeed(seed, 7));
                    return baseValue - Mathf.Max(0f, dots - 0.7f) * 1.5f;
                }

                default:
                {
                    // Soft base with sparse bright glints.
                    float baseValue = FbmTileable(u, v, preset.Period, preset.Period, seed);
                    float glints = ValueNoiseTileable(u, v, preset.Period * 6, preset.Period * 6,
                        DeterministicHash.DeriveSeed(seed, 7));
                    return baseValue + Mathf.Max(0f, glints - 0.85f) * 2f;
                }
            }
        }

        // Fbm of ridged noise: each octave folds the value around its middle,
        // creating sharp vein lines instead of soft blobs.
        private static float RidgedFbm(float u, float v, int period, int seed)
        {
            float sum = 0f;
            float amplitude = 1f;
            float amplitudeSum = 0f;
            for (int octave = 0; octave < Octaves; octave++)
            {
                int frequency = 1 << octave;
                float noise = ValueNoiseTileable(u, v, period * frequency, period * frequency,
                    DeterministicHash.DeriveSeed(seed, octave));
                float ridge = 1f - Mathf.Abs(noise * 2f - 1f);
                sum += ridge * ridge * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
            }
            return sum / amplitudeSum;
        }

        private static float FbmTileable(float u, float v, int periodX, int periodY, int seed)
        {
            float sum = 0f;
            float amplitude = 1f;
            float amplitudeSum = 0f;
            for (int octave = 0; octave < Octaves; octave++)
            {
                int frequency = 1 << octave;
                sum += ValueNoiseTileable(u, v, periodX * frequency, periodY * frequency,
                    DeterministicHash.DeriveSeed(seed, octave)) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
            }
            return sum / amplitudeSum;
        }

        // Value noise whose lattice wraps at the period, so the result tiles
        // seamlessly over [0, 1) x [0, 1).
        private static float ValueNoiseTileable(float u, float v, int periodX, int periodY, int seed)
        {
            float x = u * periodX;
            float y = v * periodY;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float sx = Smoothstep(x - x0);
            float sy = Smoothstep(y - y0);

            float v00 = Lattice(x0, y0, periodX, periodY, seed);
            float v10 = Lattice(x0 + 1, y0, periodX, periodY, seed);
            float v01 = Lattice(x0, y0 + 1, periodX, periodY, seed);
            float v11 = Lattice(x0 + 1, y0 + 1, periodX, periodY, seed);

            float bottom = Mathf.Lerp(v00, v10, sx);
            float top = Mathf.Lerp(v01, v11, sx);
            return Mathf.Lerp(bottom, top, sy);
        }

        private static float Lattice(int x, int y, int periodX, int periodY, int seed) =>
            DeterministicHash.Value01(x % periodX, y % periodY, 0, seed);

        private static float Smoothstep(float t) => t * t * (3f - 2f * t);
    }
}
