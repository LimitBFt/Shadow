using ShadowPath.Shadow;
using UnityEngine;

namespace ShadowPath.Player
{
    public class ShadowRideController : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private ShadowRideProbe probe;
        [SerializeField, Min(0f)] private float maxCarryDistancePerStep = 0.5f;
        [SerializeField, Min(0)] private int requiredStableGeometryFixedSteps = 2;
        [SerializeField] private bool isAttached;
        [SerializeField] private GeneratedShadow attachedShadow;
        [SerializeField] private float attachedNormalizedX;
        [SerializeField] private int observedGeometryVersion;
        [SerializeField] private int currentShadowGeometryVersion;
        [SerializeField] private int stableGeometryFixedSteps;
        [SerializeField] private Vector3 previousSurfaceWorldPoint;
        [SerializeField] private Vector3 lastCarryDelta;

        private bool warnedLargeCarryDelta;

        public bool IsAttached => isAttached;
        public GeneratedShadow AttachedShadow => attachedShadow;
        public float AttachedNormalizedX => attachedNormalizedX;
        public int ObservedGeometryVersion => observedGeometryVersion;
        public int CurrentShadowGeometryVersion => currentShadowGeometryVersion;
        public int StableGeometryFixedSteps => stableGeometryFixedSteps;
        public Vector3 PreviousSurfaceWorldPoint => previousSurfaceWorldPoint;
        public Vector3 LastCarryDelta => lastCarryDelta;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (probe == null)
            {
                probe = GetComponent<ShadowRideProbe>();
            }
        }

        private void FixedUpdate()
        {
            if (!isAttached)
            {
                TryAttachFromProbe();
                return;
            }

            if (!IsAttachedShadowUsable())
            {
                Detach();
                return;
            }

            int currentGeometryVersion = attachedShadow.GeometryVersion;
            currentShadowGeometryVersion = currentGeometryVersion;
            if (currentGeometryVersion != observedGeometryVersion)
            {
                CarryWithGeometryChange(currentGeometryVersion);
                return;
            }

            RefreshAttachmentFromStableProbe();
        }

        private void TryAttachFromProbe()
        {
            if (playerController == null || probe == null || !probe.HasShadowSupport)
            {
                Detach();
                return;
            }

            GeneratedShadow shadow = probe.CurrentShadow;
            if (!IsShadowUsable(shadow))
            {
                Detach();
                return;
            }

            float normalizedX = probe.SupportNormalizedX;
            if (!TryGetSurfaceWorldPoint(shadow, normalizedX, out Vector3 surfaceWorldPoint))
            {
                Detach();
                return;
            }

            attachedShadow = shadow;
            attachedNormalizedX = normalizedX;
            observedGeometryVersion = shadow.GeometryVersion;
            currentShadowGeometryVersion = observedGeometryVersion;
            stableGeometryFixedSteps = 0;
            previousSurfaceWorldPoint = surfaceWorldPoint;
            lastCarryDelta = Vector3.zero;
            isAttached = true;
        }

        private void CarryWithGeometryChange(int currentGeometryVersion)
        {
            if (!TryGetSurfaceWorldPoint(attachedShadow, attachedNormalizedX, out Vector3 newSurfaceWorldPoint))
            {
                Detach();
                return;
            }

            Vector3 carryDelta = newSurfaceWorldPoint - previousSurfaceWorldPoint;
            carryDelta.z = 0f;

            if (carryDelta.magnitude > maxCarryDistancePerStep)
            {
                WarnLargeCarryDelta(carryDelta);
                Detach();
                return;
            }

            playerController.AddExternalDisplacement(carryDelta);
            lastCarryDelta = carryDelta;
            previousSurfaceWorldPoint = newSurfaceWorldPoint;
            observedGeometryVersion = currentGeometryVersion;
            currentShadowGeometryVersion = currentGeometryVersion;
            stableGeometryFixedSteps = 0;
        }

        private void RefreshAttachmentFromStableProbe()
        {
            if (probe == null
                || !probe.HasShadowSupport
                || probe.CurrentShadow != attachedShadow)
            {
                Detach();
                return;
            }

            stableGeometryFixedSteps++;
            lastCarryDelta = Vector3.zero;
            if (stableGeometryFixedSteps < requiredStableGeometryFixedSteps)
            {
                return;
            }

            attachedNormalizedX = probe.SupportNormalizedX;
            if (!TryGetSurfaceWorldPoint(attachedShadow, attachedNormalizedX, out previousSurfaceWorldPoint))
            {
                Detach();
                return;
            }

            observedGeometryVersion = attachedShadow.GeometryVersion;
            currentShadowGeometryVersion = observedGeometryVersion;
        }

        private bool TryGetSurfaceWorldPoint(
            GeneratedShadow shadow,
            float normalizedX,
            out Vector3 worldPoint)
        {
            worldPoint = default;

            if (!IsShadowUsable(shadow)
                || !ShadowSurfaceSampler.TryGetTopPointAtNormalizedX(
                    shadow.CurrentHull2D,
                    normalizedX,
                    out Vector2 planePoint))
            {
                return false;
            }

            Transform projectionPlane = shadow.ProjectionPlane;
            return ProjectionPlaneCoordinates.TryPlane2DToWorld(
                planePoint,
                projectionPlane.position,
                projectionPlane.right,
                projectionPlane.up,
                out worldPoint);
        }

        private bool IsAttachedShadowUsable()
        {
            return IsShadowUsable(attachedShadow);
        }

        private static bool IsShadowUsable(GeneratedShadow shadow)
        {
            return shadow != null
                && shadow.HasValidGeometry
                && shadow.ProjectionPlane != null
                && shadow.CurrentHull2D != null
                && shadow.CurrentHull2D.Count >= 3;
        }

        private void Detach()
        {
            isAttached = false;
            attachedShadow = null;
            attachedNormalizedX = default;
            observedGeometryVersion = default;
            currentShadowGeometryVersion = default;
            stableGeometryFixedSteps = default;
            previousSurfaceWorldPoint = default;
            lastCarryDelta = default;
        }

        private void WarnLargeCarryDelta(Vector3 carryDelta)
        {
            if (warnedLargeCarryDelta)
            {
                return;
            }

            warnedLargeCarryDelta = true;
            Debug.LogWarning(
                $"Shadow ride detached because carry delta {carryDelta.magnitude:F3} exceeded max {maxCarryDistancePerStep:F3}.",
                this);
        }
    }
}
