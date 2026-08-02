using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildTools
{
    /// <summary>One platform of a multi-target run: what to build, and which config to build it with.</summary>
    /// <remarks>Holds the config's <em>asset path</em> rather than the object. Switching the active build
    /// target reloads the asset database and destroys every loaded <see cref="BuildConfig"/> instance, so a
    /// plan that carried the object would be holding a dangling reference by the time its turn came.</remarks>
    [Serializable]
    public struct TargetBuildPlan
    {
        public BuildTarget target;
        public string configPath;

        public TargetBuildPlan(BuildTarget target, string configPath)
        {
            this.target = target;
            this.configPath = configPath;
        }
    }

    /// <summary>What one target of the run did.</summary>
    public struct TargetBuildResult
    {
        public BuildTarget target;
        public bool succeeded;
        public string detail;

        public override string ToString() => $"{target}: {detail}";
    }

    /// <summary>
    /// Builds several platforms back to back in one editor session, each with its own
    /// <see cref="BuildConfig"/>.
    /// </summary>
    /// <remarks>
    /// The one implementation behind both the Build button's multi-target mode and
    /// <see cref="BatchBuild.BuildAllFromCommandLine"/>, so the two cannot drift.
    /// <para>Invariant: sequential, never parallel. <c>activeBuildTarget</c> is a single global and one
    /// editor holds one project lock, so nothing can build two platforms at once. Building both at the
    /// same wall-clock time needs two project copies and two editors.</para>
    /// <para>Invariant: the active build target is left on whichever plan ran last. Order the list so the
    /// platform you want to keep working in comes last.</para>
    /// <para>Invariant: no two plans write into the same folder — see <see cref="ClaimFolder"/>.</para>
    /// </remarks>
    public static class MultiTargetBuild
    {
        /// <summary>Runs every plan in order and reports what each one did. Never throws for a failed build.</summary>
        /// <param name="plans">What to build, in build order.</param>
        /// <param name="afterApply">
        /// Run immediately after each plan's <see cref="BuildConfigurator.Apply"/>, for a caller that
        /// overrides what the config stored. Needed because Apply runs once per plan and rewrites every
        /// deployment EditorPref from the asset -- an override applied only once, before the loop, would be
        /// wiped by the first target's Apply and silently ignored from then on.
        /// </param>
        /// <returns>One result per plan, in the order the plans were given. Empty when there was nothing to build.</returns>
        public static List<TargetBuildResult> Run(
            IReadOnlyList<TargetBuildPlan> plans,
            Action afterApply = null)
        {
            var results = new List<TargetBuildResult>();
            if (plans == null || plans.Count == 0)
            {
                Debug.LogError("[MultiTargetBuild] Nothing to build: no targets given.");
                return results;
            }

            string[] scenes = BuildConfigurator.GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[MultiTargetBuild] No enabled scenes in Build Settings.");
                return results;
            }

            // worked out for the whole run up front, before anything switches target: no two plans may
            // end up writing into the same folder.
            string[] folders = ResolveFolders(plans);

            // only a real multi-platform run renames its archives; one plan lays out and names exactly as
            // the Build button would, so a single-row run is indistinguishable from an ordinary build.
            bool nameZipsPerTarget = plans.Count > 1;

            for (int i = 0; i < plans.Count; i++)
            {
                results.Add(RunOne(plans[i], folders[i], scenes, afterApply, nameZipsPerTarget));
            }

            return results;
        }

        private static TargetBuildResult RunOne(
            TargetBuildPlan plan,
            string folder,
            string[] scenes,
            Action afterApply,
            bool nameZipsPerTarget)
        {
            // switch BEFORE loading the config, and load it fresh for every plan: the switch reloads the
            // asset database, so a config fetched earlier is destroyed by the time the build would read it.
            if (!SwitchTo(plan.target))
            {
                return Failed(plan.target, "could not switch to this target — is its build support module installed?");
            }

            BuildConfig cfg = AssetDatabase.LoadAssetAtPath<BuildConfig>(plan.configPath);
            if (cfg == null)
            {
                return Failed(plan.target, $"no BuildConfig at '{plan.configPath}'");
            }

            // per plan, not once for the run: each target carries its own zip and itch settings, and these
            // are the EditorPrefs the post-build hooks read. The caller's overrides go on top, in that
            // order -- Apply rewrites every one of those prefs from the asset, so an override underneath
            // it would not survive.
            BuildConfigurator.Apply(cfg);
            afterApply?.Invoke();

            string productName = cfg.ResolveProductName();

            // after Apply, which clears it: the zip is otherwise named from the build's output path, and
            // on Windows that is the exe -- so every Windows build of this project is "<product>" whatever
            // folder it went to, and a second platform's archive would replace the first's.
            if (nameZipsPerTarget)
            {
                EditorPrefs.SetString(
                    BuildConfigurator.ZipNameKey,
                    $"{(string.IsNullOrEmpty(productName) ? "Game" : productName)}-{plan.target}");
            }

            Directory.CreateDirectory(folder);
            string location = LocationInFolder(folder, productName, plan.target);
            string[] defines = BuildConfigurator.BuildExtraDefines(cfg);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = plan.target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(plan.target),
                options = cfg.developmentBuild ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = defines,
            };

            Debug.Log($"[MultiTargetBuild] Building {plan.target} → {location} " +
                      $"(config={cfg.name}, dev={cfg.developmentBuild}, scenes={scenes.Length}, " +
                      $"defines={(defines.Length == 0 ? "(none)" : string.Join(";", defines))}, " +
                      $"zip={EditorPrefs.GetBool(BuildConfigurator.ZipEnabledKey, false)}, " +
                      $"itch={EditorPrefs.GetBool(BuildConfigurator.UploadToItchKey, false)})");

            // the zip and itch hooks run inside this call -- they are post-build callbacks, and BuildPlayer
            // does not return until they are done -- so each target is zipped and pushed before the next starts.
            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;

            string detail = $"{summary.result} in {summary.totalTime}, " +
                            $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors";
            Debug.Log($"[MultiTargetBuild] {plan.target}: {detail}");

            return new TargetBuildResult
            {
                target = plan.target,
                succeeded = summary.result == BuildResult.Succeeded,
                detail = detail,
            };
        }

        /// <summary>Where each plan's player goes, worked out for the whole run so no two share a folder.</summary>
        /// <remarks>
        /// Two plans may name the same <c>outputPath</c> -- most often because the same config drives every
        /// platform -- and left alone the second build would land on top of the first. Worse than lost
        /// files: <c>ItchUploader</c> pushes the folder a build landed in and <c>ZipPostprocess</c> names
        /// the zip after it, so one folder for two platforms means a mixed upload and a clobbered zip.
        /// <para>Every plan sharing a folder moves into a subfolder of it, not just the later ones. Moving
        /// only the loser would nest one whole build inside another and leave the layout dependent on row
        /// order; this way the shared folder ends up holding platform subfolders and nothing else.</para>
        /// <para>A run whose paths are already distinct -- a single-platform build, or configs using the
        /// <c>{target}</c> token -- is untouched, and lays out exactly as the Build button would.</para>
        /// <para>Runs before anything switches target: the configs are read here and not held, so none of
        /// them is destroyed by the asset database reload a switch causes.</para>
        /// </remarks>
        private static string[] ResolveFolders(IReadOnlyList<TargetBuildPlan> plans)
        {
            var folders = new string[plans.Count];

            for (int i = 0; i < plans.Count; i++)
            {
                BuildConfig cfg = AssetDatabase.LoadAssetAtPath<BuildConfig>(plans[i].configPath);
                folders[i] = cfg != null
                    ? BuildConfigurator.ResolveOutputFolder(cfg, plans[i].target)
                    : Path.GetFullPath(Path.Combine(
                        Path.GetDirectoryName(Application.dataPath), "Builds", plans[i].target.ToString()));
            }

            // equality is not enough: 'D:/Releases' and 'D:/Releases/web' are different strings, but the
            // first folder CONTAINS the second, so zipping or pushing the first would swallow the second's
            // whole build. Anything that overlaps another plan's folder gets moved.
            for (int i = 0; i < plans.Count; i++)
            {
                if (!OverlapsAnother(folders, i)) continue;

                string shared = folders[i];
                folders[i] = Path.GetFullPath(Path.Combine(shared, plans[i].target.ToString()));
                Debug.LogWarning(
                    $"[MultiTargetBuild] '{shared}' overlaps another target's output folder in this run. " +
                    $"Writing {plans[i].target} to '{folders[i]}' so the platforms cannot overwrite or " +
                    "contain each other. Put {target} in the config's output path to choose the layout yourself.");
            }

            // two rows for the same platform with the same config land on the same subfolder even after
            // that; a suffix is the last resort so a run never silently builds one on top of the other.
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < plans.Count; i++)
            {
                string unique = folders[i];
                for (int n = 2; !taken.Add(unique); n++) unique = $"{folders[i]}-{n}";
                folders[i] = unique;
            }

            return folders;
        }

        /// <summary>Whether the folder at <paramref name="index"/> is the same as, inside, or the parent of any other plan's.</summary>
        private static bool OverlapsAnother(string[] folders, int index)
        {
            for (int other = 0; other < folders.Length; other++)
            {
                if (other == index) continue;
                if (Overlaps(folders[index], folders[other])) return true;
            }
            return false;
        }

        /// <summary>Whether two folders are the same, or one lies inside the other.</summary>
        /// <remarks>Compared with a trailing separator so a shared name prefix is not mistaken for
        /// containment -- 'D:/ReleasesOld' is not inside 'D:/Releases'.</remarks>
        private static bool Overlaps(string a, string b)
        {
            string na = WithTrailingSeparator(a);
            string nb = WithTrailingSeparator(b);
            return na.StartsWith(nb, StringComparison.OrdinalIgnoreCase)
                || nb.StartsWith(na, StringComparison.OrdinalIgnoreCase);
        }

        private static string WithTrailingSeparator(string path)
        {
            string full = Path.GetFullPath(path);
            return full.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? full
                : full + Path.DirectorySeparatorChar;
        }

        /// <summary>What to hand <c>BuildPlayer</c> as the location: an exe inside the folder on Windows, the folder itself otherwise.</summary>
        internal static string LocationInFolder(string folder, string productName, BuildTarget target)
        {
            if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
            {
                return Path.Combine(folder, (string.IsNullOrEmpty(productName) ? "Game" : productName) + ".exe");
            }
            return folder;
        }

        /// <summary>Makes <paramref name="target"/> active, reimporting the project for it.</summary>
        /// <remarks>Blocks until the switch is done, so the build that follows never runs against a
        /// half-imported project. Already being on the target is a no-op and costs nothing.</remarks>
        internal static bool SwitchTo(BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target) return true;

            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            return EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
        }

        /// <summary>Every target that failed, as "Target (why)" strings. Empty when the whole run succeeded.</summary>
        public static List<string> Failures(IReadOnlyList<TargetBuildResult> results)
        {
            var failures = new List<string>();
            foreach (TargetBuildResult result in results)
            {
                if (!result.succeeded) failures.Add($"{result.target} ({result.detail})");
            }
            return failures;
        }

        private static TargetBuildResult Failed(BuildTarget target, string why)
        {
            Debug.LogError($"[MultiTargetBuild] {target}: {why}");
            return new TargetBuildResult { target = target, succeeded = false, detail = why };
        }
    }
}
