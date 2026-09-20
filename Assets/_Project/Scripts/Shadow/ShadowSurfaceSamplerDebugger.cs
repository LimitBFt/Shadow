using UnityEngine;

namespace ShadowPath.Shadow
{
    public class ShadowSurfaceSamplerDebugger : MonoBehaviour
    {
        [SerializeField] private GeneratedShadow targetShadow;
        [SerializeField] private float sampleX;
        [SerializeField, Range(0f, 1f)] private float sampleNormalizedX = 0.5f;
        [SerializeField] private float epsilon = ShadowSurfaceSampler.DefaultEpsilon;
        [SerializeField] private bool sampleValid;
        [SerializeField] private float sampledTopY;
        [SerializeField] private bool normalizedSampleValid;
        [SerializeField] private Vector2 normalizedSamplePoint;
        [SerializeField, Min(0.001f)] private float gizmoRadius = 0.08f;

        private void Update()
        {
            RefreshSample();
        }

        private void OnDrawGizmosSelected()
        {
            if (targetShadow == null || targetShadow.ProjectionPlane == null)
            {
                return;
            }

            Transform projectionPlane = targetShadow.ProjectionPlane;

            if (sampleValid
                && TryPlanePointToWorld(projectionPlane, new Vector2(sampleX, sampledTopY), out Vector3 sampleWorldPoint))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(sampleWorldPoint, gizmoRadius);
            }

            if (normalizedSampleValid
                && TryPlanePointToWorld(projectionPlane, normalizedSamplePoint, out Vector3 normalizedWorldPoint))
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(normalizedWorldPoint, gizmoRadius);
            }
        }

        private void RefreshSample()
        {
            sampleValid = false;
            sampledTopY = default;
            normalizedSampleValid = false;
            normalizedSamplePoint = default;

            if (targetShadow == null || !targetShadow.HasValidGeometry)
            {
                return;
            }

            sampleValid = ShadowSurfaceSampler.TryGetTopYAtX(
                targetShadow.CurrentHull2D,
                sampleX,
                out sampledTopY,
                epsilon);

            normalizedSampleValid = ShadowSurfaceSampler.TryGetTopPointAtNormalizedX(
                targetShadow.CurrentHull2D,
                sampleNormalizedX,
                out normalizedSamplePoint,
                epsilon);
        }

        private bool TryPlanePointToWorld(
            Transform projectionPlane,
            Vector2 planePoint,
            out Vector3 worldPoint)
        {
            return ProjectionPlaneCoordinates.TryPlane2DToWorld(
                planePoint,
                projectionPlane.position,
                projectionPlane.right,
                projectionPlane.up,
                out worldPoint,
                out _,
                epsilon);
        }
    }
}
