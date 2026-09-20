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
        [SerializeField, Range(0f, 1f)] private float directionThreshold = 0.35f;

        private InteractionStation nearbyStation;
        private InteractionStation currentStation;
        private InteractionState state = InteractionState.Normal;
        private int selectedIndex = -1;
        private RotatableObject selectedObject;
        private ObjectHighlight currentHighlight;
        private bool playerControllerWasEnabled;
        private bool lightSourceControllerWasEnabled;
        private const float SelectionEpsilon = 0.0001f;

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

            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                TrySelectInDirection(Vector2.up);
                return;
            }

            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                TrySelectInDirection(Vector2.down);
                return;
            }

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                TrySelectInDirection(Vector2.left);
                return;
            }

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                TrySelectInDirection(Vector2.right);
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

            for (int i = 0; i < controlledObjects.Count; i++)
            {
                RotatableObject candidate = controlledObjects[i];
                if (!IsValidSelectable(candidate))
                {
                    continue;
                }

                SetSelectedObject(i, candidate);
                return;
            }

            ClearCurrentHighlight();
            selectedObject = null;
            selectedIndex = -1;
        }

        private bool TrySelectInDirection(Vector2 inputDirection)
        {
            if (!EnsureValidSelection())
            {
                return false;
            }

            if (currentStation == null)
            {
                ClearCurrentSelection();
                return false;
            }

            IReadOnlyList<RotatableObject> controlledObjects = currentStation.ControlledObjects;
            if (controlledObjects == null || controlledObjects.Count == 0)
            {
                return false;
            }

            Vector2 normalizedDirection = inputDirection.normalized;
            Vector2 currentPosition = GetWorldXY(selectedObject.transform.position);
            int bestIndex = -1;
            RotatableObject bestCandidate = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < controlledObjects.Count; i++)
            {
                RotatableObject candidate = controlledObjects[i];
                if (!IsValidSelectable(candidate) || candidate == selectedObject)
                {
                    continue;
                }

                Vector2 candidatePosition = GetWorldXY(candidate.transform.position);
                Vector2 delta = candidatePosition - currentPosition;
                float sqrDistance = delta.sqrMagnitude;
                if (sqrDistance <= SelectionEpsilon * SelectionEpsilon)
                {
                    continue;
                }

                float forward = Vector2.Dot(delta, normalizedDirection);
                if (forward <= SelectionEpsilon)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(sqrDistance);
                float alignment = forward / distance;
                if (alignment < directionThreshold)
                {
                    continue;
                }

                float score = alignment * 1000f - distance;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestIndex = i;
                bestCandidate = candidate;
            }

            if (bestCandidate == null)
            {
                return false;
            }

            SetSelectedObject(bestIndex, bestCandidate);
            return true;
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

        private static Vector2 GetWorldXY(Vector3 worldPosition)
        {
            return new Vector2(worldPosition.x, worldPosition.y);
        }

    }
}
