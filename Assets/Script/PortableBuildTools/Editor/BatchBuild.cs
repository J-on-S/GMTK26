using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Headless entry point: the build the Build button makes, driven by a <see cref="BuildConfig"/>
    /// asset and command-line arguments instead of the modal window.
    /// </summary>
    /// <remarks>
    /// Exists so a build can be made with nobody at the editor -- from a terminal, a CI step, or an
    /// agent. Everything past the player itself is the ordinary post-build pipeline: this only writes
    /// the EditorPrefs those hooks read, through <see cref="BuildConfigurator.Apply"/>, so a headless
    /// build zips and uploads exactly as a button build does.
    /// <para>Invariant: nothing here opens a panel or a dialog. In batch mode those hang the build or
    /// hand back an empty string, which is why <see cref="DatedBuild"/> cannot be run this way -- it
    /// asks for its output folder with <c>OpenFolderPanel</c>.</para>
    /// <para>Invariant: the exit code is the build result. <c>-quit</c> alone always exits 0, so a
    /// failed build would read as a success to whatever ran it; this calls <c>EditorApplication.Exit</c>
    /// itself instead.</para>
    /// <para>Unity must not already have the project open: the editor holds a lock on it and a second
    /// instance refuses to start. That is a Unity constraint, not something this can work around.</para>
    /// <example>
    /// Unity.exe -batchmode -nographics -projectPath C:\Bureau\GMTK26
    ///   -executeMethod BuildTools.BatchBuild.BuildFromCommandLine
    ///   -buildConfig Assets/BuildConfig.asset -itchUpload true -logFile -
    /// </example>
    /// </remarks>
    public static class BatchBuild
    {
        /// <summary>Builds the active target, then exits with 0 on success and 1 on anything else.</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Run();
            }
            catch (Exception e)
            {
                // an exception out of -executeMethod otherwise leaves the editor running forever in
                // batch mode, holding the project lock, with no build and no exit code.
                Fail($"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void Run()
        {
            BuildConfig cfg = LoadConfig(Arg("-buildConfig"));
            if (cfg == null)
            {
                Fail("No BuildConfig found. Pass -buildConfig <assetPath>, or keep one anywhere in Assets/.");
                return;
            }

            // writes the zip/itch settings the post-build hooks read, and the config's PlayerPrefs
            BuildConfigurator.Apply(cfg);
            ApplyOverrides();

            string[] scenes = BuildConfigurator.GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings.");
                return;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string location = ResolveLocation(cfg, target);
            string[] defines = BuildConfigurator.BuildExtraDefines(cfg);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = cfg.developmentBuild ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = defines,
            };

            bool uploading = EditorPrefs.GetBool(BuildConfigurator.UploadToItchKey, false);
            Debug.Log($"[BatchBuild] Building {target} → {location} " +
                      $"(config={cfg.name}, dev={cfg.developmentBuild}, scenes={scenes.Length}, " +
                      $"defines={(defines.Length == 0 ? "(none)" : string.Join(";", defines))}, " +
                      $"zip={EditorPrefs.GetBool(BuildConfigurator.ZipEnabledKey, false)}, itch={uploading})");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            // the zip and itch hooks have already run by here: they are post-build callbacks, and
            // BuildPlayer does not return until they are done.
            Debug.Log($"[BatchBuild] {summary.result} in {summary.totalTime}, " +
                      $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors.");

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build result was {summary.result}.");
                return;
            }

            Done();
        }

        /// <summary>Command-line overrides for the two settings a caller most often wants to differ from the asset.</summary>
        /// <remarks>Written onto the EditorPrefs rather than onto the config: the asset is checked in,
        /// and a headless run that edited it would leave a modified file behind for the next person.</remarks>
        private static void ApplyOverrides()
        {
            string upload = Arg("-itchUpload");
            if (upload != null)
            {
                bool on = ParseBool(upload);
                EditorPrefs.SetBool(BuildConfigurator.UploadToItchKey, on);
                Debug.Log($"[BatchBuild] itch upload overridden to {on}.");
            }

            string zip = Arg("-zip");
            if (zip != null)
            {
                bool on = ParseBool(zip);
                EditorPrefs.SetBool(BuildConfigurator.ZipEnabledKey, on);
                Debug.Log($"[BatchBuild] zip overridden to {on}.");
            }
        }

        /// <summary>Where the player goes: <c>-buildOutput</c> when given, otherwise whatever the config resolves to.</summary>
        private static string ResolveLocation(BuildConfig cfg, BuildTarget target)
        {
            string folder = Arg("-buildOutput");
            if (string.IsNullOrWhiteSpace(folder))
            {
                return BuildConfigurator.ResolveLocationPath(cfg, target);
            }

            folder = Path.GetFullPath(folder);
            Directory.CreateDirectory(folder);

            // Windows wants an exe path inside the folder; every other target wants the folder.
            if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
            {
                string product = cfg.ResolveProductName();
                return Path.Combine(folder, (string.IsNullOrEmpty(product) ? "Game" : product) + ".exe");
            }
            return folder;
        }

        /// <summary>The config at the given asset path, or the only one in the project when no path is given.</summary>
        /// <remarks>Falling back to a search rather than a hardcoded path: the asset moves between
        /// projects that copy these tools, and a caller should not have to know where it landed. Several
        /// found with no path given is an error, not a guess -- picking one would build the wrong config.</remarks>
        private static BuildConfig LoadConfig(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                var explicitly = AssetDatabase.LoadAssetAtPath<BuildConfig>(assetPath);
                if (explicitly == null) Debug.LogError($"[BatchBuild] No BuildConfig at '{assetPath}'.");
                return explicitly;
            }

            string[] guids = AssetDatabase.FindAssets("t:BuildConfig");
            if (guids.Length == 0) return null;

            if (guids.Length > 1)
            {
                Debug.LogError($"[BatchBuild] {guids.Length} BuildConfig assets in the project; " +
                               "pass -buildConfig <assetPath> to say which one.");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<BuildConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>The value after <paramref name="name"/> on the command line, or null when it is absent or last.</summary>
        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static bool ParseBool(string value)
        {
            value = value.Trim();
            return value.Equals("1") || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[BatchBuild] {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void Done()
        {
            Debug.Log("[BatchBuild] Done.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
