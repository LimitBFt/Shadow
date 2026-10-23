using System.Collections.Generic;
using ShadowPath.Interaction;
using ShadowPath.Lighting;
using ShadowPath.Player;
using UnityEngine;

namespace ShadowPath.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class TutorialSign : MonoBehaviour
    {
        [Header("Tutorial")]
        [SerializeField] private string tutorialTitle = "MOVEMENT";
        [SerializeField, TextArea(2, 8)] private string tutorialText = "LEFT STICK — MOVE\nA — JUMP";
        [SerializeField] private TutorialSignUI tutorialUI;

        [Header("Gameplay Action")]
        [SerializeField] private LightSourceController lightSourceController;
        [SerializeField] private bool unlockManualLightControl;
        [SerializeField] private bool disableLightAutoFollow;
        [SerializeField] private bool gameplayActionOneShot = true;

        [Header("Runtime Debug")]
        [SerializeField] private bool playerInRange;
        [SerializeField] private bool gameplayActionExecuted;

        private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
        private PlayerInteractionController playerInteraction;

        internal bool CanRead => isActiveAndEnabled && playerInRange
            && (playerInteraction == null || !playerInteraction.IsInteracting);

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other) => TrackPlayer(other);
        private void OnTriggerStay(Collider other) => TrackPlayer(other);

        private void TrackPlayer(Collider other)
        {
            if (!isActiveAndEnabled || other == null) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !playerColliders.Add(other)) return;
            playerInteraction = player.GetComponent<PlayerInteractionController>();
            playerInRange = true;
            if (tutorialUI != null) tutorialUI.EnterSign(this);
            else Debug.LogWarning("Assign the shared Tutorial Sign UI before using this sign.", this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!playerColliders.Remove(other) || playerColliders.Count > 0) return;
            LeaveRange();
        }

        private void Update()
        {
            if (!playerInRange) return;
            // Collider destruction/disable does not always produce an exit callback.
            foreach (Collider collider in playerColliders)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy) return;
            LeaveRange();
        }

        private void LeaveRange()
        {
            playerColliders.Clear();
            playerInRange = false;
            playerInteraction = null;
            if (tutorialUI != null) tutorialUI.ExitSign(this);
        }

        internal void Read()
        {
            if (!CanRead || tutorialUI == null || tutorialUI.FocusedSign != this) return;
            if (!tutorialUI.HasTutorialPanel)
            {
                Debug.LogWarning("Configure the shared Tutorial Sign UI panel and text references before reading.", this);
                return;
            }
            if (!gameplayActionOneShot || !gameplayActionExecuted)
            {
                bool hasLightAction = disableLightAutoFollow || unlockManualLightControl;
                if (hasLightAction && lightSourceController == null)
                {
                    Debug.LogWarning("This sign unlocks Light control but has no Light Source Controller assigned.", this);
                    return;
                }
                // Existing APIs clear motion history and preserve the current world position.
                if (disableLightAutoFollow) lightSourceController.SetAutoFollow(false);
                if (unlockManualLightControl) lightSourceController.SetManualControlEnabled(true);
                gameplayActionExecuted = true;
            }
            tutorialUI.ShowTutorial(tutorialTitle, tutorialText);
        }

        private void OnDisable() => LeaveRange();
    }
}
