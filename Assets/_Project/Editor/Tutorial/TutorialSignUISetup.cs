using ShadowPath.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ShadowPath.Editor.Tutorial
{
    public static class TutorialSignUISetup
    {
        [MenuItem("GameObject/ShadowPath/Tutorial Sign UI", false, 10)]
        private static void CreateUI()
        {
            if (Application.isPlaying) return;
            Canvas canvas = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace || !canvas.gameObject.scene.IsValid())
            {
                Debug.LogWarning("Select the normal HUD Canvas (UI) before creating Tutorial Sign UI.");
                return;
            }
            TutorialSignUI existingUI = canvas.GetComponentInChildren<TutorialSignUI>(true);
            if (existingUI != null) { Selection.activeGameObject = existingUI.gameObject; return; }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
                if (label.font != null) { font = label.font; break; }

            var owner = new GameObject("TutorialSignUI", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(owner, "Create Tutorial Sign UI");
            SceneManager.MoveGameObjectToScene(owner, canvas.gameObject.scene);
            owner.transform.SetParent(canvas.transform, false);
            owner.layer = canvas.gameObject.layer;
            var ui = owner.AddComponent<TutorialSignUI>();
            var ownerRect = (RectTransform)owner.transform;
            ownerRect.anchorMin = Vector2.zero;
            ownerRect.anchorMax = Vector2.one;
            ownerRect.sizeDelta = Vector2.zero;
            ownerRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI prompt = CreateText("InteractionPrompt", owner.transform, font, 28f);
            var promptRect = (RectTransform)prompt.transform;
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 100f);
            promptRect.sizeDelta = new Vector2(420f, 60f);
            prompt.text = "[X] READ";

            var panel = new GameObject("TutorialPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(owner.transform, false);
            panel.layer = owner.layer;
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = Vector2.one * 0.5f;
            panelRect.sizeDelta = new Vector2(700f, 300f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 0.93f);
            background.raycastTarget = false;

            TextMeshProUGUI title = CreateText("Title", panel.transform, font, 32f);
            ((RectTransform)title.transform).anchoredPosition = new Vector2(0f, 90f);
            ((RectTransform)title.transform).sizeDelta = new Vector2(640f, 60f);
            title.fontStyle = FontStyles.Bold;
            title.text = "TUTORIAL";
            TextMeshProUGUI body = CreateText("Body", panel.transform, font, 28f);
            ((RectTransform)body.transform).anchoredPosition = new Vector2(0f, -20f);
            ((RectTransform)body.transform).sizeDelta = new Vector2(640f, 170f);
            body.text = "Read a sign to show its tutorial.";

            var serialized = new SerializedObject(ui);
            serialized.FindProperty("interactionPrompt").objectReferenceValue = prompt.gameObject;
            serialized.FindProperty("interactionPromptLabel").objectReferenceValue = prompt;
            serialized.FindProperty("tutorialPanel").objectReferenceValue = panel;
            serialized.FindProperty("titleLabel").objectReferenceValue = title;
            serialized.FindProperty("bodyLabel").objectReferenceValue = body;
            serialized.ApplyModifiedProperties();
            prompt.gameObject.SetActive(false);
            panel.SetActive(false);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = owner;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            owner.transform.SetParent(parent, false);
            owner.layer = parent.gameObject.layer;
            var rect = (RectTransform)owner.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI label = owner.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
    }
}
