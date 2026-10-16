using System.Collections.Generic;
using System.Text;
using ShadowPath.Camera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowPath.Editor.Occlusion
{
    public static class OccluderSetupUtility
    {
        private const string OccluderLayerName = "Occluder";
        private const string DitherShaderName = "ShadowPath/Foreground Occlusion Dither Lit";
        private const string SetupMenuPath = "Tools/ShadowPath/Occlusion/Setup Selected As Occluders";
        private const string RemoveMenuPath = "Tools/ShadowPath/Occlusion/Remove Setup From Selected";

        [MenuItem(SetupMenuPath)]
        private static void SetupSelectedAsOccluders()
        {
            int occluderLayer = LayerMask.NameToLayer(OccluderLayerName);
            if (occluderLayer < 0)
            {
                Debug.LogError(
                    $"Occluder setup failed: create a User Layer named '{OccluderLayerName}' first.");
                return;
            }

            GameObject[] selection = Selection.gameObjects;
            HashSet<Renderer> processedRenderers = new HashSet<Renderer>();
            StringBuilder unsupportedMaterials = new StringBuilder();
            int sceneRootCount = 0;
            int rendererCount = 0;
            int changedLayerCount = 0;
            int addedComponentCount = 0;
            int existingComponentCount = 0;
            int unsupportedRendererCount = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Selected As Occluders");

            for (int selectionIndex = 0; selectionIndex < selection.Length; selectionIndex++)
            {
                GameObject selectedRoot = selection[selectionIndex];
                if (selectedRoot == null || EditorUtility.IsPersistent(selectedRoot))
                {
                    continue;
                }

                sceneRootCount++;
                Renderer[] childRenderers = selectedRoot.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < childRenderers.Length; rendererIndex++)
                {
                    Renderer targetRenderer = childRenderers[rendererIndex];
                    if (targetRenderer == null || !processedRenderers.Add(targetRenderer))
                    {
                        continue;
                    }

                    rendererCount++;
                    GameObject rendererObject = targetRenderer.gameObject;
                    bool rendererObjectChanged = false;

                    if (rendererObject.layer != occluderLayer)
                    {
                        Undo.RecordObject(rendererObject, "Set Occluder Layer");
                        rendererObject.layer = occluderLayer;
                        changedLayerCount++;
                        rendererObjectChanged = true;
                    }

                    OccludableObject occludable = rendererObject.GetComponent<OccludableObject>();
                    if (occludable == null)
                    {
                        occludable = Undo.AddComponent<OccludableObject>(rendererObject);
                        AssignRenderer(occludable, targetRenderer);
                        addedComponentCount++;
                        rendererObjectChanged = true;
                    }
                    else
                    {
                        existingComponentCount++;
                    }

                    if (rendererObjectChanged)
                    {
                        MarkChanged(rendererObject, occludable);
                    }

                    if (!UsesDitherShader(targetRenderer, out string materialDetails))
                    {
                        unsupportedRendererCount++;
                        unsupportedMaterials
                            .Append("\n  - ")
                            .Append(GetHierarchyPath(rendererObject))
                            .Append(": ")
                            .Append(materialDetails);
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (sceneRootCount == 0)
            {
                Debug.LogWarning("Occluder setup skipped: select one or more GameObjects in the Hierarchy.");
                return;
            }

            StringBuilder report = new StringBuilder();
            report
                .Append("Occluder setup complete. Selected roots: ")
                .Append(sceneRootCount)
                .Append(", Renderers processed: ")
                .Append(rendererCount)
                .Append(", Layers changed: ")
                .Append(changedLayerCount)
                .Append(", OccludableObject added: ")
                .Append(addedComponentCount)
                .Append(", already present: ")
                .Append(existingComponentCount)
                .Append('.');

            if (unsupportedRendererCount > 0)
            {
                report
                    .Append("\nRenderers still requiring a material that uses '")
                    .Append(DitherShaderName)
                    .Append("' (")
                    .Append(unsupportedRendererCount)
                    .Append("):")
                    .Append(unsupportedMaterials);
                Debug.LogWarning(report.ToString());
            }
            else
            {
                report.Append(" All Renderer materials already support Occlusion Dither.");
                Debug.Log(report.ToString());
            }
        }

        [MenuItem(RemoveMenuPath)]
        private static void RemoveSetupFromSelected()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Occluder Setup",
                "Remove OccludableObject components and reset affected GameObjects currently on the "
                    + "Occluder Layer to Default? Materials will not be changed.",
                "Remove and Reset",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            int occluderLayer = LayerMask.NameToLayer(OccluderLayerName);
            GameObject[] selection = Selection.gameObjects;
            HashSet<GameObject> processedObjects = new HashSet<GameObject>();
            HashSet<OccludableObject> processedComponents = new HashSet<OccludableObject>();
            int sceneRootCount = 0;
            int removedComponentCount = 0;
            int resetLayerCount = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Occluder Setup From Selected");

            for (int selectionIndex = 0; selectionIndex < selection.Length; selectionIndex++)
            {
                GameObject selectedRoot = selection[selectionIndex];
                if (selectedRoot == null || EditorUtility.IsPersistent(selectedRoot))
                {
                    continue;
                }

                sceneRootCount++;

                Renderer[] childRenderers = selectedRoot.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < childRenderers.Length; rendererIndex++)
                {
                    Renderer childRenderer = childRenderers[rendererIndex];
                    if (childRenderer == null)
                    {
                        continue;
                    }

                    GameObject rendererObject = childRenderer.gameObject;
                    if (!processedObjects.Add(rendererObject)
                        || occluderLayer < 0
                        || rendererObject.layer != occluderLayer)
                    {
                        continue;
                    }

                    Undo.RecordObject(rendererObject, "Reset Occluder Layer");
                    rendererObject.layer = 0;
                    MarkChanged(rendererObject, null);
                    resetLayerCount++;
                }

                OccludableObject[] childOccludables =
                    selectedRoot.GetComponentsInChildren<OccludableObject>(true);
                for (int componentIndex = 0; componentIndex < childOccludables.Length; componentIndex++)
                {
                    OccludableObject occludable = childOccludables[componentIndex];
                    if (occludable == null || !processedComponents.Add(occludable))
                    {
                        continue;
                    }

                    GameObject componentObject = occludable.gameObject;
                    Undo.DestroyObjectImmediate(occludable);
                    MarkChanged(componentObject, null);
                    removedComponentCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (sceneRootCount == 0)
            {
                Debug.LogWarning("Occluder removal skipped: select one or more GameObjects in the Hierarchy.");
                return;
            }

            Debug.Log(
                "Occluder setup removed. Selected roots: " + sceneRootCount
                + ", OccludableObject removed: " + removedComponentCount
                + ", Layers reset to Default: " + resetLayerCount
                + ". Materials were not changed.");
        }

        [MenuItem(SetupMenuPath, true)]
        [MenuItem(RemoveMenuPath, true)]
        private static bool ValidateSceneSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] != null && !EditorUtility.IsPersistent(selection[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool UsesDitherShader(Renderer targetRenderer, out string materialDetails)
        {
            Material[] materials = targetRenderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                materialDetails = "<No Materials>";
                return false;
            }

            StringBuilder unsupported = null;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material != null
                    && material.shader != null
                    && material.shader.name == DitherShaderName)
                {
                    continue;
                }

                if (unsupported == null)
                {
                    unsupported = new StringBuilder();
                }
                else
                {
                    unsupported.Append(", ");
                }

                unsupported.Append("slot ").Append(materialIndex).Append(" = ");
                if (material == null)
                {
                    unsupported.Append("<Missing Material>");
                }
                else
                {
                    unsupported
                        .Append(material.name)
                        .Append(" [")
                        .Append(material.shader != null ? material.shader.name : "Missing Shader")
                        .Append(']');
                }
            }

            materialDetails = unsupported != null ? unsupported.ToString() : string.Empty;
            return unsupported == null;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            string path = gameObject.name;
            Transform current = gameObject.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return gameObject.scene.name + ":" + path;
        }

        private static void AssignRenderer(OccludableObject occludable, Renderer targetRenderer)
        {
            SerializedObject serializedOccludable = new SerializedObject(occludable);
            SerializedProperty renderersProperty = serializedOccludable.FindProperty("renderers");
            if (renderersProperty == null)
            {
                return;
            }

            renderersProperty.arraySize = 1;
            renderersProperty.GetArrayElementAtIndex(0).objectReferenceValue = targetRenderer;
            serializedOccludable.ApplyModifiedProperties();
        }

        private static void MarkChanged(GameObject gameObject, Object changedObject)
        {
            EditorUtility.SetDirty(gameObject);
            if (changedObject != null)
            {
                EditorUtility.SetDirty(changedObject);
            }

            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
                if (changedObject != null)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(changedObject);
                }
            }

            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
    }
}
