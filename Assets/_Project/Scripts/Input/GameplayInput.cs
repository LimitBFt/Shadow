using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowPath.Input
{
    public static class GameplayInput
    {
        private const string ActionMapName = "Gameplay";

        private static InputActionMap actionMap;
        private static bool initializationAttempted;

        public static InputAction Move { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Interact { get; private set; }
        public static InputAction Cancel { get; private set; }
        public static InputAction LightMove { get; private set; }

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
