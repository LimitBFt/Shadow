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
        public enum CheckpointSpawnMode
        {
            PlayerPosition,
            ManualRespawnPoint
        }

        [Header("Tutorial")]
        [SerializeField] private string tutorialTitle = "MOVEMENT";
        [SerializeField, TextArea(2, 8)] private string tutorialText = "LEFT STICK — MOVE\nA — JUMP";
        [SerializeField] private TutorialSignUI tutorialUI;
        [SerializeField] private GameObject signVisual;

        [Header("Gameplay Action")]
        [SerializeField] private LightSourceController lightSourceController;
        [SerializeField] private bool unlockManualLightControl;
        [SerializeField] private bool disableLightAutoFollow;
        [Header("Checkpoint")]
        [SerializeField] private bool activateCheckpointOnRead;
        [SerializeField] private CheckpointSpawnMode checkpointSpawnMode = CheckpointSpawnMode.PlayerPosition;
        [InspectorName("Manual Respawn Point")]
        [Tooltip("Used in ManualRespawnPoint mode, or as an optional fallback when PlayerPosition is unsafe.")]
        [SerializeField] private Transform respawnPoint;

        [Header("Runtime Debug")]
        [SerializeField] private bool playerInRange;

        private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
        private PlayerInteractionController playerInteraction;
        private PlayerController nearbyPlayer;
        private bool ownsTutorialPause;

        public bool IsReading => ownsTutorialPause;
        public bool HasBeenRead { get; private set; }

        internal bool CanRead => isActiveAndEnabled && playerInRange && !HasBeenRead
            && (playerInteraction == null || !playerInteraction.IsInteracting);

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other) => TrackPlayer(other);
        private void OnTriggerStay(Collider other) => TrackPlayer(other);

        private void TrackPlayer(Collider other)
        {
            if (!isActiveAndEnabled || HasBeenRead || other == null) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !playerColliders.Add(other)) return;
            nearbyPlayer = player;
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
            CancelReading();
            playerColliders.Clear();
            playerInRange = false;
            playerInteraction = null;
            nearbyPlayer = null;
            if (tutorialUI != null) tutorialUI.ExitSign(this);
        }

        internal void Read()
        {
            if (!CanRead || Time.timeScale <= 0f || tutorialUI == null || tutorialUI.FocusedSign != this) return;
            if (!tutorialUI.HasTutorialPanel)
            {
                Debug.LogWarning("Configure the shared Tutorial Sign UI panel and text references before reading.", this);
                return;
            }
            ownsTutorialPause = true;
            Time.timeScale = 0f;
            tutorialUI.ShowTutorial(tutorialTitle, tutorialText);
        }

        internal void CompleteReading()
        {
            if (!IsReading) return;
            HasBeenRead = true;
            CancelReading();

            if (lightSourceController != null)
            {
                if (disableLightAutoFollow) lightSourceController.SetAutoFollow(false);
                if (unlockManualLightControl) lightSourceController.SetManualControlEnabled(true);
            }
            else if (disableLightAutoFollow || unlockManualLightControl)
                Debug.LogWarning("This sign unlocks Light control but has no Light Source Controller assigned.", this);

            if (activateCheckpointOnRead)
            {
                PlayerRespawnController respawn = nearbyPlayer != null
                    ? nearbyPlayer.GetComponent<PlayerRespawnController>() : null;
                if (respawn == null)
                    Debug.LogWarning("A checkpoint sign requires a Player Respawn Controller on the Player Root.", this);
                else if (checkpointSpawnMode == CheckpointSpawnMode.PlayerPosition)
                    respawn.TrySetCheckpointFromPlayer(respawnPoint);
                else if (respawnPoint != null)
                    respawn.SetCheckpoint(respawnPoint);
                else
                    Debug.LogWarning("ManualRespawnPoint mode requires a Manual Respawn Point.", this);
            }

            GameObject visual = signVisual;
            if (visual == null)
            {
                Transform child = transform.Find("SignVisual");
                if (child != null) visual = child.gameObject;
            }
            if (visual != null && !transform.IsChildOf(visual.transform)) visual.SetActive(false);
            GetComponent<BoxCollider>().enabled = false;
            LeaveRange();
        }

        internal void CancelReading()
        {
            if (!ownsTutorialPause) return;
            ownsTutorialPause = false;
            Time.timeScale = 1f;
            if (tutorialUI != null) tutorialUI.HideTutorial();
        }

        private void OnDisable() => LeaveRange();
        private void OnDestroy() => CancelReading();
    }
}
