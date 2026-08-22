using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    public enum ShadowPrismMeshBuildResult
    {
        Success,
        TargetMeshMissing,
        InsufficientHullPoints,
        ThicknessTooSmall
    }

    public static class ShadowPrismMeshBuilder
    {
        public const float DefaultEpsilon = 0.00001f;

        public static ShadowPrismMeshBuildResult Build(
            IReadOnlyList<Vector2> hullPlanePoints,
            Mesh targetMesh,
            float frontZ,
            float thickness,
            float epsilon = DefaultEpsilon)
        {
            if (targetMesh == null)
            {
                return ShadowPrismMeshBuildResult.TargetMeshMissing;
            }

            targetMesh.Clear();

            if (hullPlanePoints == null || hullPlanePoints.Count < 3)
            {
                return ShadowPrismMeshBuildResult.InsufficientHullPoints;
            }

            float safeEpsilon = Mathf.Max(epsilon, 0.0000001f);
            if (thickness <= safeEpsilon)
            {
                return ShadowPrismMeshBuildResult.ThicknessTooSmall;
            }

            int pointCount = hullPlanePoints.Count;
            int backVertexOffset = pointCount;
            int sideVertexOffset = pointCount * 2;
            float backZ = frontZ - thickness;

            Vector3[] vertices = new Vector3[pointCount * 6];
            Vector3[] normals = new Vector3[vertices.Length];
            int frontTriangleCount = pointCount - 2;
            int sideTriangleCount = pointCount * 2;
            int[] triangles = new int[(frontTriangleCount * 2 + sideTriangleCount) * 3];

            for (int i = 0; i < pointCount; i++)
            {
                Vector2 point = hullPlanePoints[i];
                vertices[i] = new Vector3(point.x, point.y, frontZ);
                vertices[backVertexOffset + i] = new Vector3(point.x, point.y, backZ);
                normals[i] = Vector3.forward;
                normals[backVertexOffset + i] = Vector3.back;
            }

            int triangleIndex = 0;
            for (int i = 1; i < pointCount - 1; i++)
            {
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + 1;
            }

            for (int i = 1; i < pointCount - 1; i++)
            {
                triangles[triangleIndex++] = backVertexOffset;
                triangles[triangleIndex++] = backVertexOffset + i + 1;
                triangles[triangleIndex++] = backVertexOffset + i;
            }

            for (int i = 0; i < pointCount; i++)
            {
                int next = (i + 1) % pointCount;
                Vector2 currentPoint = hullPlanePoints[i];
                Vector2 nextPoint = hullPlanePoints[next];
                Vector2 edge = nextPoint - currentPoint;
                Vector3 sideNormal = new Vector3(edge.y, -edge.x, 0f).normalized;

                int sideBase = sideVertexOffset + i * 4;
                int sideFrontCurrent = sideBase;
                int sideFrontNext = sideBase + 1;
                int sideBackNext = sideBase + 2;
                int sideBackCurrent = sideBase + 3;

                vertices[sideFrontCurrent] = new Vector3(currentPoint.x, currentPoint.y, frontZ);
                vertices[sideFrontNext] = new Vector3(nextPoint.x, nextPoint.y, frontZ);
                vertices[sideBackNext] = new Vector3(nextPoint.x, nextPoint.y, backZ);
                vertices[sideBackCurrent] = new Vector3(currentPoint.x, currentPoint.y, backZ);

                normals[sideFrontCurrent] = sideNormal;
                normals[sideFrontNext] = sideNormal;
                normals[sideBackNext] = sideNormal;
                normals[sideBackCurrent] = sideNormal;

                triangles[triangleIndex++] = sideFrontCurrent;
                triangles[triangleIndex++] = sideBackNext;
                triangles[triangleIndex++] = sideFrontNext;

                triangles[triangleIndex++] = sideFrontCurrent;
                triangles[triangleIndex++] = sideBackCurrent;
                triangles[triangleIndex++] = sideBackNext;
            }

            targetMesh.vertices = vertices;
            targetMesh.triangles = triangles;
            targetMesh.normals = normals;
            targetMesh.RecalculateBounds();

            return ShadowPrismMeshBuildResult.Success;
        }
    }
}
