using System.Collections.Generic;
using UnityEngine;

namespace SceneTools
{
    /// <summary>
    /// Lists the prefabs that must exist in every scene, plus the scenes exempt from that rule.
    /// </summary>
    /// <remarks>
    /// Invariant: the asset only deserializes from inside an <c>Editor/</c> folder — it is an
    /// editor-only type.
    /// Invariant: only the first asset of this type in the project takes effect, so keep exactly one.
    /// </remarks>
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
