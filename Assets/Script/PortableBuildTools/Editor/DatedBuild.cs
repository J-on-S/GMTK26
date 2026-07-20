using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// One-click build of the active target into a date-stamped folder under a folder the user picks.
    /// </summary>
    /// <remarks>
    /// Invariant: a dated build never uploads anywhere, whatever the last config left enabled.
    /// Invariant: the result is always zipped.
    /// Invariant: an existing folder for today is deleted only after the user confirms.
    /// </remarks>
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

        // Windows needs an exe path inside the folder; other platforms build into the folder itself.
        private static string LocationFor(BuildTarget target, string buildFolder, string buildName)
        {
            if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
                return Path.Combine(buildFolder, $"{buildName}.exe");
            return buildFolder;
        }
    }
}
