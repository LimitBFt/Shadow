using UnityEngine;

namespace ShadowPath.Shadow
{
    public enum ProjectionPlaneCoordinateResult
    {
        Success,
        PlaneRightTooSmall,
        PlaneUpTooSmall,
        PlaneAxesNearlyParallel
    }

    public static class ProjectionPlaneCoordinates
    {
        public const float DefaultEpsilon = 0.00001f;

        public static bool TryWorldToPlane2D(
            Vector3 worldPoint,
            Vector3 planeOrigin,
            Vector3 planeRight,
            Vector3 planeUp,
            out Vector2 point2D)
        {
            return TryWorldToPlane2D(
                worldPoint,
                planeOrigin,
                planeRight,
                planeUp,
                out point2D,
                out _);
        }

        public static bool TryWorldToPlane2D(
            Vector3 worldPoint,
            Vector3 planeOrigin,
            Vector3 planeRight,
            Vector3 planeUp,
            out Vector2 point2D,
            out ProjectionPlaneCoordinateResult result,
            float epsilon = DefaultEpsilon)
        {
            point2D = default;

            if (!TryValidateAxes(planeRight, planeUp, out Vector3 normalizedRight, out Vector3 normalizedUp, out result, epsilon))
            {
                return false;
            }

            Vector3 offset = worldPoint - planeOrigin;
            point2D = new Vector2(
                Vector3.Dot(offset, normalizedRight),
                Vector3.Dot(offset, normalizedUp));

            result = ProjectionPlaneCoordinateResult.Success;
            return true;
        }

        public static bool TryPlane2DToWorld(
            Vector2 point2D,
            Vector3 planeOrigin,
            Vector3 planeRight,
            Vector3 planeUp,
            out Vector3 worldPoint)
        {
            return TryPlane2DToWorld(
                point2D,
                planeOrigin,
                planeRight,
                planeUp,
                out worldPoint,
                out _);
        }

        public static bool TryPlane2DToWorld(
            Vector2 point2D,
            Vector3 planeOrigin,
            Vector3 planeRight,
            Vector3 planeUp,
            out Vector3 worldPoint,
            out ProjectionPlaneCoordinateResult result,
            float epsilon = DefaultEpsilon)
        {
            worldPoint = default;

            if (!TryValidateAxes(planeRight, planeUp, out Vector3 normalizedRight, out Vector3 normalizedUp, out result, epsilon))
            {
                return false;
            }

            worldPoint = planeOrigin + normalizedRight * point2D.x + normalizedUp * point2D.y;
            result = ProjectionPlaneCoordinateResult.Success;
            return true;
        }

        private static bool TryValidateAxes(
            Vector3 planeRight,
            Vector3 planeUp,
            out Vector3 normalizedRight,
            out Vector3 normalizedUp,
            out ProjectionPlaneCoordinateResult result,
            float epsilon)
        {
            normalizedRight = default;
            normalizedUp = default;
            result = ProjectionPlaneCoordinateResult.Success;

            float safeEpsilon = Mathf.Max(epsilon, 0.0000001f);
            if (planeRight.sqrMagnitude <= safeEpsilon * safeEpsilon)
            {
                result = ProjectionPlaneCoordinateResult.PlaneRightTooSmall;
                return false;
            }

            if (planeUp.sqrMagnitude <= safeEpsilon * safeEpsilon)
            {
                result = ProjectionPlaneCoordinateResult.PlaneUpTooSmall;
                return false;
            }

            normalizedRight = planeRight.normalized;
            normalizedUp = planeUp.normalized;
            if (Vector3.Cross(normalizedRight, normalizedUp).sqrMagnitude <= safeEpsilon * safeEpsilon)
            {
                result = ProjectionPlaneCoordinateResult.PlaneAxesNearlyParallel;
                return false;
            }

            return true;
        }
    }
}

