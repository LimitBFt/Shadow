using ShadowPath.Shadow;
using UnityEngine;

namespace ShadowPath.Player
{
    public class ShadowRideProbe : MonoBehaviour
    {
        private const int SupportHitBufferSize = 8;

        [SerializeField] private LayerMask supportMask = ~0;
        [SerializeField, Min(0f)] private float groundProbeStartOffset = 0.05f;
        [SerializeField, Min(0.001f)] private float groundProbeDistance = 0.25f;
        [SerializeField, Min(0.000001f)] private float samplerEpsilon = ShadowSurfaceSampler.DefaultEpsilon;
        [SerializeField, Min(0.001f)] private float gizmoRadius = 0.06f;
        [SerializeField] private GeneratedShadow currentShadow;
        [SerializeField] private bool hasShadowSupport;
        [SerializeField] private float supportNormalizedX;
        [SerializeField] private Vector2 supportPlanePoint;
        [SerializeField] private Vector3 supportWorldPoint;

        private readonly RaycastHit[] supportHits = new RaycastHit[SupportHitBufferSize];
        private Collider playerCollider;
        private Vector3 lastProbeOrigin;
        private Vector3 lastProbeEnd;

        public GeneratedShadow CurrentShadow => currentShadow;
        public bool HasShadowSupport => hasShadowSupport;
        public float SupportNormalizedX => supportNormalizedX;
        public Vector2 SupportPlanePoint => supportPlanePoint;
        public Vector3 SupportWorldPoint => supportWorldPoint;

        private void Awake()
        {
            playerCollider = GetComponent<Collider>();
        }

        private void FixedUpdate()
        {
            RefreshSupport();
        }

        private void RefreshSupport()
        {
            if (playerCollider == null || !playerCollider.enabled)
            {
                ClearSupportState();
                return;
            }

            Vector3 probeOrigin = GetFootProbeOrigin();
            lastProbeOrigin = probeOrigin;
            lastProbeEnd = probeOrigin + Vector3.down * groundProbeDistance;

            int hitCount = Physics.RaycastNonAlloc(
                probeOrigin,
                Vector3.down,
                supportHits,
                groundProbeDistance,
                supportMask,
                QueryTriggerInteraction.Ignore);

            if (!TryGetClosestValidHit(hitCount, out RaycastHit supportHit))
            {
                ClearSupportState();
                return;
            }

            GeneratedShadow shadow = supportHit.collider.GetComponentInParent<GeneratedShadow>();
            if (shadow == null || !shadow.HasValidGeometry || shadow.ProjectionPlane == null)
            {
                ClearSupportState();
                return;
            }

            Transform projectionPlane = shadow.ProjectionPlane;
            if (!ProjectionPlaneCoordinates.TryWorldToPlane2D(
                    supportHit.point,
                    projectionPlane.position,
                    projectionPlane.right,
                    projectionPlane.up,
                    out Vector2 planePoint,
                    out _,
                    samplerEpsilon))
            {
                ClearSupportState();
                return;
            }

            if (!ShadowSurfaceSampler.TryGetNormalizedX(
                    shadow.CurrentHull2D,
                    planePoint.x,
                    out float normalizedX,
                    samplerEpsilon))
            {
                ClearSupportState();
                return;
            }

            currentShadow = shadow;
            hasShadowSupport = true;
            supportNormalizedX = normalizedX;
            supportPlanePoint = planePoint;
            supportWorldPoint = supportHit.point;
        }

        private Vector3 GetFootProbeOrigin()
        {
            Bounds bounds = playerCollider.bounds;
            return new Vector3(
                bounds.center.x,
                bounds.min.y + groundProbeStartOffset,
                bounds.center.z);
        }

        private bool TryGetClosestValidHit(int hitCount, out RaycastHit closestHit)
        {
            closestHit = default;
            bool foundHit = false;
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = supportHits[i];
                if (hit.collider == null || hit.collider.isTrigger || IsOwnCollider(hit.collider))
                {
                    continue;
                }

                if (hit.distance >= closestDistance)
                {
                    continue;
                }

                closestHit = hit;
                closestDistance = hit.distance;
                foundHit = true;
            }

            return foundHit;
        }

        private bool IsOwnCollider(Collider hitCollider)
        {
            Transform hitTransform = hitCollider.transform;
            return hitCollider == playerCollider
                || hitTransform == transform
                || hitTransform.IsChildOf(transform);
        }

        private void ClearSupportState()
        {
            currentShadow = null;
            hasShadowSupport = false;
            supportNormalizedX = default;
            supportPlanePoint = default;
            supportWorldPoint = default;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(lastProbeOrigin, lastProbeEnd);

            if (!hasShadowSupport)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(supportWorldPoint, gizmoRadius);
        }
    }
}
