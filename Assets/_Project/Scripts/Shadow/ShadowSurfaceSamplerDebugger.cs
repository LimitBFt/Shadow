using UnityEngine;

namespace ShadowPath.Shadow
{
    public class ShadowSurfaceSamplerDebugger : MonoBehaviour
    {
        [SerializeField] private GeneratedShadow targetShadow;
        [SerializeField] private float sampleX;
        [SerializeField] private float epsilon = ShadowSurfaceSampler.DefaultEpsilon;
        [SerializeField] private bool sampleValid;
        [SerializeField] private float sampledTopY;
        [SerializeField, Min(0.001f)] private float gizmoRadius = 0.08f;

        private void Update()
        {
            RefreshSample();
        }

        private void OnDrawGizmosSelected()
        {
            if (!sampleValid || targetShadow == null || targetShadow.ProjectionPlane == null)
            {
                return;
            }

            Transform projectionPlane = targetShadow.ProjectionPlane;
            if (!ProjectionPlaneCoordinates.TryPlane2DToWorld(
                    new Vector2(sampleX, sampledTopY),
                    projectionPlane.position,
                    projectionPlane.right,
                    projectionPlane.up,
                    out Vector3 worldPoint,
                    out _,
                    epsilon))
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(worldPoint, gizmoRadius);
        }

        private void RefreshSample()
        {
            sampleValid = false;
            sampledTopY = default;

            if (targetShadow == null || !targetShadow.HasValidGeometry)
            {
                return;
            }

            sampleValid = ShadowSurfaceSampler.TryGetTopYAtX(
                targetShadow.CurrentHull2D,
                sampleX,
                out sampledTopY,
                epsilon);
        }
    }
}
