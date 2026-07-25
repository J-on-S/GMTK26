using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Tracks which edit-mode preview owns the scene camera, so no two ever write the same transform.</summary>
/// <remarks>
/// Invariant: at most one holder at a time — claiming releases whoever had it.
/// <para>Invariant: the holder is released before a recompile, a play-mode change or a scene close,
/// so the camera is never stranded with the snapshot that would restore it already gone.</para>
/// </remarks>
[InitializeOnLoad]
public static class EditorCameraClaim
{
    /// <summary>What puts the camera back, or <c>null</c> when nobody holds it.</summary>
    private static Action release;

    /// <summary>Identifies the current owner, so a preview can ask whether the claim is still its own.</summary>
    public static object Holder { get; private set; }

    public static bool IsClaimed => Holder != null;

    static EditorCameraClaim()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Release;

        // entering play mode still claimed would start the game from the preview pose
        EditorApplication.playModeStateChanged += _ => Release();

        EditorSceneManager.sceneClosed += _ => Release();
    }

    /// <summary>What the current holder is called, for inspector messages.</summary>
    private static string holderLabel;

    /// <summary>Takes the camera for <paramref name="owner"/>, releasing whoever had it.</summary>
    /// <param name="owner">Identity token, compared by reference; must outlive the thing being previewed, or the claim can never be matched on release.</param>
    /// <param name="onRelease">What puts the camera back.</param>
    /// <param name="label">Name shown to the user, read now because the owner may be gone by the time it is asked for.</param>
    public static void Claim(object owner, Action onRelease, string label = null)
    {
        Release();

        Holder = owner;
        release = onRelease;
        holderLabel = label;
    }

    /// <summary>Gives the camera back, doing nothing when nobody holds it.</summary>
    public static void Release()
    {
        Action onRelease = release;

        // cleared first: the callback stops a preview, which calls back into here
        release = null;
        Holder = null;
        holderLabel = null;

        onRelease?.Invoke();
    }

    /// <summary>Gives the camera back only when <paramref name="owner"/> is the one holding it.</summary>
    public static void ReleaseIfHeldBy(object owner)
    {
        if (Holder == owner)
        {
            Release();
        }
    }

    /// <summary>Name of whatever holds the camera, empty when nobody does.</summary>
    public static string HolderName() => holderLabel ?? string.Empty;
}
