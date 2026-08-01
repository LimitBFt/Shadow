using UnityEngine;

namespace ShadowPath.Shadow
{
    public class ShadowProjectionDebugger : MonoBehaviour
    {
        [SerializeField] private Transform lightSource;
        [SerializeField] private MeshFilter shadowCaster;
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private float projectedPointRadius = 0.05f;

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
            Vector3[] vertices = mesh.vertices;

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

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(projectedPoint, Mathf.Max(0f, projectedPointRadius));
            }
        }
    }
}

