using ShadowPath.Shadow;
using UnityEngine;

namespace ShadowPath.Lighting
{
    public class LightGameplayArea : MonoBehaviour
    {
        private const float SmallBoundaryCorrection = 0.05f;
        private const float MinimumAxisComponent = 0.0001f;
        private const int EllipseSegments = 48;

        [Header("References")]
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private Transform lightSource;
        [SerializeField] private Transform player;
        [SerializeField] private UnityEngine.Camera gameplayCamera;

        [Header("Gameplay")]
        [SerializeField] private float gameplayHalfWidth = 5f;
        [SerializeField] private float playerSafetyMargin = 0.25f;

        [Header("Visual")]
        [SerializeField] private Vector2 visualRadius = new Vector2(5.8f, 4.2f);
        [SerializeField] private float visualFeather = 0.8f;
        [SerializeField, Range(0f, 1f)] private float outsideDarkness = 0.98f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Vector2 lightPlanePosition;
        [SerializeField] private Vector2 gameplayCenterPlanePosition;
        [SerializeField] private Vector2 playerPlanePosition;
        [SerializeField] private float leftGameplayBoundary;
        [SerializeField] private float rightGameplayBoundary;
        [SerializeField] private bool playerInsideArea;

        public UnityEngine.Camera GameplayCamera => gameplayCamera;
        public Vector3 ProjectionPlaneRight =>
            projectionPlane != null ? projectionPlane.right : Vector3.right;
        public Vector3 ProjectionPlaneUp =>
            projectionPlane != null ? projectionPlane.up : Vector3.up;
        public Vector2 VisualRadius => new Vector2(
            Mathf.Max(0.0001f, Mathf.Abs(visualRadius.x)),
            Mathf.Max(0.0001f, Mathf.Abs(visualRadius.y)));
        public float VisualFeather => Mathf.Max(0f, visualFeather);
        public float OutsideDarkness => Mathf.Clamp01(outsideDarkness);

        private float SafeGameplayHalfWidth => Mathf.Max(0f, gameplayHalfWidth);

        private void Awake()
        {
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
            RefreshDebugState();
        }

        public Vector2 ClampLightPlanePosition(Vector2 proposedLightPlanePosition)
        {
            if (player == null
                || !TryWorldToPlane(player.position, out Vector2 currentPlayerPosition))
            {
                return proposedLightPlanePosition;
            }

            float usableHalfWidth = Mathf.Max(
                0f,
                SafeGameplayHalfWidth - Mathf.Max(0f, playerSafetyMargin));
            proposedLightPlanePosition.x = Mathf.Clamp(
                proposedLightPlanePosition.x,
                currentPlayerPosition.x - usableHalfWidth,
                currentPlayerPosition.x + usableHalfWidth);
            return proposedLightPlanePosition;
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

            float leftBoundary = centerPosition.x - SafeGameplayHalfWidth;
            float rightBoundary = centerPosition.x + SafeGameplayHalfWidth;
            float constrainedX = ConstrainAxisPosition(
                currentPosition.x,
                proposedPosition.x,
                leftBoundary,
                rightBoundary);
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

        public Vector3 ConstrainPlayerVelocity(
            Vector3 currentWorldPosition,
            Vector3 proposedVelocity,
            float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return proposedVelocity;
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
            bool hasLight = lightSource != null
                && TryWorldToPlane(lightSource.position, out lightPlanePosition);
            if (hasLight)
            {
                gameplayCenterPlanePosition = lightPlanePosition;
                leftGameplayBoundary = gameplayCenterPlanePosition.x - SafeGameplayHalfWidth;
                rightGameplayBoundary = gameplayCenterPlanePosition.x + SafeGameplayHalfWidth;
            }

            bool hasPlayer = player != null
                && TryWorldToPlane(player.position, out playerPlanePosition);
            playerInsideArea = hasLight
                && hasPlayer
                && playerPlanePosition.x >= leftGameplayBoundary
                && playerPlanePosition.x <= rightGameplayBoundary;
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
