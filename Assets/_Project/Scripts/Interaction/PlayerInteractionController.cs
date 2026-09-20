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
        Selecting,
        Rotating
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
        private RotatableObject selectedObject;
        private ObjectHighlight currentHighlight;
        private bool playerControllerWasEnabled;
        private bool lightSourceControllerWasEnabled;

        public bool IsInteracting => state != InteractionState.Normal;
        public bool IsSelecting => state == InteractionState.Selecting;
        public bool IsRotatingObject => state == InteractionState.Rotating;

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

            switch (state)
            {
                case InteractionState.Normal:
                    HandleNormalInput(keyboard);
                    break;
                case InteractionState.Selecting:
                    HandleSelectingInput(keyboard);
                    break;
                case InteractionState.Rotating:
                    HandleRotatingInput(keyboard);
                    break;
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
                ExitInteractionMode();
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

        private void EnterRotatingMode()
        {
            if (state != InteractionState.Selecting)
            {
                return;
            }

            if (!EnsureValidSelection())
            {
                return;
            }

            state = InteractionState.Rotating;
            UpdateInteractionPrompt();
            Debug.Log($"Entered rotation mode: {selectedObject.name}", this);
        }

        private void ReturnToSelectionMode()
        {
            if (state != InteractionState.Rotating)
            {
                return;
            }

            state = InteractionState.Selecting;

            if (!IsValidSelectable(selectedObject))
            {
                ClearCurrentSelection();
                SelectFirstValidObject();
            }

            UpdateInteractionPrompt();
            Debug.Log("Returned to selection mode", this);
        }

        private void ExitInteractionMode()
        {
            if (state == InteractionState.Normal)
            {
                return;
            }

            ClearCurrentSelection();

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

        private void HandleNormalInput(Keyboard keyboard)
        {
            if (nearbyStation != null && keyboard.eKey.wasPressedThisFrame)
            {
                EnterSelectionMode(nearbyStation);
            }
        }

        private void HandleSelectingInput(Keyboard keyboard)
        {
            if (currentStation == null)
            {
                ExitInteractionMode();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ExitInteractionMode();
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                EnterRotatingMode();
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
        }

        private void HandleRotatingInput(Keyboard keyboard)
        {
            if (currentStation == null)
            {
                ExitInteractionMode();
                return;
            }

            if (!IsValidSelectable(selectedObject))
            {
                ReturnToSelectionMode();
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                ReturnToSelectionMode();
                return;
            }

            if (selectedObject.IsRotating)
            {
                return;
            }

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                selectedObject.RotateNegative();
            }

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                selectedObject.RotatePositive();
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
                ClearCurrentSelection();
                return;
            }

            IReadOnlyList<RotatableObject> controlledObjects = currentStation.ControlledObjects;
            if (controlledObjects == null || controlledObjects.Count == 0)
            {
                ClearCurrentSelection();
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
            selectedObject = null;
            selectedIndex = -1;
        }

        private void SetSelectedObject(int newSelectedIndex, RotatableObject selectedObject)
        {
            ClearCurrentHighlight();
            selectedIndex = newSelectedIndex;
            this.selectedObject = selectedObject;
            currentHighlight = selectedObject.GetComponent<ObjectHighlight>();
            if (currentHighlight != null)
            {
                currentHighlight.SetHighlighted(true);
            }
        }

        private bool EnsureValidSelection()
        {
            if (IsValidSelectable(selectedObject))
            {
                return true;
            }

            ClearCurrentSelection();
            SelectFirstValidObject();
            return IsValidSelectable(selectedObject);
        }

        private void ClearCurrentSelection()
        {
            ClearCurrentHighlight();
            selectedObject = null;
            selectedIndex = -1;
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
