using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Draws <c>BuildConfig</c> with file-explorer pickers for its path fields and a preview of the
    /// parsed itch URL.
    /// </summary>
    /// <remarks>
    /// Invariant: a picked folder inside the project root is stored as a project-relative path, so
    /// the asset works on another machine.
    /// Invariant: the butler path is always stored absolute.
    /// </remarks>
    [CustomEditor(typeof(BuildConfig)), CanEditMultipleObjects]
    public class BuildConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iter = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = false;

                switch (iter.propertyPath)
                {
                    case "m_Script":
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.PropertyField(iter);
                        break;

                    case nameof(BuildConfig.outputPath):
                        DrawFolderPath(iter, "Select build output folder", relative: true);
                        break;

                    case nameof(BuildConfig.zipOutputPath):
                        DrawFolderPath(iter, "Select zip output folder", relative: true);
                        break;

                    case nameof(BuildConfig.butlerPath):
                        DrawFilePath(iter, "Locate butler executable");
                        break;

                    case nameof(BuildConfig.itchGameUrl):
                        EditorGUILayout.PropertyField(iter, true);
                        DrawItchPreview(iter.stringValue);
                        break;

                    default:
                        EditorGUILayout.PropertyField(iter, true);
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawFolderPath(SerializedProperty prop, string title, bool relative)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(prop);
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                {
                    string picked = EditorUtility.OpenFolderPanel(title, ResolveStartFolder(prop.stringValue), "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        prop.stringValue = relative ? MakeRelativeIfInsideProject(picked) : picked;
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private static void DrawFilePath(SerializedProperty prop, string title)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(prop);
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                {
                    string dir = "";
                    if (!string.IsNullOrEmpty(prop.stringValue) && File.Exists(prop.stringValue))
                        dir = Path.GetDirectoryName(prop.stringValue);

                    // butler has no extension outside Windows.
                    string ext = Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "";
                    string picked = EditorUtility.OpenFilePanel(title, dir, ext);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        prop.stringValue = picked;
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private static void DrawItchPreview(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            EditorGUI.indentLevel++;
            if (ItchUrl.TryParse(url, out string user, out string game))
                EditorGUILayout.HelpBox($"user: {user}\ngame: {game}\n(channel auto-derived from build target)", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Can't parse. Expected e.g. https://team7.itch.io/fish-game", MessageType.Warning);
            EditorGUI.indentLevel--;
        }

        private static string ResolveStartFolder(string current)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(current)) return projectRoot;

            string candidate = Path.IsPathRooted(current) ? current : Path.Combine(projectRoot, current);
            return Directory.Exists(candidate) ? candidate : projectRoot;
        }

        private static string MakeRelativeIfInsideProject(string absolute)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string picked = absolute.Replace('\\', '/');

            if (picked.StartsWith(projectRoot + "/", System.StringComparison.OrdinalIgnoreCase))
                return picked.Substring(projectRoot.Length + 1);
            return picked;
        }
    }
}
