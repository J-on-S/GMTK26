using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Turns Unity's own Build button into a config-driven build.
    /// </summary>
    /// <remarks>
    /// Invariant: the platform built is whatever Unity has active, so Build Profiles still decide
    /// the target.
    /// Invariant: scripting defines apply to this build only — <c>PlayerSettings</c> is left
    /// untouched, so no project-wide recompile follows.
    /// Invariant: cancelling the window aborts the build instead of falling through to a default one.
    /// </remarks>
    [InitializeOnLoad]
    public static class BuildConfigurator
    {
        // EditorPrefs are machine-global; the product name keeps two projects on one machine
        // from clobbering each other's settings.
        private static string Prefix => "BuildTools." + Application.productName + ".";
        public static string ZipEnabledKey    => Prefix + "ZipEnabled";
        public static string ZipPathKey       => Prefix + "ZipPath";
        public static string UploadToItchKey  => Prefix + "UploadToItch";
        public static string ButlerPathKey    => Prefix + "ButlerPath";
        public static string ItchUserKey      => Prefix + "ItchUser";
        public static string ItchGameKey      => Prefix + "ItchGame";
        public static string ItchTimeoutKey   => Prefix + "ItchTimeoutSec";
        public static string ProductNameKey   => Prefix + "ProductName";

        static BuildConfigurator()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(HandleBuildClicked);
        }

        private static void HandleBuildClicked(BuildPlayerOptions defaults)
        {
            BuildConfigWindow window = BuildConfigWindow.ShowModal();
            if (!window.WasConfirmed)
                throw new BuildPlayerWindow.BuildMethodException("Build cancelled in BuildConfigWindow.");

            BuildConfig cfg = window.Config;
            if (cfg == null)
                throw new BuildPlayerWindow.BuildMethodException("No BuildConfig assigned. Aborting.");

            BuildTarget target = defaults.target;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);

            Apply(cfg);

            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
                throw new BuildPlayerWindow.BuildMethodException("No enabled scenes in Build Settings. Aborting.");

            string locationPath = ResolveLocationPath(cfg, target);
            string[] extraDefines = BuildExtraDefines(cfg);

            BuildPlayerOptions options = defaults;
            options.scenes = scenes;
            options.locationPathName = locationPath;
            options.target = target;
            options.targetGroup = group;
            options.options = cfg.developmentBuild ? BuildOptions.Development : BuildOptions.None;
            options.extraScriptingDefines = extraDefines;

            string definesStr = extraDefines.Length == 0 ? "(none)" : string.Join(";", extraDefines);
            Debug.Log($"[BuildConfigurator] Building {target} → {locationPath} (dev={cfg.developmentBuild}, scenes={scenes.Length}, defines={definesStr})");

            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }

        /// <summary>
        /// Writes the config's PlayerPrefs and hands its deployment settings to the post-build hooks.
        /// </summary>
        /// <remarks>
        /// Invariant: an unparseable itch URL clears the stored user and game, so a misconfigured
        /// upload never pushes to the wrong page; it logs a warning when <c>uploadToItch</c> is on.
        /// </remarks>
        public static void Apply(BuildConfig cfg)
        {
            foreach (ScriptingFlag flag in cfg.scriptingFlags)
            {
                if (!string.IsNullOrWhiteSpace(flag.playerPrefKey))
                    PlayerPrefs.SetInt(flag.playerPrefKey.Trim(), flag.enabled ? 1 : 0);
            }
            foreach (PrefSeed seed in cfg.prefSeeds)
            {
                if (!string.IsNullOrWhiteSpace(seed.key))
                    PlayerPrefs.SetInt(seed.key.Trim(), seed.value);
            }
            PlayerPrefs.Save();

            EditorPrefs.SetBool(ZipEnabledKey, cfg.zipAfterBuild);
            EditorPrefs.SetString(ZipPathKey, cfg.zipOutputPath ?? "");

            EditorPrefs.SetBool(UploadToItchKey, cfg.uploadToItch);
            EditorPrefs.SetString(ButlerPathKey, cfg.butlerPath ?? "");
            EditorPrefs.SetInt(ItchTimeoutKey, Mathf.Max(1, cfg.itchTimeoutSeconds));
            EditorPrefs.SetString(ProductNameKey, cfg.ResolveProductName());

            if (ItchUrl.TryParse(cfg.itchGameUrl, out string user, out string game))
            {
                EditorPrefs.SetString(ItchUserKey, user);
                EditorPrefs.SetString(ItchGameKey, game);
            }
            else
            {
                EditorPrefs.SetString(ItchUserKey, "");
                EditorPrefs.SetString(ItchGameKey, "");
                if (cfg.uploadToItch)
                    Debug.LogWarning($"[BuildConfigurator] Could not parse itch URL '{cfg.itchGameUrl}'. Expected e.g. https://user.itch.io/game.");
            }
        }

        private static string[] BuildExtraDefines(BuildConfig cfg)
        {
            List<string> defines = new List<string>();
            foreach (ScriptingFlag flag in cfg.scriptingFlags)
            {
                if (flag.enabled && !string.IsNullOrWhiteSpace(flag.define))
                    defines.Add(flag.define.Trim());
            }
            return defines.ToArray();
        }

        private static string[] GetEnabledScenes()
        {
            List<string> scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) scenes.Add(scene.path);
            }
            return scenes.ToArray();
        }

        private static string ResolveLocationPath(BuildConfig cfg, BuildTarget target)
        {
            string product = cfg.ResolveProductName();
            string folder = string.IsNullOrEmpty(cfg.outputPath) ? $"Builds/{product}" : cfg.outputPath;

            if (!Path.IsPathRooted(folder))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                folder = Path.Combine(projectRoot, folder);
            }
            Directory.CreateDirectory(folder);

            // Windows wants an .exe path; other platforms want the folder itself.
            if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
            {
                string exeName = string.IsNullOrEmpty(product) ? "Game" : product;
                return Path.Combine(folder, exeName + ".exe");
            }
            return folder;
        }
    }
}
