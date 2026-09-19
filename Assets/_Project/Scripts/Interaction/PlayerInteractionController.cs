using ShadowPath.Lighting;
using ShadowPath.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowPath.Interaction
{
    public enum InteractionState
    {
        Normal,
        Interacting
    }

    public class PlayerInteractionController : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private LightSourceController lightSourceController;
        [SerializeField] private GameObject interactionPrompt;

        private InteractionStation nearbyStation;
        private InteractionStation activeStation;
        private InteractionState state = InteractionState.Normal;
        private bool playerControllerWasEnabled;
        private bool lightSourceControllerWasEnabled;

        public bool IsInteracting => state == InteractionState.Interacting;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            UpdateInteractionPrompt();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (state == InteractionState.Normal)
            {
                if (nearbyStation != null && keyboard.eKey.wasPressedThisFrame)
                {
                    EnterInteractionMode(nearbyStation);
                }

                return;
            }

            if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                ExitInteractionMode();
            }
        }

        public void EnterStationRange(InteractionStation station)
        {
            if (station == null)
            {
                return;
            }

            nearbyStation = station;
            UpdateInteractionPrompt();
        }

        public void ExitStationRange(InteractionStation station)
        {
            if (station == null || (nearbyStation != station && activeStation != station))
            {
                return;
            }

            if (activeStation == station)
            {
                ExitInteractionMode();
            }

            if (nearbyStation == station)
            {
                nearbyStation = null;
            }

            UpdateInteractionPrompt();
        }

        private void EnterInteractionMode(InteractionStation station)
        {
            if (station == null || state == InteractionState.Interacting)
            {
                return;
            }

            activeStation = station;
            state = InteractionState.Interacting;
            UpdateInteractionPrompt();

            playerControllerWasEnabled = playerController != null && playerController.enabled;
            lightSourceControllerWasEnabled = lightSourceController != null && lightSourceController.enabled;

            if (playerController != null && playerController.enabled)
            {
                playerController.enabled = false;
            }

            if (lightSourceController != null && lightSourceController.enabled)
            {
                lightSourceController.enabled = false;
            }

            Debug.Log($"Entered interaction mode: {station.name}", this);
        }

        private void ExitInteractionMode()
        {
            if (state == InteractionState.Normal)
            {
                return;
            }

            if (playerController != null && playerControllerWasEnabled)
            {
                playerController.enabled = true;
            }

            if (lightSourceController != null && lightSourceControllerWasEnabled)
            {
                lightSourceController.enabled = true;
            }

            activeStation = null;
            state = InteractionState.Normal;
            UpdateInteractionPrompt();
            Debug.Log("Exited interaction mode", this);
        }

        private void OnDisable()
        {
            ExitInteractionMode();
            SetInteractionPromptActive(false);
        }

        private void UpdateInteractionPrompt()
        {
            SetInteractionPromptActive(state == InteractionState.Normal && nearbyStation != null);
        }

        private void SetInteractionPromptActive(bool active)
        {
            if (interactionPrompt != null && interactionPrompt.activeSelf != active)
            {
                interactionPrompt.SetActive(active);
            }
        }
    }
}
