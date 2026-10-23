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
        [SerializeField] private bool useLevelLightBounds = false;
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

        [Header("Manual Control")]
        [SerializeField] private bool manualControlEnabled = true;

        [Header("Auto Follow")]
        [SerializeField] private bool autoFollowPlayer = false;
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool followHorizontal = true;
        [SerializeField] private bool followVertical = false;
        [Tooltip("Dead zone half-width on each enabled Projection Plane axis, in world meters.")]
        [SerializeField, Min(0f)] private float followDeadZone = 3f;
        [SerializeField, Min(0.0001f)] private float followSmoothTime = 0.8f;
        [SerializeField, Min(0f)] private float followMaxSpeed = 4f;

        [Header("Runtime Debug")]
        [SerializeField] private bool autoFollowActive;
        [SerializeField] private Vector2 followTargetPlanePosition;
        [SerializeField] private Vector2 followDelta;
        [SerializeField] private bool insideDeadZone;
        [SerializeField] private Vector2 autoFollowTargetPosition;
        [SerializeField] private Vector2 autoFollowVelocity;
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
        [SerializeField, InspectorName("Level Bounds Blocking")] private bool isLevelBoundsBlocking;
        [SerializeField, InspectorName("Mutual Circle Blocking")] private bool isGameplayCircleBlocking;
        [SerializeField] private float playerLightDistance;
        [SerializeField] private float effectiveGameplayRadius;
        [SerializeField] private bool movementReducesDistance;
        [SerializeField] private Vector2 blockedOutwardComponent;

        private bool isInitialized;
        private bool preserveInitialPositionForFirstUpdate;
        private bool initialMovementValid;
        private Vector2 smoothedKeyboardInput;
        private bool previousAutoFollowPlayer;
        private bool previousManualControlEnabled = true;

        public bool AutoFollowPlayer => autoFollowPlayer;
        public bool ManualControlEnabled => manualControlEnabled;

        public void SetManualControlEnabled(bool enabled)
        {
            manualControlEnabled = enabled;
            SynchronizeManualControl();
        }

        private void SynchronizeManualControl()
        {
            if (previousManualControlEnabled == manualControlEnabled) return;
            previousManualControlEnabled = manualControlEnabled;
            smoothedKeyboardInput = rawInput = processedInput = Vector2.zero;
            // Changing manual permission must not interrupt an active auto-follow velocity.
            if (!autoFollowPlayer) currentPlaneVelocity = desiredPlaneVelocity = Vector2.zero;
        }

        // Read the same action as movement, without relying on Update or scaled deltaTime.
        public Vector2 RawManualInput => isActiveAndEnabled && manualControlEnabled && !autoFollowPlayer
            ? ReadMoveInput() : Vector2.zero;

        // Reuse the movement pipeline's device dead zone and keyboard processing.
        // Raw input also has to be present, so residual smoothing cannot complete a tutorial.
        public bool HasManualLightInput => isActiveAndEnabled && manualControlEnabled && initialMovementValid && !autoFollowPlayer
            && rawInput.sqrMagnitude > 0.0001f && processedInput.sqrMagnitude > 0.0001f;

        public void SetAutoFollow(bool enabled)
        {
            autoFollowPlayer = enabled;
            SynchronizeControlMode();
        }

        private void SynchronizeControlMode()
        {
            if (previousAutoFollowPlayer == autoFollowPlayer) return;
            previousAutoFollowPlayer = autoFollowPlayer;
            ResetMovementVelocity();
            autoFollowActive = false;
            followTargetPlanePosition = followDelta = Vector2.zero;
            insideDeadZone = false;
            autoFollowTargetPosition = currentLightPlanePosition;
        }

        private void ResetMovementVelocity()
        {
            currentPlaneVelocity = desiredPlaneVelocity = autoFollowVelocity = Vector2.zero;
            smoothedKeyboardInput = rawInput = processedInput = Vector2.zero;
        }

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
        }

        private void RefreshGameplayConstraintDebug()
        {
            initialMovementValid = lightGameplayArea == null;
            if (lightGameplayArea != null)
            {
                lightGameplayArea.TryGetAllowedLightRange(out allowedLightMinX, out allowedLightMaxX);
                initialMovementValid = lightGameplayArea.TryGetMutualCircle(out playerPlanePosition,
                    out Vector2 center, out effectiveGameplayRadius);
                playerLightDistance = Vector2.Distance(playerPlanePosition, center);
            }
        }

        private void Update()
        {
            // Inspector changes and the public API share the same position-preserving handover.
            SynchronizeControlMode();
            SynchronizeManualControl();
            autoFollowActive = false;
            isLevelBoundsBlocking = isGameplayCircleBlocking = movementReducesDistance = false;
            blockedOutwardComponent = Vector2.zero;
            RefreshGameplayConstraintDebug();
            if (!initialMovementValid)
            {
                ResetMovementVelocity();
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
            if (autoFollowPlayer)
            {
                rawProposedLightPosition = GetAutoFollowProposedPosition(dt);
            }
            else if (manualControlEnabled)
            {
                UpdateManualVelocity(dt);
                rawProposedLightPosition = currentLightPlanePosition + currentPlaneVelocity * dt;
            }
            else
            {
                ResetMovementVelocity();
                rawProposedLightPosition = finalLightPosition = currentLightPlanePosition;
                lastClampCorrection = Vector2.zero;
                return;
            }
            ApplyConstrainedMovement();
        }

        private void UpdateManualVelocity(float dt)
        {
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
        }

        private Vector2 GetAutoFollowProposedPosition(float dt)
        {
            rawInput = processedInput = smoothedKeyboardInput = Vector2.zero;
            desiredPlaneVelocity = currentPlaneVelocity = Vector2.zero;
            autoFollowTargetPosition = currentLightPlanePosition;
            followTargetPlanePosition = followDelta = Vector2.zero;
            insideDeadZone = false;
            if (followTarget == null || !TryWorldToPlane(followTarget.position, out followTargetPlanePosition))
            {
                autoFollowVelocity = Vector2.zero;
                return currentLightPlanePosition;
            }

            Vector2 center = currentLightPlanePosition;
            if (lightGameplayArea != null
                && lightGameplayArea.TryGetGameplayCenterWorldPosition(out Vector3 centerWorld))
            {
                TryWorldToPlane(centerWorld, out center);
            }
            autoFollowActive = true;
            followDelta = followTargetPlanePosition - center;
            autoFollowTargetPosition = CalculateAutoFollowTarget(currentLightPlanePosition, center,
                followTargetPlanePosition, followHorizontal, followVertical, followDeadZone);
            Vector2 excess = autoFollowTargetPosition - currentLightPlanePosition;
            insideDeadZone = excess == Vector2.zero;
            // An axis inside its dead zone must not retain motion from a previous target.
            if (!followHorizontal || excess.x == 0f) autoFollowVelocity.x = 0f;
            if (!followVertical || excess.y == 0f) autoFollowVelocity.y = 0f;
            if (insideDeadZone || followMaxSpeed <= 0f)
            {
                autoFollowVelocity = Vector2.zero;
                return currentLightPlanePosition;
            }
            return Vector2.SmoothDamp(currentLightPlanePosition, autoFollowTargetPosition,
                ref autoFollowVelocity, Mathf.Max(0.0001f, followSmoothTime),
                Mathf.Max(0f, followMaxSpeed), dt);
        }

        internal static Vector2 CalculateAutoFollowTarget(Vector2 lightPosition, Vector2 center,
            Vector2 target, bool horizontal, bool vertical, float deadZone)
        {
            Vector2 delta = target - center;
            float zone = Mathf.Max(0f, deadZone);
            return lightPosition + new Vector2(
                horizontal ? delta.x - Mathf.Clamp(delta.x, -zone, zone) : 0f,
                vertical ? delta.y - Mathf.Clamp(delta.y, -zone, zone) : 0f);
        }

        private void ApplyConstrainedMovement()
        {
            Vector2 proposedPosition = useLevelLightBounds
                ? ClampToLevelBounds(rawProposedLightPosition) : rawProposedLightPosition;
            isLevelBoundsBlocking = useLevelLightBounds
                && (proposedPosition - rawProposedLightPosition).sqrMagnitude > 0.0000000001f;
            isGameplayCircleBlocking = false;
            movementReducesDistance = false;
            blockedOutwardComponent = Vector2.zero;

            if (lightGameplayArea != null)
            {
                if (lightGameplayArea.TryGetMutualCircle(out playerPlanePosition, out _, out effectiveGameplayRadius))
                    movementReducesDistance = (proposedPosition - playerPlanePosition).sqrMagnitude
                        < (currentLightPlanePosition - playerPlanePosition).sqrMagnitude;
                Vector2 constrainedPosition;
                bool constrained = useLevelLightBounds
                    ? lightGameplayArea.TryConstrainLightPlanePosition(currentLightPlanePosition,
                        proposedPosition, SafeMinLevelPosition, SafeMaxLevelPosition, out constrainedPosition)
                    : lightGameplayArea.TryConstrainLightPlanePosition(currentLightPlanePosition,
                        proposedPosition, out constrainedPosition);
                if (!constrained)
                {
                    finalLightPosition = currentLightPlanePosition;
                    lastClampCorrection = finalLightPosition - rawProposedLightPosition;
                    isGameplayCircleBlocking = true;
                    currentPlaneVelocity = smoothedKeyboardInput = Vector2.zero;
                    autoFollowVelocity = Vector2.zero;
                    return;
                }

                isGameplayCircleBlocking = (constrainedPosition - proposedPosition).sqrMagnitude > 0.0000000001f;
                Vector2 outward = (currentLightPlanePosition - playerPlanePosition).normalized;
                blockedOutwardComponent = outward * Mathf.Max(0f,
                    Vector2.Dot(proposedPosition - constrainedPosition, outward));
                proposedPosition = constrainedPosition;
            }

            finalLightPosition = proposedPosition;
            lastClampCorrection = finalLightPosition - rawProposedLightPosition;
            CleanBlockedVelocity(finalLightPosition);
            currentLightPlanePosition = proposedPosition;
            ApplyLightPosition();
            if (lightGameplayArea != null)
                playerLightDistance = Vector2.Distance(currentLightPlanePosition, playerPlanePosition);
        }

        private void CleanBlockedVelocity(Vector2 position)
        {
            // Infinite limits disable all rectangular contacts, including input-history cleanup.
            Vector2 minimum = useLevelLightBounds ? SafeMinLevelPosition : Vector2.one * float.NegativeInfinity;
            Vector2 maximum = useLevelLightBounds ? SafeMaxLevelPosition : Vector2.one * float.PositiveInfinity;
            Vector2 center = Vector2.zero;
            float radius = 0f;
            bool hasCircle = lightGameplayArea != null
                && lightGameplayArea.TryGetMutualCircle(out center, out _, out radius);
            // Legal Light centers form the same-radius disk centered at the Player, not at the Light.
            LightMovementMath.RemoveBlockedVelocity(currentPlaneVelocity.x, currentPlaneVelocity.y, position.x, position.y,
                minimum.x, minimum.y, maximum.x, maximum.y, hasCircle, center.x, center.y, radius,
                out float vx, out float vy);
            currentPlaneVelocity = new Vector2(vx, vy);
            if (autoFollowPlayer)
            {
                LightMovementMath.RemoveBlockedVelocity(autoFollowVelocity.x, autoFollowVelocity.y,
                    position.x, position.y, minimum.x, minimum.y, maximum.x, maximum.y,
                    hasCircle, center.x, center.y, radius, out float fx, out float fy);
                autoFollowVelocity = new Vector2(fx, fy);
            }
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
            ResetMovementVelocity();
            autoFollowActive = false;
            isLevelBoundsBlocking = isGameplayCircleBlocking = false;
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
            followDeadZone = Mathf.Max(0f, followDeadZone);
            followSmoothTime = Mathf.Max(0.0001f, followSmoothTime);
            followMaxSpeed = Mathf.Max(0f, followMaxSpeed);
            if (!useLevelLightBounds) isLevelBoundsBlocking = false;
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

            if (useLevelLightBounds && !IsInsideLevelBounds(currentLightPlanePosition))
            {
                Debug.LogWarning(
                    $"The initial light plane position {currentLightPlanePosition} is outside the " +
                    "configured Level Light Bounds. No initialization teleport was applied. " +
                    "Recovery toward the bounds remains input-sized. Disable Use Level Light Bounds " +
                    "or place the initial Light inside the level bounds.",
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
                CircleGameplayConstraint.ConstrainHorizontal(currentLightPlanePosition.x, planePosition.x, minimum.x, maximum.x),
                CircleGameplayConstraint.ConstrainHorizontal(currentLightPlanePosition.y, planePosition.y, minimum.y, maximum.y));
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
            if (projectionPlane == null || !useLevelLightBounds)
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
