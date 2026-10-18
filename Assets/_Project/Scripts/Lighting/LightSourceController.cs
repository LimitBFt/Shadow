using ShadowPath.Input;
using ShadowPath.Shadow;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShadowPath.Lighting
{
    public class LightSourceController : MonoBehaviour
    {
        private const float GamepadDeadzone = 0.15f;
        private const float InitializationPositionEpsilon = 0.0001f;

        [Header("References")]
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private LightGameplayArea lightGameplayArea;

        [Header("Level Light Bounds")]
        [SerializeField] private Vector2 minLevelLightPosition = new Vector2(-12f, -5f);
        [SerializeField] private Vector2 maxLevelLightPosition = new Vector2(12f, 8f);

        [Header("Input")]
        [FormerlySerializedAs("moveSpeed")]
        [SerializeField] private float keyboardMoveSpeed = 4f;
        [FormerlySerializedAs("gamepadLightMoveSpeed")]
        [SerializeField] private float gamepadMoveSpeed = 2.5f;

        [Header("Runtime Debug")]
        [SerializeField] private Vector2 currentLightPlanePosition;
        [FormerlySerializedAs("normalDistance")]
        [SerializeField] private float signedNormalDistance;
        [SerializeField] private Vector3 initialWorldPosition;
        [SerializeField] private Vector3 reconstructedWorldPosition;
        [SerializeField] private Vector3 planeNormal;
        [SerializeField] private float initializationDelta;

        private bool isInitialized;
        private bool preserveInitialPositionForFirstUpdate;

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

        private void Update()
        {
            if (preserveInitialPositionForFirstUpdate)
            {
                preserveInitialPositionForFirstUpdate = false;
                transform.position = initialWorldPosition;
                return;
            }

            Vector2 input = ReadMoveInput();
            bool isGamepadInput = GameplayInput.IsLightMoveFromGamepad();
            if (isGamepadInput && input.magnitude <= GamepadDeadzone)
            {
                input = Vector2.zero;
            }
            else if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            if (input.sqrMagnitude <= 0f)
            {
                return;
            }

            float moveSpeed = isGamepadInput ? gamepadMoveSpeed : keyboardMoveSpeed;
            Vector2 proposedPosition = currentLightPlanePosition
                + input * Mathf.Max(0f, moveSpeed) * Time.deltaTime;
            proposedPosition = ClampToLevelBounds(proposedPosition);

            if (lightGameplayArea != null)
            {
                proposedPosition = lightGameplayArea.ClampLightPlanePosition(proposedPosition);
                proposedPosition = ClampToLevelBounds(proposedPosition);
            }

            currentLightPlanePosition = proposedPosition;
            ApplyLightPosition();
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
                    "configured Level Light Bounds. It is preserved until the player moves the light.",
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
