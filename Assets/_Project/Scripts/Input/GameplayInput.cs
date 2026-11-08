using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowPath.Input
{
    public static class GameplayInput
    {
        private const string ActionMapName = "Gameplay";

        private static InputActionMap actionMap;
        private static bool initializationAttempted;
        private static readonly HashSet<Object> modalInputOwners = new HashSet<Object>();
        private static int blockedCommandFrame = -1;

        public static InputAction Move { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Interact { get; private set; }
        public static InputAction Cancel { get; private set; }
        public static InputAction LightMove { get; private set; }
        public static InputAction ResetToCheckpoint { get; private set; }
        public static InputAction CameraZoomOut { get; private set; }
        public static InputAction CameraZoomIn { get; private set; }

        public static bool GameplayCommandsBlocked =>
            Time.timeScale <= 0f || modalInputOwners.Count > 0 || blockedCommandFrame == Time.frameCount;

        internal static string GetGameplayCommandBlockReason()
        {
            if (Time.timeScale <= 0f) return "Time.timeScale is zero or negative.";
            foreach (Object owner in modalInputOwners)
                return owner != null
                    ? $"Modal input owner {owner.name} ({owner.GetType().Name})."
                    : "A destroyed modal input owner is still registered.";
            return blockedCommandFrame == Time.frameCount
                ? "Gameplay commands were consumed earlier in this frame."
                : null;
        }

        internal static void AcquireModalInput(Object owner) => modalInputOwners.Add(owner);

        internal static void ReleaseModalInput(Object owner)
        {
            if (modalInputOwners.Remove(owner)) BlockGameplayCommandsForFrame();
        }

        internal static void BlockGameplayCommandsForFrame() => blockedCommandFrame = Time.frameCount;

        public static bool TryInitialize(Object context)
        {
            if (actionMap != null)
            {
                if (!actionMap.enabled)
                {
                    actionMap.Enable();
                }

                return true;
            }

            if (initializationAttempted)
            {
                return false;
            }

            initializationAttempted = true;
            InputActionAsset projectActions = InputSystem.actions;
            if (projectActions == null)
            {
                Debug.LogError("Project-wide Input Actions are not configured.", context);
                return false;
            }

            actionMap = projectActions.FindActionMap(ActionMapName, false);
            if (actionMap == null)
            {
                Debug.LogError($"Input Action Map '{ActionMapName}' was not found.", context);
                return false;
            }

            Move = FindRequiredAction("Move", context);
            Jump = FindRequiredAction("Jump", context);
            Interact = FindRequiredAction("Interact", context);
            Cancel = FindRequiredAction("Cancel", context);
            LightMove = FindRequiredAction("LightMove", context);
            // Older action assets can continue to supply movement without the optional reset action.
            ResetToCheckpoint = actionMap.FindAction("ResetToCheckpoint", false);
            CameraZoomOut = actionMap.FindAction("CameraZoomOut", false);
            CameraZoomIn = actionMap.FindAction("CameraZoomIn", false);

            if (Move == null || Jump == null || Interact == null || Cancel == null || LightMove == null)
            {
                actionMap = null;
                return false;
            }

            actionMap.Enable();
            return true;
        }

        public static bool IsLightMoveFromGamepad()
        {
            return LightMove?.activeControl?.device is Gamepad;
        }

        private static InputAction FindRequiredAction(string actionName, Object context)
        {
            InputAction action = actionMap.FindAction(actionName, false);
            if (action == null)
            {
                Debug.LogError($"Input Action '{ActionMapName}/{actionName}' was not found.", context);
            }

            return action;
        }
    }
}
