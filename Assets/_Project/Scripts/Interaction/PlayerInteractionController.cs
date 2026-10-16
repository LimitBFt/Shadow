using System.Collections.Generic;
using ShadowPath.Input;
using ShadowPath.Lighting;
using ShadowPath.Player;
using UnityEngine;

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
        [SerializeField, Range(0f, 1f)] private float moveInputThreshold = 0.5f;

        private InteractionStation nearbyStation;
        private InteractionStation currentStation;
        private InteractionState state = InteractionState.Normal;
        private int selectedIndex = -1;
        private RotatableObject selectedObject;
        private ObjectHighlight currentHighlight;
        private bool playerControllerWasEnabled;
        private bool lightSourceControllerWasEnabled;
        private bool moveInputArmed = true;
        private const float SelectionEpsilon = 0.0001f;

        public bool IsInteracting => state != InteractionState.Normal;
        public bool IsSelecting => state == InteractionState.Selecting;
        public bool IsRotatingObject => state == InteractionState.Rotating;

        private void Awake()
        {
            GameplayInput.TryInitialize(this);
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            UpdateInteractionPrompt();
        }

        private void Update()
        {
            switch (state)
            {
                case InteractionState.Normal:
                    HandleNormalInput();
                    break;
                case InteractionState.Selecting:
                    HandleSelectingInput();
                    break;
                case InteractionState.Rotating:
                    HandleRotatingInput();
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
            moveInputArmed = true;
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
            moveInputArmed = false;
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
            moveInputArmed = false;

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

        private void HandleNormalInput()
        {
            if (nearbyStation != null
                && GameplayInput.Interact != null
                && GameplayInput.Interact.WasPressedThisFrame())
            {
                EnterSelectionMode(nearbyStation);
            }
        }

        private void HandleSelectingInput()
        {
            if (currentStation == null)
            {
                ExitInteractionMode();
                return;
            }

            if (GameplayInput.Cancel != null && GameplayInput.Cancel.WasPressedThisFrame())
            {
                ExitInteractionMode();
                return;
            }

            if (GameplayInput.Interact != null && GameplayInput.Interact.WasPressedThisFrame())
            {
                EnterRotatingMode();
                return;
            }

            if (TryReadMoveDirection(out Vector2 direction))
            {
                TrySelectInDirection(direction);
            }
        }

        private void HandleRotatingInput()
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

            bool interactPressed = GameplayInput.Interact != null
                && GameplayInput.Interact.WasPressedThisFrame();
            bool cancelPressed = GameplayInput.Cancel != null
                && GameplayInput.Cancel.WasPressedThisFrame();
            if (interactPressed || cancelPressed)
            {
                ReturnToSelectionMode();
                return;
            }

            if (selectedObject.IsRotating)
            {
                return;
            }

            if (!TryReadMoveDirection(out Vector2 direction) || Mathf.Abs(direction.x) < 1f)
            {
                return;
            }

            if (direction.x < 0f)
            {
                selectedObject.RotateNegative();
                return;
            }

            selectedObject.RotatePositive();
        }

        private bool TryReadMoveDirection(out Vector2 direction)
        {
            direction = Vector2.zero;
            Vector2 move = GameplayInput.Move == null
                ? Vector2.zero
                : GameplayInput.Move.ReadValue<Vector2>();
            float threshold = Mathf.Clamp01(moveInputThreshold);

            if (move.magnitude < threshold)
            {
                moveInputArmed = true;
                return false;
            }

            if (!moveInputArmed)
            {
                return false;
            }

            moveInputArmed = false;
            direction = Mathf.Abs(move.x) >= Mathf.Abs(move.y)
                ? new Vector2(Mathf.Sign(move.x), 0f)
                : new Vector2(0f, Mathf.Sign(move.y));
            return true;
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
