using ShadowPath.Stars;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowPath.Editor.Stars
{
    public static class StarHUDSetup
    {
        [MenuItem("GameObject/ShadowPath/Star HUD", false, 10)]
        private static void CreateHUD()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Canvas canvas = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace
                || !canvas.gameObject.scene.IsValid() || EditorUtility.IsPersistent(canvas)
                || PrefabStageUtility.GetPrefabStage(canvas.gameObject) != null)
            {
                Debug.LogWarning("Select the screen-space UI Canvas in the scene before creating Star HUD.");
                return;
            }

            StarHUD existing = canvas.GetComponentInChildren<StarHUD>(true);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.font == null) continue;
                font = label.font;
                break;
            }
            if (font == null)
            {
                Debug.LogWarning("Assign a TMP font on the UI Canvas or in TMP Settings before creating Star HUD.");
                return;
            }

            Scene scene = canvas.gameObject.scene;
            StarCollectionManager manager = StarCollectionManager.FindUniqueInScene(scene);
            var owner = new GameObject("StarHUD", typeof(RectTransform), typeof(CanvasGroup), typeof(StarHUD));
            SceneManager.MoveGameObjectToScene(owner, scene);
            owner.transform.SetParent(canvas.transform, false);
            owner.layer = canvas.gameObject.layer;

            var rect = (RectTransform)owner.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(300f, 100f);

            var group = owner.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            TMP_Text scoreLabel = CreateLabel(rect, font, "Score", "SCORE 000000", 0f);
            int required = manager != null ? manager.RequiredLargeStars : 0;
            TMP_Text countLabel = CreateLabel(rect, font, "Stars", $"STARS 0 / {required}", -30f);
            TMP_Text hintLabel = CreateLabel(rect, font, "Goal Hint", "COLLECT MORE STARS", -66f);
            hintLabel.enabled = false;

            var serialized = new SerializedObject(owner.GetComponent<StarHUD>());
            serialized.FindProperty("collectionManager").objectReferenceValue = manager;
            serialized.FindProperty("scoreLabel").objectReferenceValue = scoreLabel;
            serialized.FindProperty("starCountLabel").objectReferenceValue = countLabel;
            serialized.FindProperty("goalHintLabel").objectReferenceValue = hintLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(owner, "Create Star HUD");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = owner;
            if (manager == null)
                Debug.LogWarning("Star HUD created. Assign Collection Manager manually because the scene has no unique manager.", owner);
        }

        private static TMP_Text CreateLabel(RectTransform parent, TMP_FontAsset font, string name, string text, float y)
        {
            var owner = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            owner.layer = parent.gameObject.layer;
            var rect = (RectTransform)owner.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 30f);

            var label = owner.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }
    }
}
