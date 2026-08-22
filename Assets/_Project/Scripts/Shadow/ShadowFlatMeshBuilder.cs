using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    public enum ShadowFlatMeshBuildResult
    {
        Success,
        TargetMeshMissing,
        InsufficientHullPoints
    }

    public static class ShadowFlatMeshBuilder
    {
        public static ShadowFlatMeshBuildResult Build(
            IReadOnlyList<Vector2> hullPlanePoints,
            Mesh targetMesh,
            float localZOffset)
        {
            if (targetMesh == null)
            {
                return ShadowFlatMeshBuildResult.TargetMeshMissing;
            }

            targetMesh.Clear();

            if (hullPlanePoints == null || hullPlanePoints.Count < 3)
            {
                return ShadowFlatMeshBuildResult.InsufficientHullPoints;
            }

            Vector3[] vertices = new Vector3[hullPlanePoints.Count];
            Vector3[] normals = new Vector3[hullPlanePoints.Count];
            int[] triangles = new int[(hullPlanePoints.Count - 2) * 3];

            for (int i = 0; i < hullPlanePoints.Count; i++)
            {
                Vector2 point = hullPlanePoints[i];
                vertices[i] = new Vector3(point.x, point.y, localZOffset);
                normals[i] = Vector3.forward;
            }

            int triangleIndex = 0;
            for (int i = 1; i < hullPlanePoints.Count - 1; i++)
            {
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + 1;
            }

            targetMesh.vertices = vertices;
            targetMesh.triangles = triangles;
            targetMesh.normals = normals;
            targetMesh.RecalculateBounds();

            return ShadowFlatMeshBuildResult.Success;
        }
    }
}

