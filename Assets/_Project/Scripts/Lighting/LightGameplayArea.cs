using ShadowPath.Shadow;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShadowPath.Lighting
{
    public class LightGameplayArea : MonoBehaviour
    {
        public enum BoundaryMode { Manual, FromSpotLight }
        public enum LightSizeMode { SpotAngle, TargetRadius }

        private const float SmallBoundaryCorrection = 0.05f;
        private const float MinimumAxisComponent = 0.0001f;
        private const int EllipseSegments = 48;

        [Header("References")]
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private Transform lightSource;
        [SerializeField] private Transform player;
        [SerializeField] private UnityEngine.Camera gameplayCamera;
        [SerializeField] private Light spotLight;

        [Header("Light Size (World Meters)")]
        [SerializeField] private LightSizeMode lightSizeMode = LightSizeMode.TargetRadius;
        [SerializeField, Min(0.01f)] private float targetLightRadius = 4f;
        [SerializeField, Range(0.5f, 0.9f)] private float innerAngleRatio = 0.75f;

        [Header("Gameplay")]
        [SerializeField] private BoundaryMode boundaryMode = BoundaryMode.FromSpotLight;
        [SerializeField, Min(0f)] private float gameplayBoundaryInset = 0.2f;
        [Tooltip("Used only in Manual boundary mode.")]
        [SerializeField] private float gameplayHalfWidth = 5f;
        [Tooltip("Additional margin for Manual mode only. Spot mode uses the Gameplay Boundary Inset circle.")]
        [SerializeField] private float playerSafetyMargin = 0.25f;

        [Header("Legacy UI Mask (not used by Spot Lighting)")]
        [SerializeField] private Vector2 visualRadius = new Vector2(5.8f, 4.2f);
        [SerializeField] private float visualFeather = 0.8f;
        [SerializeField, Range(0f, 1f)] private float outsideDarkness = 0.98f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Vector2 lightPlanePosition;
        [SerializeField, InspectorName("Spot Center Plane Position")] private Vector2 gameplayCenterPlanePosition;
        [SerializeField] private Vector2 playerPlanePosition;
        [SerializeField] private float leftGameplayBoundary;
        [SerializeField] private float rightGameplayBoundary;
        [SerializeField] private bool playerInsideArea;

        [Header("Runtime Debug - Spot Boundary")]
        [SerializeField] private bool spotLightAssigned;
        [SerializeField] private float calculatedSpotAngle;
        [FormerlySerializedAs("spotAngle")]
        [SerializeField] private float actualSpotAngle;
        [SerializeField] private bool targetRadiusAngleLimited;
        [SerializeField] private float lightToPlaneDistance;
        [SerializeField] private float calculatedSpotRadius;
        [SerializeField] private float effectiveGameplayHalfWidth;
        [SerializeField] private bool spotGeometryValid;
        [InspectorName("Visual Horizontal Radius At Player Height")]
        [SerializeField] private float spotHorizontalRadiusAtPlayerHeight;
        [SerializeField] private float effectiveGameplayRadius;
        [SerializeField] private float gameplayHorizontalRadiusAtPlayerHeight;
        [SerializeField] private float playerDeltaX;
        [SerializeField] private float playerDeltaY;
        [SerializeField, InspectorName("Player Circle Distance")] private float playerDistanceFromLightCenter;
        [SerializeField] private bool playerInsideGameplayCircle;
        [SerializeField] private bool playerOutsideVerticalLightRange;
        [SerializeField] private bool boundaryContact;
        [SerializeField] private Vector2 blockedOutwardPlaneVelocity;
        [SerializeField] private Vector2 blockedOutwardPlaneDisplacement;

        public float EffectiveGameplayHalfWidth => SafeGameplayHalfWidth;

        [Header("Runtime Debug - Light Constraint")]
        [SerializeField] private float playerPlaneX;
        [SerializeField] private float initialLightPlaneX;
        [SerializeField] private float allowedLightMinX;
        [SerializeField] private float allowedLightMaxX;
        [SerializeField, InspectorName("Initial Gameplay Constraint Valid")] private bool initialPositionValid;

        private bool lightConstraintInitialized;

        public bool InitialPositionValid => initialPositionValid;
        public Vector2 PlayerPlanePosition => playerPlanePosition;
        public bool TryGetGameplayCircle(out Vector2 center, out float radius)
        {
            radius = SafeGameplayHalfWidth;
            center = default;
            return boundaryMode == BoundaryMode.FromSpotLight && spotGeometryValid
                && TryGetGameplayCenterPlanePosition(out center);
        }

        public UnityEngine.Camera GameplayCamera => gameplayCamera;
        public Vector3 ProjectionPlaneRight =>
            projectionPlane != null ? projectionPlane.right : Vector3.right;
        public Vector3 ProjectionPlaneUp =>
            projectionPlane != null ? projectionPlane.up : Vector3.up;
        public Vector2 VisualRadius
        {
            get
            {
                if (boundaryMode == BoundaryMode.FromSpotLight)
                {
                    RefreshSpotBoundary();
                    return Vector2.one * Mathf.Max(0.0001f, calculatedSpotRadius);
                }
                return new Vector2(Mathf.Max(0.0001f, Mathf.Abs(visualRadius.x)),
                    Mathf.Max(0.0001f, Mathf.Abs(visualRadius.y)));
            }
        }
        public float VisualFeather => Mathf.Max(0f, visualFeather);
        public float OutsideDarkness => Mathf.Clamp01(outsideDarkness);

        private float SafeGameplayHalfWidth
        {
            get
            {
                if (boundaryMode == BoundaryMode.Manual)
                    return effectiveGameplayHalfWidth = Mathf.Max(0f, gameplayHalfWidth);
                RefreshSpotBoundary();
                return effectiveGameplayHalfWidth;
            }
        }

        private void AlignVisualSpotLight()
        {
            if (boundaryMode != BoundaryMode.FromSpotLight || spotLight == null
                || spotLight.type != LightType.Spot || projectionPlane == null || lightSource == null
                || spotLight.transform == lightSource) return;
            float side = Vector3.Dot(lightSource.position - projectionPlane.position, projectionPlane.forward);
            Vector3 direction = side > 0f ? -projectionPlane.forward : projectionPlane.forward;
            spotLight.transform.rotation = Quaternion.LookRotation(direction, projectionPlane.up);
        }

        private bool RefreshSpotBoundary()
        {
            spotLightAssigned = spotLight != null && spotLight.type == LightType.Spot;
            actualSpotAngle = spotLightAssigned ? spotLight.spotAngle : 0f;
            calculatedSpotAngle = actualSpotAngle;
            targetRadiusAngleLimited = false;
            lightToPlaneDistance = calculatedSpotRadius = effectiveGameplayHalfWidth = 0f;
            spotHorizontalRadiusAtPlayerHeight = 0f;
            spotGeometryValid = false;
            if (!spotLightAssigned || projectionPlane == null || lightSource == null) return false;
            float signedDistance = Vector3.Dot(
                lightSource.position - projectionPlane.position, projectionPlane.forward);
            lightToPlaneDistance = Mathf.Abs(signedDistance);
            if (lightSizeMode == LightSizeMode.TargetRadius)
            {
                if (!SpotLightBoundaryCalculator.TryCalculateSpotAngle(signedDistance, targetLightRadius,
                        out calculatedSpotAngle, out targetRadiusAngleLimited)) return false;
                if (!Mathf.Approximately(spotLight.spotAngle, calculatedSpotAngle))
                    spotLight.spotAngle = calculatedSpotAngle;
            }
            actualSpotAngle = spotLight.spotAngle;
            float innerAngle = actualSpotAngle * Mathf.Clamp(innerAngleRatio, 0.5f, 0.9f);
            if (!Mathf.Approximately(spotLight.innerSpotAngle, innerAngle)) spotLight.innerSpotAngle = innerAngle;
            if (!SpotLightBoundaryCalculator.TryCalculate(signedDistance, actualSpotAngle, gameplayBoundaryInset,
                    out calculatedSpotRadius, out float halfWidth)) return false;
            if (lightSizeMode == LightSizeMode.TargetRadius)
            {
                // Normally exactly Target Radius - Inset. At angle limits, never allow a
                // boundary wider than either the requested radius or the actual light footprint.
                float radius = targetRadiusAngleLimited ? Mathf.Min(targetLightRadius, calculatedSpotRadius) : targetLightRadius;
                halfWidth = Mathf.Max(0f, radius - Mathf.Max(0f, gameplayBoundaryInset));
            }
            Vector3 direction = signedDistance > 0f ? -projectionPlane.forward : projectionPlane.forward;
            bool sameOrigin = (spotLight.transform.position - lightSource.position).sqrMagnitude <= 0.000001f;
            bool perpendicular = Vector3.Dot(spotLight.transform.forward, direction) >= 0.9999f;
            bool reachesRim = spotLight.range >= Mathf.Sqrt(
                lightToPlaneDistance * lightToPlaneDistance + calculatedSpotRadius * calculatedSpotRadius);
            spotGeometryValid = sameOrigin && perpendicular && reachesRim;
            if (spotGeometryValid) effectiveGameplayHalfWidth = halfWidth;
            if (player != null)
            {
                float height = Vector3.Dot(player.position - lightSource.position, projectionPlane.up);
                spotHorizontalRadiusAtPlayerHeight = CircleGameplayConstraint.HorizontalRadius(calculatedSpotRadius, height);
            }
            return spotGeometryValid;
        }

        private void Awake()
        {
            AlignVisualSpotLight();
            bool validSpotBoundary = RefreshSpotBoundary();
            if (boundaryMode == BoundaryMode.FromSpotLight && !validSpotBoundary)
            {
                Debug.LogWarning("Spot gameplay boundary is invalid. Assign a Spot Light at the LightSource origin, " +
                    "aimed perpendicular toward ProjectionPlane, with Range reaching the cone rim. " +
                    "No Manual width fallback is used; Light movement initialization will be blocked.", this);
            }
            if (projectionPlane == null || lightSource == null || player == null)
            {
                Debug.LogError(
                    $"{nameof(LightGameplayArea)} requires a Projection Plane, Light Source, and Player.",
                    this);
            }

            if (gameplayCamera == null)
            {
                Debug.LogError(
                    $"{nameof(LightGameplayArea)} requires a Gameplay Camera for the visual mask.",
                    this);
            }
        }

        private void Start()
        {
            InitializeLightConstraint();
            RefreshDebugState();
            if (projectionPlane != null
                && player != null
                && lightSource != null
                && !playerInsideArea)
            {
                Debug.LogWarning(
                    "Player starts outside the illuminated gameplay area. The Player was not moved.",
                    this);
            }
        }

        private void LateUpdate()
        {
            AlignVisualSpotLight();
            RefreshSpotBoundary();
            RefreshDebugState();
        }

        public Vector2 ClampLightPlanePosition(Vector2 proposedLightPlanePosition)
        {
            // Initialization must happen before input, never as a side effect of clamping.
            if (!lightConstraintInitialized || !initialPositionValid)
            {
                return TryGetGameplayCenterPlanePosition(out Vector2 currentLightPosition)
                    ? currentLightPosition
                    : proposedLightPlanePosition;
            }

            if (!TryGetAllowedLightRange(out float minimum, out float maximum))
            {
                return proposedLightPlanePosition;
            }

            if (boundaryMode == BoundaryMode.FromSpotLight)
            {
                if (!TryGetGameplayCenterPlanePosition(out Vector2 current)) return proposedLightPlanePosition;
                TryConstrainLightPlanePosition(current, proposedLightPlanePosition,
                    Vector2.one * -float.MaxValue, Vector2.one * float.MaxValue, out Vector2 final);
                return final;
            }
            proposedLightPlanePosition.x = Mathf.Clamp(
                proposedLightPlanePosition.x, minimum, maximum);
            return proposedLightPlanePosition;
        }

        public bool TryConstrainLightPlanePosition(Vector2 current, Vector2 proposed,
            Vector2 minimum, Vector2 maximum, out Vector2 final)
        {
            final = current;
            if (!lightConstraintInitialized || !initialPositionValid
                || !TryGetAllowedLightRange(out float allowedMin, out float allowedMax)) return false;
            if (boundaryMode == BoundaryMode.Manual)
            {
                float low = Mathf.Max(minimum.x, allowedMin);
                float high = Mathf.Min(maximum.x, allowedMax);
                if (low > high || minimum.y > maximum.y) return false;
                final = new Vector2(Mathf.Clamp(proposed.x, low, high), Mathf.Clamp(proposed.y, minimum.y, maximum.y));
                return true;
            }

            float radius = SafeGameplayHalfWidth;
            if (!CircleGameplayConstraint.TryClamp(proposed.x, proposed.y, playerPlanePosition.x, playerPlanePosition.y,
                    radius, minimum.x, minimum.y, maximum.x, maximum.y, out float x, out float y)) return false;
            if (!CircleGameplayConstraint.Contains(current.x, current.y, playerPlanePosition.x, playerPlanePosition.y, radius))
            {
                // Jumping or an external Player move may temporarily invalidate the current center.
                // Accept only input-sized recovery steps, never project the Light several meters at once.
                bool insideBox = proposed.x >= minimum.x && proposed.x <= maximum.x
                    && proposed.y >= minimum.y && proposed.y <= maximum.y;
                if (insideBox && (proposed - playerPlanePosition).sqrMagnitude
                    <= (current - playerPlanePosition).sqrMagnitude)
                    final = proposed;
                return true;
            }
            final = new Vector2(x, y);
            return true;
        }

        public bool InitializeLightConstraint()
        {
            if (lightConstraintInitialized)
            {
                return initialPositionValid;
            }

            lightConstraintInitialized = true;
            initialPositionValid = TryGetAllowedLightRange(out _, out _)
                && TryGetGameplayCenterPlanePosition(out lightPlanePosition);
            initialLightPlaneX = lightPlanePosition.x;
            initialPositionValid = initialPositionValid
                && initialLightPlaneX >= allowedLightMinX
                && initialLightPlaneX <= allowedLightMaxX;
            if (initialPositionValid && boundaryMode == BoundaryMode.FromSpotLight)
            {
                initialPositionValid = CircleGameplayConstraint.Contains(lightPlanePosition.x, lightPlanePosition.y,
                    playerPlanePosition.x, playerPlanePosition.y, SafeGameplayHalfWidth);
            }

            if (!initialPositionValid)
            {
                Debug.LogWarning(
                    $"{nameof(LightGameplayArea)}: Initial Light gameplay constraint is INVALID.\n" +
                    $"Player Plane X: {playerPlaneX:F5}\n" +
                    $"Initial Light Plane X: {initialLightPlaneX:F5}\n" +
                    $"Player Plane Position: {playerPlanePosition}; Initial Light Plane Position: {lightPlanePosition}\n" +
                    $"Gameplay radius: {SafeGameplayHalfWidth:F5}; 2D distance: {Vector2.Distance(playerPlanePosition, lightPlanePosition):F5}\n" +
                    $"Allowed Light X: [{allowedLightMinX:F5}, {allowedLightMaxX:F5}]\n" +
                    "Check Projection Plane, Player and Light Source references. " +
                    "Light was NOT moved. Light input movement is blocked for this Play session. " +
                    "Exit Play Mode, place the initial Light inside the allowed range in Scene View, then Play again.",
                    this);
            }

            return initialPositionValid;
        }

        public bool TryGetAllowedLightRange(out float minimum, out float maximum)
        {
            minimum = maximum = 0f;
            if (boundaryMode == BoundaryMode.FromSpotLight && !RefreshSpotBoundary()) return false;
            if (player == null || !TryWorldToPlane(player.position, out playerPlanePosition))
            {
                return false;
            }

            float usableHalfWidth = Mathf.Max(
                0f,
                SafeGameplayHalfWidth - Mathf.Max(0f, playerSafetyMargin));
            if (boundaryMode == BoundaryMode.FromSpotLight)
            {
                if (!TryGetGameplayCenterPlanePosition(out Vector2 center)) return false;
                usableHalfWidth = CircleGameplayConstraint.HorizontalRadius(SafeGameplayHalfWidth,
                    center.y - playerPlanePosition.y);
            }
            playerPlaneX = playerPlanePosition.x;
            minimum = allowedLightMinX = playerPlaneX - usableHalfWidth;
            maximum = allowedLightMaxX = playerPlaneX + usableHalfWidth;
            return true;
        }

        public Vector3 ConstrainPlayerWorldPosition(
            Vector3 currentWorldPosition,
            Vector3 proposedWorldPosition)
        {
            if (!TryGetGameplayCenterPlanePosition(out Vector2 centerPosition)
                || !TryWorldToPlane(currentWorldPosition, out Vector2 currentPosition)
                || !TryWorldToPlane(proposedWorldPosition, out Vector2 proposedPosition))
            {
                return proposedWorldPosition;
            }

            if (boundaryMode == BoundaryMode.FromSpotLight)
            {
                Vector2 delta = proposedPosition - currentPosition;
                CircleGameplayConstraint.ConstrainMotion(currentPosition.x, currentPosition.y, delta.x, delta.y,
                    centerPosition.x, centerPosition.y, SafeGameplayHalfWidth, out float x, out float y);
                Vector2 correction = new Vector2(x, y) - proposedPosition;
                return TryPlaneDeltaToWorldXY(correction, out Vector3 worldCorrection)
                    ? proposedWorldPosition + worldCorrection : proposedWorldPosition;
            }

            float halfWidth = SafeGameplayHalfWidth;
            float leftBoundary = centerPosition.x - halfWidth;
            float rightBoundary = centerPosition.x + halfWidth;
            float constrainedX = ConstrainAxisPosition(currentPosition.x, proposedPosition.x, leftBoundary, rightBoundary);
            float planeCorrection = constrainedX - proposedPosition.x;
            Vector3 planeRight = projectionPlane.right.normalized;
            if (Mathf.Abs(planeRight.x) <= MinimumAxisComponent)
            {
                return proposedWorldPosition;
            }

            Vector3 constrainedWorldPosition = proposedWorldPosition;
            constrainedWorldPosition.x += planeCorrection / planeRight.x;
            return constrainedWorldPosition;
        }

        // PlayerController supplies the complete frame motion once: input/jump velocity,
        // predicted gravity and all external/ShadowRide displacement are constrained together.
        public void ConstrainPlayerMovement(Vector3 currentWorldPosition, Vector3 externalDisplacement,
            Vector3 proposedVelocity, float deltaTime, out Vector3 finalExternalDisplacement, out Vector3 finalVelocity)
        {
            finalExternalDisplacement = externalDisplacement;
            finalVelocity = proposedVelocity;
            boundaryContact = false;
            blockedOutwardPlaneVelocity = blockedOutwardPlaneDisplacement = Vector2.zero;
            if (deltaTime <= 0f) return;
            if (boundaryMode == BoundaryMode.Manual)
            {
                Vector3 target = ConstrainPlayerWorldPosition(currentWorldPosition, currentWorldPosition + externalDisplacement);
                finalExternalDisplacement = target - currentWorldPosition;
                finalVelocity = ConstrainPlayerVelocity(target, proposedVelocity, deltaTime);
                return;
            }
            Vector3 proposedWorldPosition = currentWorldPosition + externalDisplacement + proposedVelocity * deltaTime;
            if (!TryGetGameplayCenterPlanePosition(out Vector2 center)
                || !TryWorldToPlane(currentWorldPosition, out Vector2 current)
                || !TryWorldToPlane(proposedWorldPosition, out Vector2 proposed)) return;

            Vector2 movement = proposed - current;
            boundaryContact = CircleGameplayConstraint.ConstrainMotion(current.x, current.y, movement.x, movement.y,
                center.x, center.y, SafeGameplayHalfWidth, out float x, out float y);
            if (!boundaryContact) return;
            Vector2 final = new Vector2(x, y);
            Vector2 planeVelocity = new Vector2(Vector3.Dot(proposedVelocity, projectionPlane.right),
                Vector3.Dot(proposedVelocity, projectionPlane.up));
            CircleGameplayConstraint.RemoveOutward(final.x - center.x, final.y - center.y,
                planeVelocity.x, planeVelocity.y, out float vx, out float vy);
            if (SafeGameplayHalfWidth <= 0f && (final - center).sqrMagnitude <= 0.000001f) vx = vy = 0f;
            blockedOutwardPlaneVelocity = planeVelocity - new Vector2(vx, vy);
            blockedOutwardPlaneDisplacement = proposed - final;
            if (!TryPlaneDeltaToWorldXY(-blockedOutwardPlaneVelocity, out Vector3 velocityCorrection)
                || !TryPlaneDeltaToWorldXY(-blockedOutwardPlaneDisplacement, out Vector3 positionCorrection)) return;
            finalVelocity += velocityCorrection;
            // Reach the contact point this frame while the stored velocity is already tangent/inward.
            // This is the external MovePosition part of the existing dynamic Rigidbody movement path.
            finalExternalDisplacement += positionCorrection - velocityCorrection * deltaTime;
        }

        private bool TryPlaneDeltaToWorldXY(Vector2 delta, out Vector3 worldDelta)
        {
            worldDelta = Vector3.zero;
            Vector3 right = projectionPlane.right;
            Vector3 up = projectionPlane.up;
            float determinant = right.x * up.y - right.y * up.x;
            if (Mathf.Abs(determinant) <= MinimumAxisComponent) return false;
            // Player freezes World Z; solve the two plane-coordinate corrections in World XY.
            worldDelta.x = (up.y * delta.x - right.y * delta.y) / determinant;
            worldDelta.y = (-up.x * delta.x + right.x * delta.y) / determinant;
            return true;
        }

        public Vector3 ConstrainPlayerVelocity(
            Vector3 currentWorldPosition,
            Vector3 proposedVelocity,
            float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return proposedVelocity;
            }
            if (boundaryMode == BoundaryMode.FromSpotLight)
            {
                ConstrainPlayerMovement(currentWorldPosition, Vector3.zero, proposedVelocity, deltaTime,
                    out _, out Vector3 velocity);
                return velocity;
            }

            Vector3 predictedPosition = currentWorldPosition + proposedVelocity * deltaTime;
            Vector3 constrainedPosition = ConstrainPlayerWorldPosition(
                currentWorldPosition,
                predictedPosition);
            Vector3 constrainedVelocity = proposedVelocity
                + (constrainedPosition - predictedPosition) / deltaTime;
            constrainedVelocity.y = proposedVelocity.y;
            constrainedVelocity.z = proposedVelocity.z;
            return constrainedVelocity;
        }

        public bool TryGetGameplayCenterWorldPosition(out Vector3 centerWorldPosition)
        {
            centerWorldPosition = default;
            return TryGetGameplayCenterPlanePosition(out Vector2 centerPlanePosition)
                && ProjectionPlaneCoordinates.TryPlane2DToWorld(
                    centerPlanePosition,
                    projectionPlane.position,
                    projectionPlane.right,
                    projectionPlane.up,
                    out centerWorldPosition);
        }

        private bool TryGetGameplayCenterPlanePosition(out Vector2 centerPlanePosition)
        {
            centerPlanePosition = default;
            return lightSource != null
                && TryWorldToPlane(lightSource.position, out centerPlanePosition);
        }

        private bool TryWorldToPlane(Vector3 worldPosition, out Vector2 planePosition)
        {
            planePosition = default;
            return projectionPlane != null
                && ProjectionPlaneCoordinates.TryWorldToPlane2D(
                    worldPosition,
                    projectionPlane.position,
                    projectionPlane.right,
                    projectionPlane.up,
                    out planePosition);
        }

        private void RefreshDebugState()
        {
            TryGetAllowedLightRange(out _, out _);
            bool hasLight = lightSource != null
                && TryWorldToPlane(lightSource.position, out lightPlanePosition);
            if (hasLight)
            {
                gameplayCenterPlanePosition = lightPlanePosition;
            }

            bool hasPlayer = player != null
                && TryWorldToPlane(player.position, out playerPlanePosition);
            float radius = SafeGameplayHalfWidth;
            effectiveGameplayRadius = radius;
            Vector2 delta = playerPlanePosition - gameplayCenterPlanePosition;
            playerDeltaX = delta.x;
            playerDeltaY = delta.y;
            playerDistanceFromLightCenter = delta.magnitude;
            gameplayHorizontalRadiusAtPlayerHeight = boundaryMode == BoundaryMode.FromSpotLight
                ? CircleGameplayConstraint.HorizontalRadius(radius, delta.y) : radius;
            leftGameplayBoundary = gameplayCenterPlanePosition.x - gameplayHorizontalRadiusAtPlayerHeight;
            rightGameplayBoundary = gameplayCenterPlanePosition.x + gameplayHorizontalRadiusAtPlayerHeight;
            playerOutsideVerticalLightRange = boundaryMode == BoundaryMode.FromSpotLight && Mathf.Abs(delta.y) >= radius;
            playerInsideGameplayCircle = hasLight && hasPlayer
                && CircleGameplayConstraint.Contains(playerPlanePosition.x, playerPlanePosition.y,
                    gameplayCenterPlanePosition.x, gameplayCenterPlanePosition.y, radius);
            playerInsideArea = hasLight
                && hasPlayer
                && playerPlanePosition.x >= leftGameplayBoundary
                && playerPlanePosition.x <= rightGameplayBoundary
                && (boundaryMode == BoundaryMode.Manual || playerInsideGameplayCircle);
        }

        private static float ConstrainAxisPosition(
            float currentPosition,
            float proposedPosition,
            float minimum,
            float maximum)
        {
            if (currentPosition >= minimum && currentPosition <= maximum)
            {
                return Mathf.Clamp(proposedPosition, minimum, maximum);
            }

            if (currentPosition > maximum)
            {
                if (currentPosition - maximum <= SmallBoundaryCorrection)
                {
                    return Mathf.Clamp(proposedPosition, minimum, maximum);
                }

                return proposedPosition >= currentPosition
                    ? currentPosition
                    : Mathf.Max(proposedPosition, minimum);
            }

            if (minimum - currentPosition <= SmallBoundaryCorrection)
            {
                return Mathf.Clamp(proposedPosition, minimum, maximum);
            }

            return proposedPosition <= currentPosition
                ? currentPosition
                : Mathf.Min(proposedPosition, maximum);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || projectionPlane == null || lightSource == null)
            {
                return;
            }

            if (!TryGetGameplayCenterPlanePosition(out Vector2 centerPlanePosition)
                || !TryPlaneToWorld(centerPlanePosition, out Vector3 centerWorldPosition))
            {
                return;
            }

            Vector2 safeVisualRadius = VisualRadius;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(centerWorldPosition, 0.12f);

            if (boundaryMode == BoundaryMode.FromSpotLight)
            {
                RefreshDebugState();
                Gizmos.color = Color.white;
                Gizmos.DrawLine(lightSource.position, centerWorldPosition);
                Vector3 rimOffset = projectionPlane.right.normalized * calculatedSpotRadius;
                Gizmos.DrawLine(lightSource.position, centerWorldPosition - rimOffset);
                Gizmos.DrawLine(lightSource.position, centerWorldPosition + rimOffset);
                Gizmos.DrawWireSphere(centerWorldPosition - rimOffset, 0.1f);
                Gizmos.DrawWireSphere(centerWorldPosition + rimOffset, 0.1f);
                Gizmos.color = new Color(1f, 0.55f, 0f, 0.9f);
                DrawPlaneCircle(centerPlanePosition, calculatedSpotRadius);
                Gizmos.color = Color.yellow;
                DrawPlaneCircle(centerPlanePosition, effectiveGameplayRadius);
                Gizmos.color = Color.gray;
                DrawPlaneLine(new Vector2(centerPlanePosition.x - calculatedSpotRadius, playerPlanePosition.y),
                    new Vector2(centerPlanePosition.x + calculatedSpotRadius, playerPlanePosition.y));
                Gizmos.color = playerOutsideVerticalLightRange ? Color.red : Color.green;
                DrawPlaneLine(new Vector2(leftGameplayBoundary, playerPlanePosition.y),
                    new Vector2(rightGameplayBoundary, playerPlanePosition.y));
                if (TryPlaneToWorld(new Vector2(leftGameplayBoundary, playerPlanePosition.y), out Vector3 left))
                    Gizmos.DrawWireSphere(left, 0.1f);
                if (TryPlaneToWorld(new Vector2(rightGameplayBoundary, playerPlanePosition.y), out Vector3 right))
                    Gizmos.DrawWireSphere(right, 0.1f);
                return;
            }

            if (TryGetAllowedLightRange(out float minimum, out float maximum))
            {
                // Cyan segment and end caps show legal Light X at its current plane Y.
                Gizmos.color = Color.cyan;
                DrawPlaneLine(new Vector2(minimum, centerPlanePosition.y),
                    new Vector2(maximum, centerPlanePosition.y));
                DrawPlaneLine(new Vector2(minimum, centerPlanePosition.y - 0.3f),
                    new Vector2(minimum, centerPlanePosition.y + 0.3f));
                DrawPlaneLine(new Vector2(maximum, centerPlanePosition.y - 0.3f),
                    new Vector2(maximum, centerPlanePosition.y + 0.3f));
            }

            float halfHeight = safeVisualRadius.y;
            float leftBoundary = centerPlanePosition.x - SafeGameplayHalfWidth;
            float rightBoundary = centerPlanePosition.x + SafeGameplayHalfWidth;
            Gizmos.color = Color.yellow;
            DrawPlaneLine(
                new Vector2(leftBoundary, centerPlanePosition.y - halfHeight),
                new Vector2(leftBoundary, centerPlanePosition.y + halfHeight));
            DrawPlaneLine(
                new Vector2(rightBoundary, centerPlanePosition.y - halfHeight),
                new Vector2(rightBoundary, centerPlanePosition.y + halfHeight));

            Gizmos.color = new Color(1f, 0.55f, 0f, 0.9f);
            Vector2 previousPoint = centerPlanePosition
                + new Vector2(safeVisualRadius.x, 0f);
            for (int i = 1; i <= EllipseSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / EllipseSegments;
                Vector2 currentPoint = centerPlanePosition + new Vector2(
                    Mathf.Cos(angle) * safeVisualRadius.x,
                    Mathf.Sin(angle) * safeVisualRadius.y);
                DrawPlaneLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }

        private bool TryPlaneToWorld(Vector2 planePosition, out Vector3 worldPosition)
        {
            return ProjectionPlaneCoordinates.TryPlane2DToWorld(
                planePosition,
                projectionPlane.position,
                projectionPlane.right,
                projectionPlane.up,
                out worldPosition);
        }

        private void DrawPlaneCircle(Vector2 center, float radius)
        {
            Vector2 previous = center + Vector2.right * radius;
            for (int i = 1; i <= EllipseSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / EllipseSegments;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                DrawPlaneLine(previous, next);
                previous = next;
            }
        }

        private void DrawPlaneLine(Vector2 from, Vector2 to)
        {
            if (TryPlaneToWorld(from, out Vector3 worldFrom)
                && TryPlaneToWorld(to, out Vector3 worldTo))
            {
                Gizmos.DrawLine(worldFrom, worldTo);
            }
        }
    }
}
