using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace BuildTools
{
    /// <summary>Decides whether post-build steps run for a given build result.</summary>
    internal static class BuildResultGate
    {
        /// <summary>Reports whether the build got far enough for post-build steps to make sense.</summary>
        /// <param name="result">Post-build callbacks see <c>Unknown</c> even on success, since the
        /// report is not final yet.</param>
        /// <returns><c>false</c> only for <c>Failed</c> and <c>Cancelled</c>; <c>Unknown</c> counts
        /// as success.</returns>
        public static bool ShouldProceed(BuildResult result)
        {
            return result != BuildResult.Failed && result != BuildResult.Cancelled;
        }
    }

    /// <summary>Zips the finished build folder into a date-stamped archive.</summary>
    /// <remarks>
    /// Invariant: a missing build folder or a zip failure logs and leaves the build itself intact.
    /// Invariant: an archive of the same name is replaced, so re-building the same day overwrites.
    /// </remarks>
    public class ZipPostprocess : IPostprocessBuildWithReport
    {
        private const string DefaultZipSubfolder = "zips";

        public int callbackOrder => 1;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!EditorPrefs.GetBool(BuildConfigurator.ZipEnabledKey, false)) return;

            if (!BuildResultGate.ShouldProceed(report.summary.result))
            {
                Debug.Log($"[ZipPostprocess] Build result {report.summary.result}; skipping zip.");
                return;
            }

            string outputPath = report.summary.outputPath;
            string buildFolder = ResolveBuildFolder(outputPath);
            if (string.IsNullOrEmpty(buildFolder) || !Directory.Exists(buildFolder))
            {
                Debug.LogWarning($"[ZipPostprocess] Build folder not found ({buildFolder}). Skipping zip.");
                return;
            }

            string zipFolder = EditorPrefs.GetString(BuildConfigurator.ZipPathKey, "");
            if (string.IsNullOrWhiteSpace(zipFolder))
            {
                string parent = Path.GetDirectoryName(buildFolder);
                zipFolder = string.IsNullOrEmpty(parent)
                    ? Path.Combine(buildFolder, DefaultZipSubfolder)
                    : Path.Combine(parent, DefaultZipSubfolder);
            }
            Directory.CreateDirectory(zipFolder);

            string buildName = Path.GetFileNameWithoutExtension(outputPath);
            if (string.IsNullOrEmpty(buildName)) buildName = new DirectoryInfo(buildFolder).Name;
            string zipPath = Path.Combine(zipFolder, $"{buildName}_{DateTime.Now:dd-MM-yyyy}.zip");

            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(buildFolder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: true);
                Debug.Log($"[ZipPostprocess] Zipped build to: {zipPath}");
                EditorUtility.RevealInFinder(zipPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ZipPostprocess] Zip failed: {e.Message}");
            }
        }

        // Windows reports an .exe path; other platforms report the folder.
        private static string ResolveBuildFolder(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return outputPath;
            if (outputPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(outputPath);
            return outputPath;
        }
    }

    /// <summary>Publishes the finished build to an itch.io page with butler.</summary>
    /// <remarks>
    /// Invariant: a missing butler, blank user or game, or unmapped platform logs a warning and
    /// skips the upload rather than failing the build.
    /// Invariant: an upload past its timeout is killed and logged as an error.
    /// Invariant: runs after the zip step, so the archive exists by then.
    /// </remarks>
    public class ItchUploader : IPostprocessBuildWithReport
    {
        public int callbackOrder => 10;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!EditorPrefs.GetBool(BuildConfigurator.UploadToItchKey, false)) return;

            if (!BuildResultGate.ShouldProceed(report.summary.result))
            {
                Debug.LogWarning($"[ItchUploader] Build result {report.summary.result}; skipping upload.");
                return;
            }

            string butler = EditorPrefs.GetString(BuildConfigurator.ButlerPathKey, "").Trim();
            if (string.IsNullOrEmpty(butler)) butler = "butler"; // resolved on PATH
            if (butler != "butler" && !File.Exists(butler))
            {
                Debug.LogWarning($"[ItchUploader] butler not found at '{butler}'. Set the path in BuildConfig or install from https://itch.io/docs/butler/. Skipping.");
                return;
            }

            string user = EditorPrefs.GetString(BuildConfigurator.ItchUserKey, "").Trim();
            string game = EditorPrefs.GetString(BuildConfigurator.ItchGameKey, "").Trim();
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(game))
            {
                Debug.LogWarning("[ItchUploader] itch user/game not set (paste the game URL in BuildConfig). Skipping upload.");
                return;
            }

            string channel = ChannelFor(report.summary.platform);
            if (channel == null)
            {
                Debug.LogWarning($"[ItchUploader] No itch channel mapping for {report.summary.platform}. Skipping upload.");
                return;
            }

            string buildFolder = ResolveBuildFolder(report.summary.outputPath);
            if (!Directory.Exists(buildFolder))
            {
                Debug.LogError($"[ItchUploader] Build folder does not exist: {buildFolder}. Skipping upload.");
                return;
            }
            buildFolder = Path.GetFullPath(buildFolder);

            int timeoutMs = EditorPrefs.GetInt(BuildConfigurator.ItchTimeoutKey, 300) * 1000;
            string target = $"{user}/{game}:{channel}";
            Debug.Log($"[ItchUploader] Pushing {buildFolder} → {target}");
            RunButlerPush(butler, buildFolder, target, timeoutMs);
        }

        private static string ChannelFor(BuildTarget platform)
        {
            switch (platform)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "win-64";
                case BuildTarget.StandaloneOSX:
                    return "osx";
                case BuildTarget.StandaloneLinux64:
                    return "linux-64";
                case BuildTarget.WebGL:
                    return "html5";
                default:
                    return null;
            }
        }

        private static string ResolveBuildFolder(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return outputPath;
            if (outputPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(outputPath);
            return outputPath;
        }

        // ArgumentList keeps paths with spaces intact without hand-written quoting.
        private static void RunButlerPush(string butler, string folder, string target, int timeoutMs)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = butler,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("push");
            psi.ArgumentList.Add(folder);
            psi.ArgumentList.Add(target);

            try
            {
                using Process proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[butler] {e.Data}"); };
                proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogError($"[butler] {e.Data}"); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (!proc.WaitForExit(timeoutMs))
                {
                    try { proc.Kill(); } catch { /* ignore */ }
                    Debug.LogError($"[ItchUploader] butler push timed out after {timeoutMs / 1000}s. Killed.");
                    return;
                }

                if (proc.ExitCode != 0)
                    Debug.LogError($"[ItchUploader] butler push exited {proc.ExitCode}. See log above.");
                else
                    Debug.Log($"[ItchUploader] butler push succeeded: {target}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItchUploader] Failed to launch butler ('{butler}'): {ex.Message}");
            }
        }
    }
}
