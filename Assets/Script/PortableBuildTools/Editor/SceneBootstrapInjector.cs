using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneTools
{
    /// <summary>
    /// Adds every prefab and component listed in the project's <c>SceneBootstrapConfig</c> assets to each
    /// scene the user opens or creates, filtered by the scene's <see cref="SceneMetaData"/> kind.
    /// </summary>
    /// <remarks>
    /// Invariant: a prefab already in the scene is never duplicated, and a variant of it counts as
    /// present. A component script already on some object in the scene is likewise not re-added.
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
            InjectInto(scene, isNewScene: false);
        }

        private static void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            InjectInto(scene, isNewScene: true);
        }

        private static void InjectInto(Scene scene, bool isNewScene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            List<SceneBootstrapConfig> configs = LoadConfigs();
            if (configs.Count == 0) return;

            SceneKind kind = FindSceneKind(scene, out bool hasMeta);

            bool changed = false;

            foreach (SceneBootstrapConfig config in configs)
            {
                if (config == null) continue;
                if (isNewScene && !config.injectOnNewScene) continue;
                if (IsIgnored(config, scene)) continue;
                if (!Applies(config, kind, hasMeta)) continue;

                changed |= InjectPrefabs(config, scene);
                changed |= InjectComponents(config, scene);
            }

            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        // ---- matching ------------------------------------------------------------------------------

        private static bool IsIgnored(SceneBootstrapConfig config, Scene scene)
        {
            if (config.ignoreSceneNameContains == null) return false;
            foreach (string frag in config.ignoreSceneNameContains)
            {
                if (!string.IsNullOrEmpty(frag) && scene.name.Contains(frag))
                    return true;
            }
            return false;
        }

        /// <summary>Whether a config reaches a scene of this kind.</summary>
        /// <remarks>
        /// A scene with no <see cref="SceneMetaData"/> is only reached by a universal config (one that
        /// targets all three kinds), so an existing scene keeps getting the shared prefabs while
        /// kind-specific rules stay off until someone marks it. A warning is logged once per open so the
        /// missing marker is noticed rather than silently swallowing kind-specific injections.
        /// </remarks>
        private static bool Applies(SceneBootstrapConfig config, SceneKind kind, bool hasMeta)
        {
            if (hasMeta) return (config.sceneKinds & kind) != 0;

            const SceneKind all = SceneKind.Menu | SceneKind.Game | SceneKind.Test;
            return (config.sceneKinds & all) == all;
        }

        private static SceneKind FindSceneKind(Scene scene, out bool hasMeta)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SceneMetaData meta = root.GetComponentInChildren<SceneMetaData>(true);
                if (meta != null)
                {
                    hasMeta = true;
                    return meta.kind;
                }
            }

            hasMeta = false;
            Debug.LogWarning($"[SceneBootstrap] Scene '{scene.name}' has no SceneMetaData, so only universal configs inject into it. Add a SceneMetaData and set its kind to control this.");
            return SceneKind.None;
        }

        // ---- prefabs -------------------------------------------------------------------------------

        private static bool InjectPrefabs(SceneBootstrapConfig config, Scene scene)
        {
            if (config.prefabs == null) return false;

            bool changed = false;
            foreach (GameObject prefab in config.prefabs)
            {
                if (prefab == null) continue;
                if (PrefabPresentInScene(scene, prefab)) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Bootstrap Prefab");
                changed = true;
                Debug.Log($"[SceneBootstrap] Added prefab '{prefab.name}' to scene '{scene.name}'.", instance);
            }
            return changed;
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

        // ---- components ----------------------------------------------------------------------------

        private static bool InjectComponents(SceneBootstrapConfig config, Scene scene)
        {
            if (config.componentScripts == null) return false;

            bool changed = false;
            foreach (MonoScript script in config.componentScripts)
            {
                if (script == null) continue;

                Type type = script.GetClass();
                if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    Debug.LogWarning($"[SceneBootstrap] '{script.name}' is not a MonoBehaviour script; skipped.", script);
                    continue;
                }

                if (ComponentPresentInScene(scene, type)) continue;

                var go = new GameObject(type.Name);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Bootstrap Component");
                Undo.AddComponent(go, type);
                changed = true;
                Debug.Log($"[SceneBootstrap] Added component '{type.Name}' to scene '{scene.name}'.", go);
            }
            return changed;
        }

        private static bool ComponentPresentInScene(Scene scene, Type type)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren(type, true) != null)
                    return true;
            }
            return false;
        }

        // ---- config loading ------------------------------------------------------------------------

        private static List<SceneBootstrapConfig> LoadConfigs()
        {
            var configs = new List<SceneBootstrapConfig>();
            foreach (string guid in AssetDatabase.FindAssets("t:SceneBootstrapConfig"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<SceneBootstrapConfig>(path);
                if (config != null) configs.Add(config);
            }
            return configs;
        }
    }
}
