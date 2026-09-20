using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    public static class ShadowSurfaceSampler
    {
        public const float DefaultEpsilon = 0.0001f;

        public static bool TryGetTopYAtX(
            IReadOnlyList<Vector2> hull,
            float x,
            out float topY,
            float epsilon = DefaultEpsilon)
        {
            topY = default;

            if (!TryGetClampedXInRange(hull, x, out float sampleX, out _, out _, epsilon))
            {
                return false;
            }

            float safeEpsilon = Mathf.Max(epsilon, 0.0000001f);
            bool foundIntersection = false;
            float bestY = float.NegativeInfinity;

            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 p0 = hull[i];
                Vector2 p1 = hull[(i + 1) % hull.Count];
                if (!IsFinite(p0) || !IsFinite(p1))
                {
                    return false;
                }

                float dx = p1.x - p0.x;
                if (Mathf.Abs(dx) <= safeEpsilon)
                {
                    if (Mathf.Abs(sampleX - p0.x) <= safeEpsilon)
                    {
                        bestY = Mathf.Max(bestY, Mathf.Max(p0.y, p1.y));
                        foundIntersection = true;
                    }

                    continue;
                }

                float minX = Mathf.Min(p0.x, p1.x) - safeEpsilon;
                float maxX = Mathf.Max(p0.x, p1.x) + safeEpsilon;
                if (sampleX < minX || sampleX > maxX)
                {
                    continue;
                }

                float t = Mathf.Clamp01((sampleX - p0.x) / dx);
                float intersectionY = p0.y + t * (p1.y - p0.y);
                if (!IsFinite(intersectionY))
                {
                    return false;
                }

                bestY = Mathf.Max(bestY, intersectionY);
                foundIntersection = true;
            }

            if (!foundIntersection || !IsFinite(bestY))
            {
                return false;
            }

            topY = bestY;
            return true;
        }

        public static bool TryGetHorizontalRange(
            IReadOnlyList<Vector2> hull,
            out float minX,
            out float maxX,
            float epsilon = DefaultEpsilon)
        {
            minX = default;
            maxX = default;

            if (hull == null || hull.Count < 3)
            {
                return false;
            }

            float calculatedMinX = float.PositiveInfinity;
            float calculatedMaxX = float.NegativeInfinity;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 point = hull[i];
                if (!IsFinite(point))
                {
                    return false;
                }

                calculatedMinX = Mathf.Min(calculatedMinX, point.x);
                calculatedMaxX = Mathf.Max(calculatedMaxX, point.x);
            }

            float safeEpsilon = Mathf.Max(epsilon, 0.0000001f);
            if (!IsFinite(calculatedMinX)
                || !IsFinite(calculatedMaxX)
                || calculatedMaxX - calculatedMinX <= safeEpsilon)
            {
                return false;
            }

            minX = calculatedMinX;
            maxX = calculatedMaxX;
            return true;
        }

        public static bool TryGetNormalizedX(
            IReadOnlyList<Vector2> hull,
            float x,
            out float normalizedX,
            float epsilon = DefaultEpsilon)
        {
            normalizedX = default;

            if (!TryGetClampedXInRange(hull, x, out float clampedX, out float minX, out float maxX, epsilon))
            {
                return false;
            }

            normalizedX = Mathf.Clamp01((clampedX - minX) / (maxX - minX));
            return IsFinite(normalizedX);
        }

        public static bool TryGetTopPointAtNormalizedX(
            IReadOnlyList<Vector2> hull,
            float normalizedX,
            out Vector2 point,
            float epsilon = DefaultEpsilon)
        {
            point = default;

            if (!IsFinite(normalizedX)
                || !TryGetHorizontalRange(hull, out float minX, out float maxX, epsilon))
            {
                return false;
            }

            float safeNormalizedX = Mathf.Clamp01(normalizedX);
            float x = Mathf.Lerp(minX, maxX, safeNormalizedX);
            if (!TryGetTopYAtX(hull, x, out float topY, epsilon))
            {
                return false;
            }

            point = new Vector2(x, topY);
            return true;
        }

        private static bool TryGetClampedXInRange(
            IReadOnlyList<Vector2> hull,
            float x,
            out float clampedX,
            out float minX,
            out float maxX,
            float epsilon)
        {
            clampedX = default;
            minX = default;
            maxX = default;

            if (!IsFinite(x) || !TryGetHorizontalRange(hull, out minX, out maxX, epsilon))
            {
                return false;
            }

            float safeEpsilon = Mathf.Max(epsilon, 0.0000001f);
            if (x < minX - safeEpsilon || x > maxX + safeEpsilon)
            {
                return false;
            }

            clampedX = Mathf.Clamp(x, minX, maxX);
            return true;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
