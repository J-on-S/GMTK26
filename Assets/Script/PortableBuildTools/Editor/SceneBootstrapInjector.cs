using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneTools
{
    /// <summary>
    /// Adds every prefab listed in <c>SceneBootstrapConfig</c> to each scene the user opens or creates.
    /// </summary>
    /// <remarks>
    /// Invariant: a prefab already in the scene is never duplicated, and a variant of it counts as
    /// present.
    /// Invariant: an injected scene is left dirty, so the addition persists only once the user saves.
    /// Invariant: each addition is undoable in one step.
    /// </remarks>
    [InitializeOnLoad]
    public static class SceneBootstrapInjector
    {
        static SceneBootstrapInjector()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (mode == OpenSceneMode.AdditiveWithoutLoading) return;
            InjectInto(scene);
        }

        private static void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            var config = LoadConfig();
            if (config == null || !config.injectOnNewScene) return;
            InjectInto(scene);
        }

        private static void InjectInto(Scene scene)
        {
            var config = LoadConfig();
            if (config == null || config.prefabs == null) return;
            if (!scene.IsValid() || !scene.isLoaded) return;

            if (config.ignoreSceneNameContains != null)
            {
                foreach (var frag in config.ignoreSceneNameContains)
                {
                    if (!string.IsNullOrEmpty(frag) && scene.name.Contains(frag))
                        return;
                }
            }

            bool changed = false;

            foreach (var prefab in config.prefabs)
            {
                if (prefab == null) continue;
                if (PrefabPresentInScene(scene, prefab)) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Bootstrap Prefab");
                changed = true;
                Debug.Log($"[SceneBootstrap] Added '{prefab.name}' to scene '{scene.name}'.", instance);
            }

            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        private static bool PrefabPresentInScene(Scene scene, GameObject prefab)
        {
            var prefabRoot = GetPrefabSourceRoot(prefab);

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (src == null) continue;

                    if (GetPrefabSourceRoot(src) == prefabRoot)
                        return true;
                }
            }
            return false;
        }

        // Resolve to the outermost prefab asset so variants and nested references compare equal.
        // Objects with no asset path (plain scene objects) are returned unchanged.
        private static GameObject GetPrefabSourceRoot(GameObject prefabOrInstance)
        {
            string path = AssetDatabase.GetAssetPath(prefabOrInstance);
            if (string.IsNullOrEmpty(path)) return prefabOrInstance;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static SceneBootstrapConfig LoadConfig()
        {
            var guids = AssetDatabase.FindAssets("t:SceneBootstrapConfig");
            if (guids.Length == 0) return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<SceneBootstrapConfig>(path);
        }
    }
}
