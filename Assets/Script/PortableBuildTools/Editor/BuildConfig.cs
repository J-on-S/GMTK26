using System.Collections.Generic;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Project-agnostic build settings asset. Create via
    /// Assets > Create > Build Tools > Build Config, keep it inside an Editor/ folder.
    ///
    /// Nothing here is hardcoded to a specific game: debug flags and progress seeds are
    /// data-driven lists, and every filesystem path can be picked from the file explorer
    /// (see BuildConfigEditor's Browse buttons). The itch target is set by pasting the
    /// game's page URL — user + game slug are parsed from it.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildConfig", menuName = "Build Tools/Build Config", order = 0)]
    public class BuildConfig : ScriptableObject
    {
        // ---------- Output ----------
        [Header("Output")]
        [Tooltip("Blank = use Player Settings product name. Used for the exe name and default folders.")]
        public string productNameOverride = "";

        [Tooltip("Build folder. Relative paths resolve against the project root. Use Browse to pick.")]
        public string outputPath = "Builds/Game";

        [Tooltip("Build with BuildOptions.Development.")]
        public bool developmentBuild = false;

        // ---------- Scripting flags ----------
        [Header("Scripting Flags")]
        [Tooltip("Per-build scripting defines. Each enabled entry adds its define; if a PlayerPref key " +
                 "is given, that pref is also set to 1/0 so runtime code can read it in the editor too.")]
        public List<ScriptingFlag> scriptingFlags = new List<ScriptingFlag>();

        // ---------- PlayerPrefs seeds ----------
        [Header("PlayerPrefs Seeds")]
        [Tooltip("Arbitrary PlayerPrefs (int) written before the build. Use for progress/save seeding.")]
        public List<PrefSeed> prefSeeds = new List<PrefSeed>();

        // ---------- Zip ----------
        [Header("Zip")]
        [Tooltip("Zip the build folder after a successful build.")]
        public bool zipAfterBuild = true;

        [Tooltip("Folder the .zip is written to. Blank = a 'zips' folder next to the build. Use Browse to pick.")]
        public string zipOutputPath = "";

        // ---------- itch.io deployment ----------
        [Header("itch.io Deployment")]
        [Tooltip("Push the build to itch.io via butler after a successful build.")]
        public bool uploadToItch = false;

        [Tooltip("Paste the game's itch.io page URL, e.g. https://team7.itch.io/fish-game. " +
                 "User and game slug are parsed from it; channel is derived from the build target.")]
        public string itchGameUrl = "";

        [Tooltip("Path to butler (butler.exe on Windows). Blank = 'butler' resolved on PATH. Use Browse to pick.")]
        public string butlerPath = "";

        [Tooltip("Kill the butler upload if it runs longer than this.")]
        public int itchTimeoutSeconds = 300;

        /// <summary>Product name to use for exe/folder naming.</summary>
        public string ResolveProductName()
        {
            return string.IsNullOrWhiteSpace(productNameOverride)
                ? Application.productName
                : productNameOverride.Trim();
        }
    }

    /// <summary>A scripting define, optionally mirrored to a PlayerPref, toggled per build.</summary>
    [System.Serializable]
    public struct ScriptingFlag
    {
        [Tooltip("Scripting define symbol added for this build, e.g. CHEATS_ENABLED.")]
        public string define;

        [Tooltip("Optional PlayerPref (int) key set to 1/0 to match, e.g. CheatsEnabled. Blank = none.")]
        public string playerPrefKey;

        public bool enabled;
    }

    /// <summary>An int PlayerPref written before the build.</summary>
    [System.Serializable]
    public struct PrefSeed
    {
        public string key;
        public int value;
    }
}
