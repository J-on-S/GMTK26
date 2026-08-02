using System.Collections.Generic;
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
    /// Invariant: multi-target mode and its rows are remembered between builds, so a two-platform
    /// release does not have to be re-typed every time.
    /// </remarks>
    public class BuildConfigWindow : EditorWindow
    {
        private static string Prefix => "BuildTools." + Application.productName + ".";
        private static string LastConfigPathKey => Prefix + "LastConfigPath";
        private static string MultiTargetKey => Prefix + "MultiTarget";
        private static string MultiTargetPlansKey => Prefix + "MultiTargetPlans";

        /// <summary>Platforms offered in multi-target mode: the ones <c>ItchUploader</c> knows a channel for.</summary>
        private static readonly BuildTarget[] SelectableTargets =
        {
            BuildTarget.StandaloneWindows64,
            BuildTarget.WebGL,
            BuildTarget.StandaloneOSX,
            BuildTarget.StandaloneLinux64,
        };

        public BuildConfig Config;

        /// <summary><c>true</c> when the user pressed Build; <c>false</c> on Cancel or window close.</summary>
        public bool WasConfirmed { get; private set; }

        /// <summary><c>true</c> when the user asked for several platforms in one run; read <see cref="Plans"/> then.</summary>
        public bool BuildAllTargets { get; private set; }

        /// <summary>What to build in multi-target mode, in the order it will be built. Empty otherwise.</summary>
        public List<TargetBuildPlan> Plans { get; private set; } = new List<TargetBuildPlan>();

        private Editor _cachedEditor;
        private Vector2 _scroll;

        private BuildConfig[] _allConfigs = System.Array.Empty<BuildConfig>();
        private string[] _allConfigLabels = System.Array.Empty<string>();
        private string[] _targetLabels = System.Array.Empty<string>();

        /// <summary>Opens the picker and blocks until the user confirms or cancels.</summary>
        /// <returns>The closed window; read <c>WasConfirmed</c> and <c>Config</c> or <c>Plans</c> from it.</returns>
        public static BuildConfigWindow ShowModal()
        {
            BuildConfigWindow window = CreateInstance<BuildConfigWindow>();
            window.titleContent = new GUIContent("Build Configurator");
            window.minSize = new Vector2(560, 660);
            window.RefreshConfigList();
            window.LoadLastConfig();
            window.LoadMultiTargetState();
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

            _targetLabels = SelectableTargets.Select(t => t.ToString()).ToArray();
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

        /// <summary>Reads back the rows the last multi-target build used.</summary>
        /// <remarks>Stored as <c>target|assetPath</c> joined by ';'. Rows whose config has since been
        /// deleted or renamed are dropped rather than restored pointing at nothing.</remarks>
        private void LoadMultiTargetState()
        {
            BuildAllTargets = EditorPrefs.GetBool(MultiTargetKey, false);
            Plans = new List<TargetBuildPlan>();

            foreach (string row in EditorPrefs.GetString(MultiTargetPlansKey, "").Split(';'))
            {
                if (string.IsNullOrWhiteSpace(row)) continue;

                string[] parts = row.Split('|');
                if (parts.Length != 2) continue;
                if (!System.Enum.TryParse(parts[0], out BuildTarget target)) continue;
                if (AssetDatabase.LoadAssetAtPath<BuildConfig>(parts[1]) == null) continue;

                Plans.Add(new TargetBuildPlan(target, parts[1]));
            }

            if (Plans.Count == 0) Plans.Add(NewRow());
        }

        private void SaveMultiTargetState()
        {
            EditorPrefs.SetBool(MultiTargetKey, BuildAllTargets);
            EditorPrefs.SetString(
                MultiTargetPlansKey,
                string.Join(";", Plans.Select(p => $"{p.target}|{p.configPath}")));
        }

        private TargetBuildPlan NewRow()
        {
            string configPath = Config != null
                ? AssetDatabase.GetAssetPath(Config)
                : (_allConfigs.Length > 0 ? AssetDatabase.GetAssetPath(_allConfigs[0]) : "");

            // default each new row to a platform not already listed, so adding one rarely needs a second click
            BuildTarget target = SelectableTargets.FirstOrDefault(t => Plans.All(p => p.target != t));
            if (Plans.Any(p => p.target == target) || target == default) target = SelectableTargets[0];

            return new TargetBuildPlan(target, configPath);
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

            EditorGUI.BeginChangeCheck();
            BuildAllTargets = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Build several platforms in one run",
                    "Builds each row below back to back, every one with its own config. " +
                    "Still sequential: Unity has one active build target at a time."),
                BuildAllTargets);
            if (EditorGUI.EndChangeCheck()) SaveMultiTargetState();

            EditorGUILayout.Space();

            if (BuildAllTargets) DrawMultiTarget();
            else DrawSingleTarget();

            GUILayout.FlexibleSpace();
            EditorGUILayout.Space();

            DrawButtons();
        }

        private void DrawSingleTarget()
        {
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
                return;
            }

            if (_cachedEditor == null || _cachedEditor.target != Config) RebuildEditor();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _cachedEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMultiTarget()
        {
            if (_allConfigs.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No BuildConfig found. Turn this off and create one first.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Built top to bottom. The last row's platform is the one the editor is left on.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int removeAt = -1;
            for (int i = 0; i < Plans.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    TargetBuildPlan plan = Plans[i];

                    EditorGUI.BeginChangeCheck();

                    int targetIndex = System.Array.IndexOf(SelectableTargets, plan.target);
                    targetIndex = EditorGUILayout.Popup(
                        Mathf.Max(0, targetIndex), _targetLabels, GUILayout.Width(160));

                    int configIndex = System.Array.FindIndex(
                        _allConfigs, c => AssetDatabase.GetAssetPath(c) == plan.configPath);
                    configIndex = EditorGUILayout.Popup(Mathf.Max(0, configIndex), _allConfigLabels);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Plans[i] = new TargetBuildPlan(
                            SelectableTargets[targetIndex],
                            AssetDatabase.GetAssetPath(_allConfigs[configIndex]));
                        SaveMultiTargetState();
                    }

                    using (new EditorGUI.DisabledScope(Plans.Count <= 1))
                    {
                        if (GUILayout.Button("−", GUILayout.Width(24))) removeAt = i;
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            if (removeAt >= 0)
            {
                Plans.RemoveAt(removeAt);
                SaveMultiTargetState();
            }

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add platform", GUILayout.Height(22)))
                {
                    Plans.Add(NewRow());
                    SaveMultiTargetState();
                }
                if (GUILayout.Button("↻", GUILayout.Width(24), GUILayout.Height(22))) RefreshConfigList();
            }

            DrawMultiTargetWarnings();
        }

        /// <summary>Says up front what the run will do about duplicate platforms and shared output folders.</summary>
        private void DrawMultiTargetWarnings()
        {
            EditorGUILayout.Space();

            List<BuildTarget> duplicateTargets = Plans
                .GroupBy(p => p.target)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateTargets.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{string.Join(", ", duplicateTargets)} is listed more than once. Each row still builds, " +
                    "but the later one wins on itch — both push to the same channel.",
                    MessageType.Warning);
            }

            // resolved the same way the build resolves it, tokens and all: a config using {target}
            // already separates its platforms, and warning about it would be noise.
            List<string> sharedFolders = Plans
                .Select(p => new
                {
                    p.target,
                    config = AssetDatabase.LoadAssetAtPath<BuildConfig>(p.configPath),
                })
                .Where(row => row.config != null)
                .Select(row => BuildConfigurator.ResolveOutputFolder(row.config, row.target))
                .GroupBy(path => path, System.StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (sharedFolders.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"More than one platform builds into '{string.Join("', '", sharedFolders)}'.\n\n" +
                    "Each will be moved into a subfolder of it named after its platform, so they cannot " +
                    "overwrite each other, share a zip name, or drag one another into the wrong itch " +
                    "channel.\n\nPut {target} in the config's output path to choose the layout yourself, " +
                    "e.g. Builds/{target}.",
                    MessageType.Info);
            }
        }

        private void DrawButtons()
        {
            bool canBuild = BuildAllTargets
                ? Plans.Count > 0 && Plans.All(p => !string.IsNullOrEmpty(p.configPath))
                : Config != null;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!canBuild))
                {
                    string label = BuildAllTargets ? $"Build {Plans.Count} platforms" : "Build";
                    if (GUILayout.Button(label, GUILayout.Width(160), GUILayout.Height(28)))
                    {
                        SaveMultiTargetState();
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
