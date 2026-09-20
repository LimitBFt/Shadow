using System.Collections.Generic;
using ShadowPath.Lighting;
using ShadowPath.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowPath.Interaction
{
    public enum InteractionState
    {
        Normal,
        Selecting
    }

    public class PlayerInteractionController : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private LightSourceController lightSourceController;
        [SerializeField] private GameObject interactionPrompt;

        private InteractionStation nearbyStation;
        private InteractionStation currentStation;
        private InteractionState state = InteractionState.Normal;
        private int selectedIndex = -1;
        private ObjectHighlight currentHighlight;
        private bool playerControllerWasEnabled;
        private bool lightSourceControllerWasEnabled;

        public bool IsInteracting => state == InteractionState.Selecting;
        public bool IsSelecting => state == InteractionState.Selecting;

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
                    EnterSelectionMode(nearbyStation);
                }

                return;
            }

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                SelectPreviousObject();
            }

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                SelectNextObject();
            }

            if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                ExitSelectionMode();
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
            if (station == null || (nearbyStation != station && currentStation != station))
            {
                return;
            }

            if (currentStation == station)
            {
                ExitSelectionMode();
            }

            if (nearbyStation == station)
            {
                nearbyStation = null;
            }

            UpdateInteractionPrompt();
        }

        private void EnterSelectionMode(InteractionStation station)
        {
            if (station == null || state == InteractionState.Selecting)
            {
                return;
            }

            currentStation = station;
            state = InteractionState.Selecting;
            selectedIndex = -1;
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

            SelectFirstValidObject();
            Debug.Log($"Entered selection mode: {station.name}", this);
        }

        private void ExitSelectionMode()
        {
            if (state == InteractionState.Normal)
            {
                return;
            }

            ClearCurrentHighlight();
            selectedIndex = -1;

            if (playerController != null && playerControllerWasEnabled)
            {
                playerController.enabled = true;
            }

            if (lightSourceController != null && lightSourceControllerWasEnabled)
            {
                lightSourceController.enabled = true;
            }

            currentStation = null;
            state = InteractionState.Normal;
            UpdateInteractionPrompt();
            Debug.Log("Exited selection mode", this);
        }

        private void OnDisable()
        {
            ExitSelectionMode();
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

        private void SelectFirstValidObject()
        {
            SelectObjectByDirection(1);
        }

        private void SelectNextObject()
        {
            SelectObjectByDirection(1);
        }

        private void SelectPreviousObject()
        {
            SelectObjectByDirection(-1);
        }

        private void SelectObjectByDirection(int direction)
        {
            if (currentStation == null)
            {
                ClearCurrentHighlight();
                selectedIndex = -1;
                return;
            }

            IReadOnlyList<RotatableObject> controlledObjects = currentStation.ControlledObjects;
            if (controlledObjects == null || controlledObjects.Count == 0)
            {
                ClearCurrentHighlight();
                selectedIndex = -1;
                return;
            }

            int startIndex = selectedIndex;
            for (int i = 0; i < controlledObjects.Count; i++)
            {
                int candidateIndex = GetWrappedIndex(startIndex + direction * (i + 1), controlledObjects.Count);
                RotatableObject candidate = controlledObjects[candidateIndex];
                if (!IsValidSelectable(candidate))
                {
                    continue;
                }

                SetSelectedObject(candidateIndex, candidate);
                return;
            }

            ClearCurrentHighlight();
            selectedIndex = -1;
        }

        private void SetSelectedObject(int newSelectedIndex, RotatableObject selectedObject)
        {
            ClearCurrentHighlight();
            selectedIndex = newSelectedIndex;
            currentHighlight = selectedObject.GetComponent<ObjectHighlight>();
            if (currentHighlight != null)
            {
                currentHighlight.SetHighlighted(true);
            }
        }

        private void ClearCurrentHighlight()
        {
            if (currentHighlight != null)
            {
                currentHighlight.SetHighlighted(false);
                currentHighlight = null;
            }
        }

        private static bool IsValidSelectable(RotatableObject rotatableObject)
        {
            return rotatableObject != null
                && rotatableObject.isActiveAndEnabled;
        }

        private static int GetWrappedIndex(int index, int count)
        {
            int wrappedIndex = index % count;
            if (wrappedIndex < 0)
            {
                wrappedIndex += count;
            }

            return wrappedIndex;
        }
    }
}
