using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ShadowMeshPreview : MonoBehaviour
    {
        [SerializeField] private Transform lightSource;
        [SerializeField] private MeshFilter shadowCaster;
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private float surfaceOffset = 0.002f;
        [SerializeField, Min(0.001f)] private float thickness = 0.1f;
        [SerializeField] private float hullEpsilon = 0.0001f;
        [SerializeField] private float transformEpsilon = 0.0001f;

        private readonly List<Vector2> projectedPlanePoints = new List<Vector2>();
        private readonly List<Vector2> hullPlanePoints = new List<Vector2>();

        private MeshFilter meshFilter;
        private Mesh runtimeMesh;

        [ContextMenu("Rebuild Preview Mesh")]
        public bool RebuildPreviewMesh()
        {
            if (!TryPrepareForRebuild())
            {
                ClearPreviewMesh();
                return false;
            }

            Mesh sourceMesh = shadowCaster.sharedMesh;
            if (sourceMesh == null || !sourceMesh.isReadable)
            {
                ClearPreviewMesh();
                return false;
            }

            projectedPlanePoints.Clear();
            hullPlanePoints.Clear();

            Vector3 lightPosition = lightSource.position;
            Vector3 planePoint = projectionPlane.position;
            Vector3 planeNormal = projectionPlane.forward;
            Vector3 planeRight = projectionPlane.right;
            Vector3 planeUp = projectionPlane.up;
            Vector3[] sourceVertices = sourceMesh.vertices;

            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 vertexPosition = shadowCaster.transform.TransformPoint(sourceVertices[i]);
                if (!ShadowProjectionCalculator.TryProjectPoint(
                        lightPosition,
                        vertexPosition,
                        planePoint,
                        planeNormal,
                        out Vector3 projectedPoint))
                {
                    continue;
                }

                if (!ProjectionPlaneCoordinates.TryWorldToPlane2D(
                        projectedPoint,
                        planePoint,
                        planeRight,
                        planeUp,
                        out Vector2 projectedPlanePoint,
                        out _,
                        hullEpsilon))
                {
                    ClearPreviewMesh();
                    return false;
                }

                projectedPlanePoints.Add(projectedPlanePoint);
            }

            if (ConvexHull2D.Calculate(projectedPlanePoints, hullPlanePoints, hullEpsilon) != ConvexHullResult.Success)
            {
                ClearPreviewMesh();
                return false;
            }

            Mesh mesh = GetOrCreateRuntimeMesh();
            if (ShadowPrismMeshBuilder.Build(hullPlanePoints, mesh, surfaceOffset, thickness, hullEpsilon) != ShadowPrismMeshBuildResult.Success)
            {
                ClearPreviewMesh();
                return false;
            }

            meshFilter.sharedMesh = mesh;
            return true;
        }

        private bool TryPrepareForRebuild()
        {
            if (lightSource == null || shadowCaster == null || projectionPlane == null)
            {
                return false;
            }

            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                return false;
            }

            return IsAlignedWithProjectionPlane();
        }

        private bool IsAlignedWithProjectionPlane()
        {
            return transform.parent == projectionPlane
                && transform.localPosition.sqrMagnitude <= transformEpsilon * transformEpsilon
                && Quaternion.Angle(transform.localRotation, Quaternion.identity) <= transformEpsilon
                && IsPositiveUnitScale(transform.localScale);
        }

        private bool IsPositiveUnitScale(Vector3 scale)
        {
            return scale.x > 0f
                && scale.y > 0f
                && scale.z > 0f
                && Mathf.Abs(scale.x - 1f) <= transformEpsilon
                && Mathf.Abs(scale.y - 1f) <= transformEpsilon
                && Mathf.Abs(scale.z - 1f) <= transformEpsilon;
        }

        private Mesh GetOrCreateRuntimeMesh()
        {
            if (runtimeMesh != null)
            {
                return runtimeMesh;
            }

            runtimeMesh = new Mesh
            {
                name = "Generated Shadow Preview",
                hideFlags = HideFlags.DontSave
            };

            return runtimeMesh;
        }

        private void ClearPreviewMesh()
        {
            if (runtimeMesh != null)
            {
                runtimeMesh.Clear();
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshFilter != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMesh == null)
            {
                return;
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshFilter != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeMesh);
            }
            else
            {
                DestroyImmediate(runtimeMesh);
            }

            runtimeMesh = null;
        }
    }
}
