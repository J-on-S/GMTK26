using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneTools
{
    /// <summary>
    /// Lists the prefabs and components that must exist in every scene of the kinds this config targets,
    /// plus the scenes exempt from that rule.
    /// </summary>
    /// <remarks>
    /// Invariant: the asset only deserializes from inside an <c>Editor/</c> folder — it is an
    /// editor-only type.
    /// <para>Every config asset in the project takes effect, not just the first: split rules across
    /// several assets (a universal one, a Game-only one, a Test-only one) and the injector merges them.
    /// Which scenes an asset reaches is decided by <see cref="sceneKinds"/>, matched against the scene's
    /// own <see cref="SceneMetaData"/>.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "SceneBootstrapConfig", menuName = "Scene Tools/Scene Bootstrap Config", order = 0)]
    public class SceneBootstrapConfig : ScriptableObject
    {
        [Tooltip("Which scene kinds this config injects into. Matched against the scene's SceneMetaData.kind. A scene with no SceneMetaData is only reached by a config that targets all kinds (a universal config), so kind-specific rules never fire on an unmarked scene.")]
        public SceneKind sceneKinds = SceneKind.Menu | SceneKind.Game | SceneKind.Test;

        [Tooltip("Prefabs guaranteed to be present in every matching scene. Injected as their own objects.")]
        public List<GameObject> prefabs = new List<GameObject>();

        [Tooltip("Component scripts guaranteed to be present in every matching scene. Each is added to its own new GameObject named after the script. A scene already carrying that component is left alone.")]
        public List<MonoScript> componentScripts = new List<MonoScript>();

        [Tooltip("Also inject into freshly created (empty/default) scenes, not just opened ones.")]
        public bool injectOnNewScene = true;

        [Tooltip("Scene name substrings to skip entirely (e.g. \"Demo\", \"Test\").")]
        public List<string> ignoreSceneNameContains = new List<string>();
    }
}
