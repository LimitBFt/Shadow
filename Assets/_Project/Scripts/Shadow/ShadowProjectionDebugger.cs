using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    public class ShadowProjectionDebugger : MonoBehaviour
    {
        [SerializeField] private Transform lightSource;
        [SerializeField] private MeshFilter shadowCaster;
        [SerializeField] private Transform projectionPlane;
        [SerializeField, Min(0.001f)] private float projectedPointRadius = 0.04f;
        [SerializeField, Min(0.001f)] private float hullPointRadius = 0.07f;
        [SerializeField] private float hullEpsilon = 0.0001f;

        private readonly List<Vector2> projectedPlanePoints = new List<Vector2>();
        private readonly List<Vector2> hullPlanePoints = new List<Vector2>();

        private void OnDrawGizmosSelected()
        {
            if (lightSource == null || shadowCaster == null || projectionPlane == null)
            {
                return;
            }

            Mesh mesh = shadowCaster.sharedMesh;
            if (mesh == null || !mesh.isReadable)
            {
                return;
            }

            Vector3 lightPosition = lightSource.position;
            Vector3 planePoint = projectionPlane.position;
            Vector3 planeNormal = projectionPlane.forward;
            Vector3 planeRight = projectionPlane.right;
            Vector3 planeUp = projectionPlane.up;
            Vector3[] vertices = mesh.vertices;
            float smallProjectedPointRadius = Mathf.Max(0.001f, Mathf.Min(projectedPointRadius, hullPointRadius * 0.5f));
            float largeHullPointRadius = Mathf.Max(hullPointRadius, smallProjectedPointRadius * 1.75f);

            projectedPlanePoints.Clear();
            hullPlanePoints.Clear();

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertexPosition = shadowCaster.transform.TransformPoint(vertices[i]);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(lightPosition, vertexPosition);

                if (!ShadowProjectionCalculator.TryProjectPoint(
                        lightPosition,
                        vertexPosition,
                        planePoint,
                        planeNormal,
                        out Vector3 projectedPoint))
                {
                    continue;
                }

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(vertexPosition, projectedPoint);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(projectedPoint, smallProjectedPointRadius);

                if (ProjectionPlaneCoordinates.TryWorldToPlane2D(
                        projectedPoint,
                        planePoint,
                        planeRight,
                        planeUp,
                        out Vector2 projectedPlanePoint,
                        out _,
                        hullEpsilon))
                {
                    projectedPlanePoints.Add(projectedPlanePoint);
                }
            }

            if (ConvexHull2D.Calculate(projectedPlanePoints, hullPlanePoints, hullEpsilon) != ConvexHullResult.Success)
            {
                return;
            }

            Gizmos.color = Color.magenta;
            for (int i = 0; i < hullPlanePoints.Count; i++)
            {
                Vector2 currentPoint2D = hullPlanePoints[i];

                if (!ProjectionPlaneCoordinates.TryPlane2DToWorld(
                        currentPoint2D,
                        planePoint,
                        planeRight,
                        planeUp,
                        out Vector3 currentWorldPoint,
                        out _,
                        hullEpsilon))
                {
                    return;
                }

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(currentWorldPoint, largeHullPointRadius);

                Vector2 nextPoint2D = hullPlanePoints[(i + 1) % hullPlanePoints.Count];
                if (!ProjectionPlaneCoordinates.TryPlane2DToWorld(
                        nextPoint2D,
                        planePoint,
                        planeRight,
                        planeUp,
                        out Vector3 nextWorldPoint,
                        out _,
                        hullEpsilon))
                {
                    return;
                }

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(currentWorldPoint, nextWorldPoint);
            }
        }
    }
}
