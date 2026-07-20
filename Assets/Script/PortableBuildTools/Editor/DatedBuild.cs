using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Menu command: build the active target into parent/build_dd-MM-yyyy/. Independent of
    /// BuildConfig — a quick one-click dated build. Zipping is handled by ZipPostprocess
    /// (this command enables it and disables itch upload for the duration, so a dated build
    /// never deploys by accident).
    ///
    /// Uses whatever build target is active in Build Settings/Profiles, not a hardcoded platform.
    /// </summary>
    public static class DatedBuild
    {
        private static string LastParentDirKey => "BuildTools." + Application.productName + ".DatedBuild.LastParentDir";

        [MenuItem("Tools/Build/Dated Build (active target)")]
        public static void BuildDated()
        {
            string parentDir = EditorUtility.OpenFolderPanel(
                "Select parent folder for the dated build",
                EditorPrefs.GetString(LastParentDirKey, ""),
                "");

            if (string.IsNullOrEmpty(parentDir))
            {
                Debug.Log("[DatedBuild] Cancelled: no parent folder selected.");
                return;
            }
            EditorPrefs.SetString(LastParentDirKey, parentDir);

            string buildName = $"build_{DateTime.Now:dd-MM-yyyy}";
            string buildFolder = Path.Combine(parentDir, buildName);

            if (Directory.Exists(buildFolder))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Build folder already exists",
                    $"\"{buildFolder}\" already exists.\n\nOverwrite it?", "Overwrite", "Cancel");
                if (!overwrite)
                {
                    Debug.Log("[DatedBuild] Cancelled: user declined overwrite.");
                    return;
                }
                Directory.Delete(buildFolder, true);
            }
            Directory.CreateDirectory(buildFolder);

            List<string> scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);

            if (scenes.Count == 0)
            {
                Debug.LogError("[DatedBuild] No enabled scenes in Build Settings. Aborting.");
                return;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            string location = LocationFor(target, buildFolder, buildName);

            // Route zipping through ZipPostprocess; make sure a dated build never uploads.
            EditorPrefs.SetBool(BuildConfigurator.ZipEnabledKey, true);
            EditorPrefs.SetString(BuildConfigurator.ZipPathKey, ""); // → parent/zips
            EditorPrefs.SetBool(BuildConfigurator.UploadToItchKey, false);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = location,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };

            Debug.Log($"[DatedBuild] Building {target} → {location}");
            BuildPipeline.BuildPlayer(options);
        }

        // Windows needs an exe path inside the folder; other platforms build into the folder.
        private static string LocationFor(BuildTarget target, string buildFolder, string buildName)
        {
            if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
                return Path.Combine(buildFolder, $"{buildName}.exe");
            return buildFolder;
        }
    }
}
