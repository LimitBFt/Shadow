using ShadowPath.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowPath.Editor.Interaction
{
    public static class InteractionModeHUDSetup
    {
        [MenuItem("GameObject/ShadowPath/Interaction Mode HUD", false, 10)]
        private static void CreateHUD()
        {
            if (Application.isPlaying) return;
            Canvas canvas = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace
                || !canvas.gameObject.scene.IsValid() || EditorUtility.IsPersistent(canvas))
            {
                Debug.LogWarning("Select the normal HUD Canvas before creating Interaction Mode HUD.");
                return;
            }
            InteractionModeHUD existing = canvas.GetComponentInChildren<InteractionModeHUD>(true);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
                if (label.font != null) { font = label.font; break; }
            if (font == null)
            {
                Debug.LogWarning("Assign a TMP font on the HUD Canvas or in TMP Settings before creating the HUD.");
                return;
            }

            var owner = new GameObject("InteractionModeHUD", typeof(RectTransform),
                typeof(CanvasGroup), typeof(CanvasRenderer), typeof(Image));
            SceneManager.MoveGameObjectToScene(owner, canvas.gameObject.scene);
            owner.transform.SetParent(canvas.transform, false);
            owner.layer = canvas.gameObject.layer;
            var rect = (RectTransform)owner.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 70f);
            rect.sizeDelta = new Vector2(800f, 180f);
            Image background = owner.GetComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 0.9f);
            background.raycastTarget = false;
            CanvasGroup visibility = owner.GetComponent<CanvasGroup>();
            visibility.alpha = 0f;
            visibility.interactable = false;
            visibility.blocksRaycasts = false;

            TextMeshProUGUI title = CreateLabel("Title", owner.transform, font, 28f,
                new Vector2(0f, 51f), new Vector2(760f, 40f));
            title.fontStyle = FontStyles.Bold;
            TextMeshProUGUI controls = CreateLabel("Controls", owner.transform, font, 22f,
                new Vector2(0f, -24f), new Vector2(760f, 100f));
            var hud = owner.AddComponent<InteractionModeHUD>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("titleLabel").objectReferenceValue = title;
            serialized.FindProperty("controlsLabel").objectReferenceValue = controls;
            PlayerInteractionController player = FindSinglePlayer(canvas.gameObject.scene);
            serialized.FindProperty("playerInteractionController").objectReferenceValue = player;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            title.text = serialized.FindProperty("selectingTitle").stringValue;
            controls.text = serialized.FindProperty("selectingControls").stringValue;

            Undo.RegisterCreatedObjectUndo(owner, "Create Interaction Mode HUD");
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = owner;
            if (player == null)
                Debug.LogWarning("HUD created. Assign Player Interaction Controller manually; "
                    + "a unique controller was not found in this Canvas's scene.", hud);
        }

        private static PlayerInteractionController FindSinglePlayer(Scene scene)
        {
            PlayerInteractionController result = null;
            foreach (PlayerInteractionController player in Object.FindObjectsByType<PlayerInteractionController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != scene) continue;
                if (result != null) return null;
                result = player;
            }
            return result;
        }

        private static TextMeshProUGUI CreateLabel(string name, Transform parent, TMP_FontAsset font,
            float size, Vector2 position, Vector2 dimensions)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            owner.transform.SetParent(parent, false);
            owner.layer = parent.gameObject.layer;
            var rect = (RectTransform)owner.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var label = owner.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
    }
}
