using ShadowPath.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowPath.Editor.Tutorial
{
    public static class LightControlTutorialSetup
    {
        [MenuItem("CONTEXT/LightControlTutorialTrigger/Create HUD Prompt")]
        private static void CreateHudPrompt(MenuCommand command)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Create the tutorial HUD prompt outside Play Mode.");
                return;
            }
            var trigger = command.context as LightControlTutorialTrigger;
            if (trigger == null) return;
            var serialized = new SerializedObject(trigger);
            var promptProperty = serialized.FindProperty("tutorialPrompt");
            if (promptProperty.objectReferenceValue != null)
            {
                Selection.activeObject = promptProperty.objectReferenceValue;
                return;
            }
            var canvas = serialized.FindProperty("promptCanvas").objectReferenceValue as Canvas;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace
                || canvas.gameObject.scene != trigger.gameObject.scene)
            {
                Debug.LogWarning("Assign this scene's normal HUD Canvas to Prompt Canvas first.", trigger);
                return;
            }

            var prompt = new GameObject("LightControlPrompt", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(prompt, "Create Light Control Prompt");
            prompt.transform.SetParent(canvas.transform, false);
            prompt.layer = canvas.gameObject.layer;
            var rect = (RectTransform)prompt.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 180f);
            rect.sizeDelta = new Vector2(560f, 100f);

            var label = prompt.AddComponent<TextMeshProUGUI>();
            // Reuse the existing HUD font, including inactive interaction hints.
            foreach (TMP_Text existing in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (existing == label || existing.font == null) continue;
                label.font = existing.font;
                break;
            }
            label.fontSize = 30f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            prompt.AddComponent<LightControlPrompt>();
            prompt.SetActive(false);

            promptProperty.objectReferenceValue = prompt;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(trigger.gameObject.scene);
            Selection.activeGameObject = prompt;
        }
    }
}
