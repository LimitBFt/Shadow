using ShadowPath.Input;
using ShadowPath.Shadow;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShadowPath.Lighting
{
    public class LightSourceController : MonoBehaviour
    {
        private const float InitializationPositionEpsilon = 0.0001f;

        [Header("References")]
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private LightGameplayArea lightGameplayArea;

        [Header("Level Light Bounds")]
        [SerializeField] private Vector2 minLevelLightPosition = new Vector2(-12f, -5f);
        [SerializeField] private Vector2 maxLevelLightPosition = new Vector2(12f, 8f);

        [Header("Movement Feel")]
        [FormerlySerializedAs("moveSpeed")]
        [FormerlySerializedAs("keyboardMoveSpeed")]
        [SerializeField, Min(0f)] private float horizontalMaxSpeed = 3f;
        [SerializeField, Min(0f)] private float verticalMaxSpeed = 2.5f;
        [SerializeField, Min(0f)] private float acceleration = 6f;
        [SerializeField, Min(0f)] private float deceleration = 8f;
        [SerializeField, Min(0f)] private float turnAcceleration = 10f;
        [Tooltip("Input ramp time in seconds; 0 gives immediate keyboard input. Velocity still accelerates/decelerates.")]
        [SerializeField, Min(0f)] private float keyboardInputSmoothing = 0.08f;
        [SerializeField, Range(0f, 0.99f)] private float gamepadDeadZone = 0.15f;
        [SerializeField, Min(0.01f)] private float gamepadResponseExponent = 1.5f;

        // Retain the old per-device gamepad value for serialized compatibility.
        // The shared per-axis maximums above now apply to both input devices.
        [FormerlySerializedAs("gamepadLightMoveSpeed")]
        [FormerlySerializedAs("gamepadMoveSpeed")]
        [SerializeField, HideInInspector] private float legacyGamepadMoveSpeed = 2.5f;

        [Header("Runtime Debug")]
        [SerializeField] private Vector2 currentLightPlanePosition;
        [FormerlySerializedAs("normalDistance")]
        [SerializeField] private float signedNormalDistance;
        [SerializeField] private Vector3 initialWorldPosition;
        [SerializeField] private Vector3 reconstructedWorldPosition;
        [SerializeField] private Vector3 planeNormal;
        [SerializeField] private float initializationDelta;
        [SerializeField] private Vector2 initialLightPlanePosition;
        [SerializeField] private Vector2 playerPlanePosition;
        [SerializeField] private float allowedLightMinX;
        [SerializeField] private float allowedLightMaxX;
        [SerializeField] private bool initialGameplayConstraintValid;
        [SerializeField, InspectorName("Raw Proposed Plane Position")] private Vector2 rawProposedLightPosition;
        [SerializeField, InspectorName("Final Plane Position")] private Vector2 finalLightPosition;
        [SerializeField, InspectorName("Constraint Correction")] private Vector2 lastClampCorrection;
        [SerializeField] private Vector2 rawInput;
        [SerializeField] private Vector2 processedInput;
        [SerializeField] private Vector2 desiredPlaneVelocity;
        [SerializeField] private Vector2 currentPlaneVelocity;
        [SerializeField] private bool isLevelBoundsBlocking;
        [SerializeField] private bool isGameplayCircleBlocking;

        private bool isInitialized;
        private bool preserveInitialPositionForFirstUpdate;
        private bool initialMovementValid;
        private Vector2 smoothedKeyboardInput;

        private Vector2 SafeMinLevelPosition =>
            Vector2.Min(minLevelLightPosition, maxLevelLightPosition);

        private Vector2 SafeMaxLevelPosition =>
            Vector2.Max(minLevelLightPosition, maxLevelLightPosition);

        private void Awake()
        {
            GameplayInput.TryInitialize(this);

            if (!TryInitializeMovement())
            {
                enabled = false;
            }
        }

        private void Start()
        {
            initialLightPlanePosition = currentLightPlanePosition;
            rawProposedLightPosition = finalLightPosition = currentLightPlanePosition;
            lastClampCorrection = Vector2.zero;
            initialGameplayConstraintValid = lightGameplayArea == null
                || lightGameplayArea.InitializeLightConstraint();
            RefreshGameplayConstraintDebug();
            initialMovementValid = initialGameplayConstraintValid
                && IsInsideLevelBounds(initialLightPlanePosition);
        }

        private void RefreshGameplayConstraintDebug()
        {
            if (lightGameplayArea != null)
            {
                lightGameplayArea.TryGetAllowedLightRange(out allowedLightMinX, out allowedLightMaxX);
                playerPlanePosition = lightGameplayArea.PlayerPlanePosition;
            }
        }

        private void Update()
        {
            RefreshGameplayConstraintDebug();
            if (!initialMovementValid)
            {
                currentPlaneVelocity = desiredPlaneVelocity = Vector2.zero;
                smoothedKeyboardInput = Vector2.zero;
                return;
            }

            if (preserveInitialPositionForFirstUpdate)
            {
                preserveInitialPositionForFirstUpdate = false;
                transform.position = initialWorldPosition;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            rawInput = ReadMoveInput();
            bool isGamepadInput = GameplayInput.IsLightMoveFromGamepad();
            if (isGamepadInput)
            {
                LightMovementMath.ProcessGamepad(rawInput.x, rawInput.y, gamepadDeadZone, gamepadResponseExponent,
                    out float x, out float y);
                processedInput = new Vector2(x, y);
                smoothedKeyboardInput = Vector2.zero;
            }
            else
            {
                LightMovementMath.SmoothKeyboard(smoothedKeyboardInput.x, smoothedKeyboardInput.y, rawInput.x, rawInput.y,
                    keyboardInputSmoothing, dt, out float x, out float y);
                smoothedKeyboardInput = processedInput = new Vector2(x, y);
            }
            desiredPlaneVelocity = new Vector2(processedInput.x * Mathf.Max(0f, horizontalMaxSpeed),
                processedInput.y * Mathf.Max(0f, verticalMaxSpeed));
            LightMovementMath.AdvanceVelocity(currentPlaneVelocity.x, currentPlaneVelocity.y,
                desiredPlaneVelocity.x, desiredPlaneVelocity.y, acceleration, deceleration, turnAcceleration, dt,
                out float vx, out float vy);
            currentPlaneVelocity = new Vector2(vx, vy);
            rawProposedLightPosition = currentLightPlanePosition + currentPlaneVelocity * dt;
            Vector2 proposedPosition = ClampToLevelBounds(rawProposedLightPosition);
            isLevelBoundsBlocking = (proposedPosition - rawProposedLightPosition).sqrMagnitude > 0.0000000001f;
            isGameplayCircleBlocking = false;

            if (lightGameplayArea != null)
            {
                if (!lightGameplayArea.TryConstrainLightPlanePosition(currentLightPlanePosition,
                    proposedPosition, SafeMinLevelPosition, SafeMaxLevelPosition, out Vector2 constrainedPosition))
                {
                    finalLightPosition = currentLightPlanePosition;
                    lastClampCorrection = finalLightPosition - rawProposedLightPosition;
                    isGameplayCircleBlocking = true;
                    currentPlaneVelocity = smoothedKeyboardInput = Vector2.zero;
                    return;
                }

                isGameplayCircleBlocking = (constrainedPosition - proposedPosition).sqrMagnitude > 0.0000000001f;
                proposedPosition = constrainedPosition;
            }

            finalLightPosition = proposedPosition;
            lastClampCorrection = finalLightPosition - rawProposedLightPosition;
            CleanBlockedVelocity(finalLightPosition);
            currentLightPlanePosition = proposedPosition;
            ApplyLightPosition();
        }

        private void CleanBlockedVelocity(Vector2 position)
        {
            Vector2 minimum = SafeMinLevelPosition, maximum = SafeMaxLevelPosition;
            Vector2 center = Vector2.zero;
            float radius = 0f;
            bool hasCircle = lightGameplayArea != null && lightGameplayArea.TryGetGameplayCircle(out center, out radius);
            // Legal Light centers form the same-radius disk centered at the Player, not at the Light.
            if (hasCircle) center = lightGameplayArea.PlayerPlanePosition;
            if (!hasCircle && lightGameplayArea != null
                && lightGameplayArea.TryGetAllowedLightRange(out float low, out float high))
            {
                minimum.x = Mathf.Max(minimum.x, low);
                maximum.x = Mathf.Min(maximum.x, high);
            }
            LightMovementMath.RemoveBlockedVelocity(currentPlaneVelocity.x, currentPlaneVelocity.y, position.x, position.y,
                minimum.x, minimum.y, maximum.x, maximum.y, hasCircle, center.x, center.y, radius,
                out float vx, out float vy);
            currentPlaneVelocity = new Vector2(vx, vy);
            // Clear input history at contact, even on the exact first frame touching an edge.
            // Otherwise an immediate reverse could first spend frames unwinding hidden input smoothing.
            LightMovementMath.RemoveBlockedVelocity(smoothedKeyboardInput.x * horizontalMaxSpeed,
                smoothedKeyboardInput.y * verticalMaxSpeed, position.x, position.y,
                minimum.x, minimum.y, maximum.x, maximum.y, hasCircle, center.x, center.y, radius,
                out float ix, out float iy);
            smoothedKeyboardInput = new Vector2(horizontalMaxSpeed > 0f ? ix / horizontalMaxSpeed : 0f,
                verticalMaxSpeed > 0f ? iy / verticalMaxSpeed : 0f);
        }

        private void OnDisable()
        {
            currentPlaneVelocity = desiredPlaneVelocity = Vector2.zero;
            smoothedKeyboardInput = rawInput = processedInput = Vector2.zero;
        }

        private void OnValidate()
        {
            horizontalMaxSpeed = Mathf.Max(0f, horizontalMaxSpeed);
            verticalMaxSpeed = Mathf.Max(0f, verticalMaxSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            turnAcceleration = Mathf.Max(0f, turnAcceleration);
            keyboardInputSmoothing = Mathf.Max(0f, keyboardInputSmoothing);
            gamepadDeadZone = Mathf.Clamp(gamepadDeadZone, 0f, 0.99f);
            gamepadResponseExponent = Mathf.Max(0.01f, gamepadResponseExponent);
            legacyGamepadMoveSpeed = Mathf.Max(0f, legacyGamepadMoveSpeed);
        }

        private bool TryInitializeMovement()
        {
            if (projectionPlane == null)
            {
                Debug.LogError(
                    $"{nameof(LightSourceController)} requires a Projection Plane.",
                    this);
                return false;
            }

            initialWorldPosition = transform.position;
            if (!TryWorldToPlane(initialWorldPosition, out currentLightPlanePosition))
            {
                Debug.LogError(
                    $"{nameof(LightSourceController)} could not use the assigned Projection Plane axes.",
                    this);
                return false;
            }

            planeNormal = projectionPlane.forward.normalized;
            signedNormalDistance = Vector3.Dot(
                initialWorldPosition - projectionPlane.position,
                planeNormal);

            if (!TryPlaneToWorld(
                    currentLightPlanePosition,
                    signedNormalDistance,
                    out reconstructedWorldPosition))
            {
                Debug.LogError(
                    $"{nameof(LightSourceController)} could not reconstruct the initial light position.",
                    this);
                return false;
            }

            initializationDelta = Vector3.Distance(initialWorldPosition, reconstructedWorldPosition);
            if (initializationDelta > InitializationPositionEpsilon)
            {
                Debug.LogWarning(
                    "Light initialization changed world position unexpectedly\n" +
                    $"Initial world position: {initialWorldPosition}\n" +
                    $"Reconstructed world position: {reconstructedWorldPosition}\n" +
                    $"Signed normal distance: {signedNormalDistance}\n" +
                    $"Plane normal: {planeNormal}\n" +
                    $"Initialization delta: {initializationDelta}",
                    this);
                return false;
            }

            if (!IsInsideLevelBounds(currentLightPlanePosition))
            {
                Debug.LogWarning(
                    $"The initial light plane position {currentLightPlanePosition} is outside the " +
                    "configured Level Light Bounds. Light input movement is blocked for this Play session. " +
                    "Exit Play Mode and place the initial Light inside the bounds before playing again.",
                    this);
            }

            transform.position = initialWorldPosition;
            preserveInitialPositionForFirstUpdate = true;
            isInitialized = true;
            return true;
        }

        private void ApplyLightPosition()
        {
            if (TryPlaneToWorld(
                    currentLightPlanePosition,
                    signedNormalDistance,
                    out Vector3 worldPosition))
            {
                transform.position = worldPosition;
            }
        }

        private Vector2 ClampToLevelBounds(Vector2 planePosition)
        {
            Vector2 minimum = SafeMinLevelPosition;
            Vector2 maximum = SafeMaxLevelPosition;
            return new Vector2(
                Mathf.Clamp(planePosition.x, minimum.x, maximum.x),
                Mathf.Clamp(planePosition.y, minimum.y, maximum.y));
        }

        private bool IsInsideLevelBounds(Vector2 planePosition)
        {
            Vector2 minimum = SafeMinLevelPosition;
            Vector2 maximum = SafeMaxLevelPosition;
            return planePosition.x >= minimum.x
                && planePosition.x <= maximum.x
                && planePosition.y >= minimum.y
                && planePosition.y <= maximum.y;
        }

        private bool TryWorldToPlane(Vector3 worldPosition, out Vector2 planePosition)
        {
            return ProjectionPlaneCoordinates.TryWorldToPlane2D(
                worldPosition,
                projectionPlane.position,
                projectionPlane.right,
                projectionPlane.up,
                out planePosition);
        }

        private bool TryPlaneToWorld(
            Vector2 planePosition,
            float signedDistance,
            out Vector3 worldPosition)
        {
            if (!ProjectionPlaneCoordinates.TryPlane2DToWorld(
                    planePosition,
                    projectionPlane.position,
                    projectionPlane.right,
                    projectionPlane.up,
                    out worldPosition))
            {
                return false;
            }

            worldPosition += projectionPlane.forward.normalized * signedDistance;
            return true;
        }

        private static Vector2 ReadMoveInput()
        {
            return GameplayInput.LightMove == null
                ? Vector2.zero
                : GameplayInput.LightMove.ReadValue<Vector2>();
        }

        private void OnDrawGizmosSelected()
        {
            if (projectionPlane == null)
            {
                return;
            }

            float distance = Application.isPlaying && isInitialized
                ? signedNormalDistance
                : Vector3.Dot(
                    transform.position - projectionPlane.position,
                    projectionPlane.forward.normalized);
            Vector2 minimum = SafeMinLevelPosition;
            Vector2 maximum = SafeMaxLevelPosition;

            if (!TryPlaneToWorld(new Vector2(minimum.x, minimum.y), distance, out Vector3 bottomLeft) ||
                !TryPlaneToWorld(new Vector2(maximum.x, minimum.y), distance, out Vector3 bottomRight) ||
                !TryPlaneToWorld(new Vector2(maximum.x, maximum.y), distance, out Vector3 topRight) ||
                !TryPlaneToWorld(new Vector2(minimum.x, maximum.y), distance, out Vector3 topLeft))
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.25f, 1f, 0.9f);
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
        }
    }
}
