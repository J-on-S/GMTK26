using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Modal that lets the user pick and edit a <c>BuildConfig</c> before the build starts.
    /// </summary>
    /// <remarks>
    /// Invariant: configs stored in <c>Editor/</c> folders are still selectable, unlike in Unity's
    /// object picker.
    /// Invariant: the last chosen config is preselected on the next open.
    /// </remarks>
    public class BuildConfigWindow : EditorWindow
    {
        private static string LastConfigPathKey => "BuildTools." + Application.productName + ".LastConfigPath";

        public BuildConfig Config;
        /// <summary><c>true</c> when the user pressed Build; <c>false</c> on Cancel or window close.</summary>
        public bool WasConfirmed { get; private set; }

        private Editor _cachedEditor;
        private Vector2 _scroll;

        private BuildConfig[] _allConfigs = System.Array.Empty<BuildConfig>();
        private string[] _allConfigLabels = System.Array.Empty<string>();

        /// <summary>Opens the picker and blocks until the user confirms or cancels.</summary>
        /// <returns>The closed window; read <c>WasConfirmed</c> and <c>Config</c> from it.</returns>
        public static BuildConfigWindow ShowModal()
        {
            BuildConfigWindow window = CreateInstance<BuildConfigWindow>();
            window.titleContent = new GUIContent("Build Configurator");
            window.minSize = new Vector2(460, 620);
            window.RefreshConfigList();
            window.LoadLastConfig();
            window.ShowModalUtility();
            return window;
        }

        private void RefreshConfigList()
        {
            _allConfigs = AssetDatabase.FindAssets("t:BuildConfig")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildConfig>)
                .Where(c => c != null)
                .OrderBy(c => c.name)
                .ToArray();

            _allConfigLabels = _allConfigs
                .Select(c => $"{c.name}  ({Path.GetDirectoryName(AssetDatabase.GetAssetPath(c))})")
                .ToArray();
        }

        private void LoadLastConfig()
        {
            string path = EditorPrefs.GetString(LastConfigPathKey, "");
            if (!string.IsNullOrEmpty(path))
            {
                Config = AssetDatabase.LoadAssetAtPath<BuildConfig>(path);
                if (Config != null) return;
            }

            if (_allConfigs.Length > 0)
            {
                Config = _allConfigs[0];
                EditorPrefs.SetString(LastConfigPathKey, AssetDatabase.GetAssetPath(Config));
            }
        }

        private void CreateNewConfigAsset()
        {
            // The asset only deserializes from an Editor/ folder.
            const string editorDir = "Assets/Editor";
            if (!AssetDatabase.IsValidFolder(editorDir))
                AssetDatabase.CreateFolder("Assets", "Editor");

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{editorDir}/BuildConfig.asset");
            BuildConfig asset = CreateInstance<BuildConfig>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Config = asset;
            EditorPrefs.SetString(LastConfigPathKey, assetPath);
            RefreshConfigList();
            RebuildEditor();

            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[BuildConfigWindow] Created new BuildConfig at {assetPath}");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build Configurator", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Pick a BuildConfig asset, tweak it, then hit Build.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                int currentIndex = System.Array.IndexOf(_allConfigs, Config);
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup("Config Asset", currentIndex, _allConfigLabels);
                if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < _allConfigs.Length)
                {
                    Config = _allConfigs[newIndex];
                    RebuildEditor();
                    EditorPrefs.SetString(LastConfigPathKey, AssetDatabase.GetAssetPath(Config));
                }

                if (GUILayout.Button("↻", GUILayout.Width(24))) RefreshConfigList();
                if (Config != null && GUILayout.Button("Ping", GUILayout.Width(44))) EditorGUIUtility.PingObject(Config);
            }

            EditorGUILayout.Space();

            if (Config == null)
            {
                EditorGUILayout.HelpBox(
                    "No BuildConfig found.\n\nThe asset must live inside an Editor/ folder " +
                    "(e.g. Assets/Editor/BuildConfig.asset) because BuildConfig is an editor-only type.",
                    MessageType.Warning);
                EditorGUILayout.Space();
                if (GUILayout.Button("Create New BuildConfig in Assets/Editor/", GUILayout.Height(26)))
                    CreateNewConfigAsset();
            }
            else
            {
                if (_cachedEditor == null || _cachedEditor.target != Config) RebuildEditor();

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                _cachedEditor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(Config == null))
                {
                    if (GUILayout.Button("Build", GUILayout.Width(110), GUILayout.Height(28)))
                    {
                        WasConfirmed = true;
                        Close();
                    }
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(110), GUILayout.Height(28)))
                {
                    WasConfirmed = false;
                    Close();
                }
            }
        }

        private void RebuildEditor()
        {
            if (_cachedEditor != null)
            {
                DestroyImmediate(_cachedEditor);
                _cachedEditor = null;
            }
            if (Config != null) _cachedEditor = Editor.CreateEditor(Config);
        }

        private void OnDisable()
        {
            if (_cachedEditor != null)
            {
                DestroyImmediate(_cachedEditor);
                _cachedEditor = null;
            }
        }
    }
}
