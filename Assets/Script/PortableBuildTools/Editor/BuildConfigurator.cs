using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Intercepts the default Build button (File > Build Profiles/Settings > Build) via
    /// BuildPlayerWindow.RegisterBuildPlayerHandler. Pops BuildConfigWindow, applies the
    /// chosen BuildConfig's side effects, then hands modified options to Unity's own
    /// DefaultBuildMethods.BuildPlayer.
    ///
    /// Target comes from whatever Unity has active (respects Build Profiles) — not a config field.
    /// Scripting defines go through BuildPlayerOptions.extraScriptingDefines (per-build, no
    /// PlayerSettings mutation, avoids Unity 6's Standalone-subtarget recompile bug).
    ///
    /// Deployment settings are stashed in EditorPrefs (keyed per product) so the post-build
    /// hooks in BuildPostprocess can read them — postprocess callbacks can't see the config asset.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildConfigurator
    {
        // EditorPrefs are machine-global; namespace them per product so two projects on the
        // same machine don't clobber each other's settings.
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
        /// Apply the config's non-build-time side effects: PlayerPrefs seeds + flag prefs, and
        /// stash deployment settings into EditorPrefs for the post-build hooks.
        /// </summary>
        public static void Apply(BuildConfig cfg)
        {
            // Data-driven PlayerPrefs — no hardcoded game keys.
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

            // Deployment settings → EditorPrefs (read by BuildPostprocess).
            EditorPrefs.SetBool(ZipEnabledKey, cfg.zipAfterBuild);
            EditorPrefs.SetString(ZipPathKey, cfg.zipOutputPath ?? "");

            EditorPrefs.SetBool(UploadToItchKey, cfg.uploadToItch);
            EditorPrefs.SetString(ButlerPathKey, cfg.butlerPath ?? "");
            EditorPrefs.SetInt(ItchTimeoutKey, Mathf.Max(1, cfg.itchTimeoutSeconds));
            EditorPrefs.SetString(ProductNameKey, cfg.ResolveProductName());

            // Parse itch user/game from the pasted page URL.
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
