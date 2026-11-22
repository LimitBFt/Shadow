using System;
using ShadowPath.Stars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowPath.Editor.Stars
{
    [CustomEditor(typeof(StarCutoutPanel))]
    public class StarCutoutPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var panel = (StarCutoutPanel)target;
            EditorGUILayout.HelpBox("Dimensions are clamped on rebuild. Minimum border is 0.05. "
                + "The non-trigger BoxCollider spans the whole panel and excludes all contact layers. "
                + "It is for ShadowSystem detection. Exclude its layer from Player ground and respawn query masks. "
                + "HoleCenter is the local origin.", MessageType.Info);
            if (GUILayout.Button("Rebuild Mesh"))
            {
                Undo.RecordObject(panel, "Rebuild Star Cutout Panel");
                panel.RebuildMesh();
                EditorUtility.SetDirty(panel);
                EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            }
            if (GUILayout.Button("Bake Mesh Asset")) Bake(panel);
        }

        private static void Bake(StarCutoutPanel panel)
        {
            string path = EditorUtility.SaveFilePanelInProject("Bake Star Cutout Mesh", "StarCutoutPanel",
                "asset", "Save inside Assets/_Project. Existing files receive a unique name.", "Assets/_Project/Art");
            if (string.IsNullOrEmpty(path)) return;
            if (!path.StartsWith("Assets/_Project/", StringComparison.Ordinal))
            {
                Debug.LogWarning("Save the mesh inside Assets/_Project.", panel);
                return;
            }
            panel.RebuildMesh();
            Mesh baked = Instantiate(panel.GetComponent<MeshFilter>().sharedMesh);
            baked.name = "StarCutoutPanel";
            baked.hideFlags = HideFlags.None;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(baked, path);
            AssetDatabase.SaveAssetIfDirty(baked);
            var settings = new SerializedObject(panel);
            settings.FindProperty("bakedMesh").objectReferenceValue = baked;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            Debug.Log($"Baked Star Cutout Panel mesh to {path}. Disable Preview In Edit Mode to use the baked mesh in edit mode.", panel);
        }
    }
}
