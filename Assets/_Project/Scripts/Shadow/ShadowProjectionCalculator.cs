using UnityEngine;

namespace ShadowPath.Shadow
{
    public enum ShadowProjectionFailureReason
    {
        None,
        PlaneNormalTooSmall,
        LightAndVertexCoincident,
        RayParallelToPlane,
        IntersectionBehindLight
    }

    public static class ShadowProjectionCalculator
    {
        public const float DefaultEpsilon = 0.00001f;

        public static bool TryProjectPoint(
            Vector3 lightPosition,
            Vector3 vertexPosition,
            Vector3 planePoint,
            Vector3 planeNormal,
            out Vector3 projectedPoint)
        {
            return TryProjectPoint(
                lightPosition,
                vertexPosition,
                planePoint,
                planeNormal,
                out projectedPoint,
                out _);
        }

        public static bool TryProjectPoint(
            Vector3 lightPosition,
            Vector3 vertexPosition,
            Vector3 planePoint,
            Vector3 planeNormal,
            out Vector3 projectedPoint,
            out ShadowProjectionFailureReason failureReason,
            float epsilon = DefaultEpsilon)
        {
            projectedPoint = default;
            failureReason = ShadowProjectionFailureReason.None;

            if (planeNormal.sqrMagnitude <= epsilon * epsilon)
            {
                failureReason = ShadowProjectionFailureReason.PlaneNormalTooSmall;
                return false;
            }

            Vector3 rayVector = vertexPosition - lightPosition;
            if (rayVector.sqrMagnitude <= epsilon * epsilon)
            {
                failureReason = ShadowProjectionFailureReason.LightAndVertexCoincident;
                return false;
            }

            Vector3 rayDirection = rayVector.normalized;
            Vector3 normalizedPlaneNormal = planeNormal.normalized;
            float denominator = Vector3.Dot(rayDirection, normalizedPlaneNormal);

            if (Mathf.Abs(denominator) <= epsilon)
            {
                failureReason = ShadowProjectionFailureReason.RayParallelToPlane;
                return false;
            }

            float distanceAlongRay = Vector3.Dot(planePoint - lightPosition, normalizedPlaneNormal) / denominator;
            if (distanceAlongRay < -epsilon)
            {
                failureReason = ShadowProjectionFailureReason.IntersectionBehindLight;
                return false;
            }

            if (distanceAlongRay < 0f)
            {
                distanceAlongRay = 0f;
            }

            projectedPoint = lightPosition + rayDirection * distanceAlongRay;
            return true;
        }
    }
}

