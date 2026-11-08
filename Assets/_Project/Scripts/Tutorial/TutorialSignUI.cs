using System.Collections.Generic;
using ShadowPath.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowPath.Tutorial
{
    [DisallowMultipleComponent]
    public class TutorialSignUI : MonoBehaviour
    {
        [Header("Interaction Prompt")]
        [SerializeField] private GameObject interactionPrompt;
        [SerializeField] private TMP_Text interactionPromptLabel;
        [SerializeField] private string interactionPromptText = "[X] READ";

        [Header("Tutorial Panel")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text closePromptLabel;
        [SerializeField] private string closePromptText = "B — CLOSE";

        private readonly List<TutorialSign> nearbySigns = new List<TutorialSign>(4);
        internal TutorialSign FocusedSign { get; private set; }
        internal bool HasTutorialPanel => IsSafeWidget(tutorialPanel) && titleLabel != null && bodyLabel != null;

        private void Awake()
        {
            GameplayInput.TryInitialize(this);
            HideTutorial();
            HideInteractionPrompt();
        }

        internal void EnterSign(TutorialSign sign)
        {
            if (!nearbySigns.Contains(sign)) nearbySigns.Add(sign);
            RefreshFocus();
        }

        internal void ExitSign(TutorialSign sign)
        {
            nearbySigns.Remove(sign);
            RefreshFocus();
        }

        private void RefreshFocus()
        {
            if (FocusedSign != null && FocusedSign.IsReading) return;

            TutorialSign next = null;
            if (isActiveAndEnabled)
                for (int i = 0; i < nearbySigns.Count; i++)
                    if (nearbySigns[i] != null && nearbySigns[i].CanRead) { next = nearbySigns[i]; break; }
            if (next == FocusedSign) return;
            HideTutorial();
            HideInteractionPrompt();
            FocusedSign = next;
            if (FocusedSign != null) ShowInteractionPrompt();
        }

        private void LateUpdate()
        {
            // Existing object-interaction modes take priority when their ranges overlap a sign.
            TutorialSign previousFocus = FocusedSign;
            RefreshFocus();
            if (FocusedSign != null && FocusedSign.IsReading)
            {
                foreach (Gamepad gamepad in Gamepad.all)
                {
                    if (!gamepad.buttonEast.wasPressedThisFrame) continue;
                    FocusedSign.CompleteReading();
                    break;
                }
                return;
            }

            if (FocusedSign != previousFocus || FocusedSign == null || GameplayInput.Interact == null
                || !GameplayInput.Interact.WasPressedThisFrame()) return;
            FocusedSign.Read();
        }

        public void ShowInteractionPrompt()
        {
            if (!IsSafeWidget(interactionPrompt)) return;
            if (interactionPromptLabel != null) interactionPromptLabel.text = interactionPromptText;
            interactionPrompt.SetActive(true);
        }

        public void HideInteractionPrompt()
        {
            if (IsSafeWidget(interactionPrompt)) interactionPrompt.SetActive(false);
        }

        public void ShowTutorial(string title, string text)
        {
            if (!HasTutorialPanel)
            {
                Debug.LogWarning("Assign a separate Tutorial Panel and its title/body TMP labels.", this);
                return;
            }
            HideInteractionPrompt();
            titleLabel.text = title;
            bodyLabel.text = text;
            if (closePromptLabel == null) CreateClosePrompt();
            closePromptLabel.text = closePromptText;
            closePromptLabel.gameObject.SetActive(true);
            tutorialPanel.SetActive(true);
        }

        public void HideTutorial()
        {
            if (IsSafeWidget(tutorialPanel)) tutorialPanel.SetActive(false);
        }

        private void CreateClosePrompt()
        {
            // Existing panels can use the footer without a scene migration.
            var footer = new GameObject("ClosePrompt", typeof(RectTransform));
            footer.layer = tutorialPanel.layer;
            var rect = (RectTransform)footer.transform;
            rect.SetParent(tutorialPanel.transform, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.offsetMin = new Vector2(12f, 8f);
            rect.offsetMax = new Vector2(-12f, 42f);
            closePromptLabel = footer.AddComponent<TextMeshProUGUI>();
            closePromptLabel.font = bodyLabel.font;
            closePromptLabel.color = bodyLabel.color;
            closePromptLabel.fontSize = 22f;
            closePromptLabel.alignment = TextAlignmentOptions.Center;
            closePromptLabel.raycastTarget = false;
        }

        private bool IsSafeWidget(GameObject widget) => widget != null && !transform.IsChildOf(widget.transform);

        private void OnDisable()
        {
            if (FocusedSign != null) FocusedSign.CancelReading();
            HideTutorial();
            HideInteractionPrompt();
            FocusedSign = null;
        }
    }
}
