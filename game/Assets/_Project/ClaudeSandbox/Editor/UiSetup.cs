#if UNITY_EDITOR
using System.IO;
using System.Text;
using GuildMaster.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace GuildMaster.Sandbox
{
    /// <summary>
    /// Подготовка интерфейса: импорт TMP Essential Resources (настройки TMP и LiberationSans с кириллицей), статический
    /// SDF-шрифт с латиницей, кириллицей и нужными значками, ассет темы <see cref="UiTheme"/> с этим шрифтом.
    /// Порядок: сначала Import TMP Essentials, потом Build Font And Theme.
    /// </summary>
    public static class UiSetup
    {
        private const string EssentialsFolder = "Assets/TextMesh Pro";
        private const string SourceFont = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string UiFolder = "Assets/_Project/Data/UI";
        private const string FontPath = UiFolder + "/LiberationSans Cyrillic SDF.asset";
        private const string ThemePath = UiFolder + "/UiTheme.asset";

        [MenuItem("GuildMaster/Sandbox/UI/Import TMP Essentials")]
        public static void ImportEssentials()
        {
            if (AssetDatabase.IsValidFolder(EssentialsFolder))
            {
                Debug.Log("[UiSetup] TMP Essential Resources already imported");
                return;
            }
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.Log("[UiSetup] TMP Essential Resources import started");
        }

        [MenuItem("GuildMaster/Sandbox/UI/Build Font And Theme")]
        public static void BuildFontAndTheme()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            if (source == null)
            {
                Debug.LogError("[UiSetup] No " + SourceFont + ": import TMP Essentials first");
                return;
            }
            if (!AssetDatabase.IsValidFolder(UiFolder)) AssetDatabase.CreateFolder("Assets/_Project/Data", "UI");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 40, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                font.name = Path.GetFileNameWithoutExtension(FontPath);
                bool complete = font.TryAddCharacters(Characters(), out string missing);
                if (!complete) Debug.LogWarning("[UiSetup] Missing glyphs: " + missing);
                font.atlasPopulationMode = AtlasPopulationMode.Static;

                AssetDatabase.CreateAsset(font, FontPath);
                for (int i = 0; i < font.atlasTextures.Length; i++)
                {
                    Texture2D atlas = font.atlasTextures[i];
                    if (atlas == null) continue;
                    atlas.name = font.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
                EditorUtility.SetDirty(font);
                Debug.Log($"[UiSetup] Font created: {FontPath}, glyphs {font.glyphTable.Count}, atlases {font.atlasTextures.Length}");
            }

            UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            var serialized = new SerializedObject(theme);
            serialized.FindProperty("font").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiSetup] Theme ready: " + ThemePath);
        }

        /// <summary>Латиница, кириллица, пунктуация и значки интерфейса.</summary>
        private static string Characters()
        {
            var chars = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) chars.Append(c);
            for (char c = 'А'; c <= 'я'; c++) chars.Append(c);
            chars.Append("Ёё№«»—–…·•×−±°‹›►◄▲▼■□●○♦♥‼ ");
            return chars.ToString();
        }
    }
}
#endif
