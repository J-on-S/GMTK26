using UnityEngine;

/// <summary>What kind of scene this is. A single scene is one kind; the flags shape lets tools and the
/// bootstrap config target several kinds at once with a mask.</summary>
[System.Flags]
public enum SceneKind
{
    None = 0,
    Menu = 1 << 0,
    Game = 1 << 1,
    Test = 1 << 2,
}

/// <summary>Marks a scene with its kind (Menu / Game / Test) so tools and the scene-bootstrap config can
/// treat scenes differently without hard-coding scene names.</summary>
/// <remarks>
/// One per scene. Runtime type (not editor-only) so game code can also ask what kind of scene it is in.
/// The bootstrap config reads this to decide which prefabs/components a scene should receive.
/// </remarks>
public class SceneMetaData : MonoBehaviour
{
    [Tooltip("What kind of scene this is. Drives which bootstrap prefabs/components get injected and which audit tools include it.")]
    public SceneKind kind = SceneKind.Game;

    /// <summary>The kind of the scene the given component lives in, or <see cref="SceneKind.None"/> when its scene has no <see cref="SceneMetaData"/>.</summary>
    public static SceneKind KindOf(GameObject inScene)
    {
        if (inScene == null) return SceneKind.None;
        SceneMetaData meta = FindMetaInScene(inScene);
        return meta != null ? meta.kind : SceneKind.None;
    }

    private static SceneMetaData FindMetaInScene(GameObject inScene)
    {
        UnityEngine.SceneManagement.Scene scene = inScene.scene;
        if (!scene.IsValid()) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            SceneMetaData meta = root.GetComponentInChildren<SceneMetaData>(true);
            if (meta != null) return meta;
        }
        return null;
    }
}
