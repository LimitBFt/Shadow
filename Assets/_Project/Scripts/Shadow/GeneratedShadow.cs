using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class GeneratedShadow : MonoBehaviour
    {
        private readonly List<Vector2> projectedPlanePoints = new List<Vector2>();
        private readonly List<Vector2> hullPlanePoints = new List<Vector2>();
        // ProjectionPlane local 2D coordinates: X is plane right, Y is plane up.
        private readonly List<Vector2> currentHull2D = new List<Vector2>();

        private ShadowCaster caster;
        private Transform lightSource;
        private Transform projectionPlane;
        private float surfaceOffset;
        private float thickness;
        private float hullEpsilon;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private Mesh runtimeMesh;
        [SerializeField] private bool hasValidGeometry;
        [SerializeField] private int geometryVersion;

        public IReadOnlyList<Vector2> CurrentHull2D => currentHull2D;
        public bool HasValidGeometry => hasValidGeometry;
        public int GeometryVersion => geometryVersion;
        public Transform ProjectionPlane => projectionPlane;

        public void Initialize(
            ShadowCaster caster,
            Transform lightSource,
            Transform projectionPlane,
            Material shadowMaterial,
            float surfaceOffset,
            float thickness,
            float hullEpsilon)
        {
            this.caster = caster;
            this.lightSource = lightSource;
            this.projectionPlane = projectionPlane;
            this.surfaceOffset = surfaceOffset;
            this.thickness = thickness;
            this.hullEpsilon = hullEpsilon;

            ClearPublishedGeometryState();
            CacheComponents();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = shadowMaterial;
            }

            if (meshCollider != null)
            {
                meshCollider.enabled = true;
                meshCollider.isTrigger = false;
                meshCollider.convex = true;
            }
        }

        public bool Rebuild()
        {
            if (!TryPrepareForRebuild(out Mesh sourceMesh, out MeshFilter sourceMeshFilter))
            {
                ClearGeneratedMesh();
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
                Vector3 vertexPosition = sourceMeshFilter.transform.TransformPoint(sourceVertices[i]);
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
                    ClearGeneratedMesh();
                    return false;
                }

                projectedPlanePoints.Add(projectedPlanePoint);
            }

            if (ConvexHull2D.Calculate(projectedPlanePoints, hullPlanePoints, hullEpsilon) != ConvexHullResult.Success)
            {
                ClearGeneratedMesh();
                return false;
            }

            Mesh mesh = GetOrCreateRuntimeMesh();
            if (ShadowPrismMeshBuilder.Build(hullPlanePoints, mesh, surfaceOffset, thickness, hullEpsilon) != ShadowPrismMeshBuildResult.Success)
            {
                ClearGeneratedMesh();
                return false;
            }

            meshFilter.sharedMesh = mesh;
            RefreshMeshCollider(mesh);
            SetPublishedGeometry(hullPlanePoints);
            return true;
        }

        private bool TryPrepareForRebuild(out Mesh sourceMesh, out MeshFilter sourceMeshFilter)
        {
            sourceMesh = null;
            sourceMeshFilter = null;

            CacheComponents();
            if (meshFilter == null || meshCollider == null)
            {
                return false;
            }

            if (caster == null || lightSource == null || projectionPlane == null || !caster.IsValid)
            {
                return false;
            }

            sourceMeshFilter = caster.MeshFilter;
            sourceMesh = sourceMeshFilter.sharedMesh;
            return sourceMesh != null && sourceMesh.isReadable;
        }

        private void CacheComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            if (meshCollider == null)
            {
                meshCollider = GetComponent<MeshCollider>();
            }
        }

        private Mesh GetOrCreateRuntimeMesh()
        {
            if (runtimeMesh != null)
            {
                return runtimeMesh;
            }

            runtimeMesh = new Mesh
            {
                name = "Generated Shadow Mesh",
                hideFlags = HideFlags.DontSave
            };

            return runtimeMesh;
        }

        private void RefreshMeshCollider(Mesh generatedMesh)
        {
            CacheComponents();
            if (meshCollider == null)
            {
                return;
            }

            meshCollider.enabled = true;
            meshCollider.isTrigger = false;
            meshCollider.convex = true;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = generatedMesh;
        }

        private void ClearGeneratedMesh()
        {
            ClearPublishedGeometryState();

            if (runtimeMesh != null)
            {
                runtimeMesh.Clear();
            }

            CacheComponents();
            if (meshFilter != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
            }
        }

        private void SetPublishedGeometry(IReadOnlyList<Vector2> hullPoints)
        {
            currentHull2D.Clear();
            for (int i = 0; i < hullPoints.Count; i++)
            {
                currentHull2D.Add(hullPoints[i]);
            }

            hasValidGeometry = true;
            geometryVersion++;
        }

        private void ClearPublishedGeometryState()
        {
            bool hadPublishedGeometry = hasValidGeometry || currentHull2D.Count > 0;
            currentHull2D.Clear();
            hasValidGeometry = false;

            if (hadPublishedGeometry)
            {
                geometryVersion++;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMesh == null)
            {
                return;
            }

            CacheComponents();
            if (meshFilter != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }

            if (meshCollider != null && meshCollider.sharedMesh == runtimeMesh)
            {
                meshCollider.sharedMesh = null;
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
