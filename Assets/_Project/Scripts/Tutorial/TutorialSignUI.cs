using System.Collections.Generic;
using ShadowPath.Input;
using TMPro;
using UnityEngine;

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

        private readonly List<TutorialSign> nearbySigns = new List<TutorialSign>(4);
        private bool tutorialVisible;
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
            if (FocusedSign != previousFocus || FocusedSign == null || GameplayInput.Interact == null
                || !GameplayInput.Interact.WasPressedThisFrame()) return;
            ToggleFocusedTutorial();
        }

        internal void ToggleFocusedTutorial()
        {
            if (FocusedSign == null) return;
            if (tutorialVisible)
            {
                HideTutorial();
                ShowInteractionPrompt();
            }
            else FocusedSign.Read();
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
            tutorialPanel.SetActive(true);
            tutorialVisible = true;
        }

        public void HideTutorial()
        {
            if (IsSafeWidget(tutorialPanel)) tutorialPanel.SetActive(false);
            tutorialVisible = false;
        }

        private bool IsSafeWidget(GameObject widget) => widget != null && !transform.IsChildOf(widget.transform);

        private void OnDisable()
        {
            HideTutorial();
            HideInteractionPrompt();
            FocusedSign = null;
        }
    }
}
