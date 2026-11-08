using TMPro;
using UnityEngine;

namespace ShadowPath.Interaction
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class InteractionModeHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractionController playerInteractionController;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text controlsLabel;

        [Header("Selecting")]
        [SerializeField] private string selectingTitle = "SELECT OBJECT";
        [SerializeField, TextArea(3, 6)] private string selectingControls =
            "LEFT STICK / W A S D — SELECT\nX / E — CONFIRM\nB / ESC — CANCEL";

        [Header("Rotating")]
        [SerializeField] private string rotatingTitle = "ROTATE OBJECT";
        [SerializeField, TextArea(3, 6)] private string rotatingControls =
            "LEFT STICK LEFT/RIGHT / A D — ROTATE\nX / E — CONFIRM & RETURN TO SELECT\nB / ESC — BACK TO SELECT";

        private CanvasGroup canvasGroup;

        private void OnEnable()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            bool canDisplay = playerInteractionController != null
                && playerInteractionController.isActiveAndEnabled && titleLabel != null && controlsLabel != null;
            bool selecting = canDisplay && playerInteractionController.IsSelecting;
            bool rotating = canDisplay && playerInteractionController.IsRotatingObject;
            float alpha = selecting || rotating ? 1f : 0f;
            if (canvasGroup.alpha != alpha) canvasGroup.alpha = alpha;
            if (alpha == 0f) return;

            string title = selecting ? selectingTitle : rotatingTitle;
            string controls = selecting ? selectingControls : rotatingControls;
            if (titleLabel.text != title) titleLabel.text = title;
            if (controlsLabel.text != controls) controlsLabel.text = controls;
        }

        private void OnDisable()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }
    }
}
