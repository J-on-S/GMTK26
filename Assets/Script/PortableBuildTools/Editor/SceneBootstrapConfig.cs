using System.Collections.Generic;
using UnityEngine;

namespace SceneTools
{
    /// <summary>
    /// Editor-only settings asset listing prefabs that must exist in every scene.
    /// Create via Assets > Create > Scene Tools > Scene Bootstrap Config, keep it in an
    /// Editor/ folder, then drag the prefabs into the list. SceneBootstrapInjector reads it
    /// whenever a scene is opened or created and adds any missing prefab instance.
    ///
    /// Discovered by type (first asset found), so keep exactly one.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneBootstrapConfig", menuName = "Scene Tools/Scene Bootstrap Config", order = 0)]
    public class SceneBootstrapConfig : ScriptableObject
    {
        [Tooltip("Prefabs guaranteed to be present in every scene that is opened or created.")]
        public List<GameObject> prefabs = new List<GameObject>();

        [Tooltip("Also inject into freshly created (empty/default) scenes, not just opened ones.")]
        public bool injectOnNewScene = true;

        [Tooltip("Scene name substrings to skip entirely (e.g. \"Demo\", \"Test\").")]
        public List<string> ignoreSceneNameContains = new List<string>();
    }
}
