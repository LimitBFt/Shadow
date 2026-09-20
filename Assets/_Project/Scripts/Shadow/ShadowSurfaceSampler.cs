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

            if (hull == null || hull.Count < 3 || !IsFinite(x))
            {
                return false;
            }

            float safeEpsilon = Mathf.Max(epsilon, 0.0000001f);
            float minHullX = float.PositiveInfinity;
            float maxHullX = float.NegativeInfinity;

            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 point = hull[i];
                if (!IsFinite(point))
                {
                    return false;
                }

                minHullX = Mathf.Min(minHullX, point.x);
                maxHullX = Mathf.Max(maxHullX, point.x);
            }

            if (x < minHullX - safeEpsilon || x > maxHullX + safeEpsilon)
            {
                return false;
            }

            float sampleX = x;
            if (Mathf.Abs(sampleX - minHullX) <= safeEpsilon)
            {
                sampleX = minHullX;
            }
            else if (Mathf.Abs(sampleX - maxHullX) <= safeEpsilon)
            {
                sampleX = maxHullX;
            }

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
