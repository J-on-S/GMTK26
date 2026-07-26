#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MissingReferenceFinder : EditorWindow {
    [MenuItem("Tools/Missing References Finder")]
    public static void ShowWindow() {
        GetWindow<MissingReferenceFinder>("Missing References Finder");
    }

    private void OnGUI() {
        if (GUILayout.Button("Find Missing References in Scene")) {
            FindMissingReferences();
        }
    }

    private void FindMissingReferences() {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int missingCount = 0;

        foreach (GameObject go in allObjects) {
            Component[] components = go.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++) {
                Component c = components[i];

                if (c == null) {
                    Debug.LogWarning($"Missing script on GameObject: {GetFullPath(go)}", go);
                    missingCount++;
                    continue;
                }

                SerializedObject so = new SerializedObject(c);
                SerializedProperty prop = so.GetIterator();

                while (prop.NextVisible(true)) {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0) {
                        Debug.LogWarning($"Missing reference in {c.GetType().Name} on GameObject: {GetFullPath(go)} → Field: {prop.displayName}", go);
                        missingCount++;
                    }
                }
            }
        }

        if (missingCount == 0)
            Debug.Log("No missing references found!");
        else
            Debug.Log($"Finished: {missingCount} missing references found.");
    }

    private string GetFullPath(GameObject obj) {
        return obj.transform.parent == null ? obj.name : GetFullPath(obj.transform.parent.gameObject) + "/" + obj.name;
    }
}
#endif
