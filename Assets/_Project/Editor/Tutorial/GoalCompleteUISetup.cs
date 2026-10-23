using ShadowPath.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowPath.Editor.Tutorial
{
    public static class GoalCompleteUISetup
    {
        [MenuItem("GameObject/ShadowPath/Goal Complete UI", false, 10)]
        private static void CreateUI()
        {
            if (Application.isPlaying) return;
            Canvas canvas = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace || !canvas.gameObject.scene.IsValid())
            {
                Debug.LogWarning("Select the normal HUD Canvas (UI) before creating Goal Complete UI.");
                return;
            }
            GoalCompleteUI existingUI = canvas.GetComponentInChildren<GoalCompleteUI>(true);
            if (existingUI != null) { Selection.activeGameObject = existingUI.gameObject; return; }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
                if (label.font != null) { font = label.font; break; }

            var owner = new GameObject("GoalCompleteUI", typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(owner, "Create Goal Complete UI");
            SceneManager.MoveGameObjectToScene(owner, canvas.gameObject.scene);
            owner.transform.SetParent(canvas.transform, false);
            owner.layer = canvas.gameObject.layer;
            Stretch((RectTransform)owner.transform);
            CanvasGroup group = owner.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            GoalCompleteUI ui = owner.AddComponent<GoalCompleteUI>();

            var background = new GameObject("Fullscreen Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(owner.transform, false);
            background.layer = owner.layer;
            Stretch((RectTransform)background.transform);
            Image image = background.GetComponent<Image>();
            image.color = new Color(0.015f, 0.02f, 0.025f, 1f);
            image.raycastTarget = false;

            CreateText(owner.transform, "CAST Title", "CAST", font, 64f, 110f, FontStyles.Bold);
            CreateText(owner.transform, "LEVEL COMPLETE Text", "LEVEL COMPLETE", font, 36f, 0f, FontStyles.Normal);
            CreateText(owner.transform, "A — RESTART Text", "A — RESTART", font, 28f, -110f, FontStyles.Normal);

            var serialized = new SerializedObject(ui);
            serialized.FindProperty("canvasGroup").objectReferenceValue = group;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = owner;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void CreateText(Transform parent, string name, string text, TMP_FontAsset font,
            float size, float verticalPosition, FontStyles style)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            owner.transform.SetParent(parent, false);
            owner.layer = parent.gameObject.layer;
            var rect = (RectTransform)owner.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(-64f, 100f);
            rect.anchoredPosition = new Vector2(0f, verticalPosition);
            TextMeshProUGUI label = owner.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
            label.color = Color.white;
            label.raycastTarget = false;
        }
    }
}
