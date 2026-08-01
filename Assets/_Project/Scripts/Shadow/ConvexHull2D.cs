using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    public enum ConvexHullResult
    {
        Success,
        InsufficientUniquePoints,
        CollinearPoints
    }

    public static class ConvexHull2D
    {
        public const float DefaultEpsilon = 0.00001f;

        public static ConvexHullResult Calculate(
            IReadOnlyList<Vector2> inputPoints,
            List<Vector2> hullPoints,
            float epsilon = DefaultEpsilon)
        {
            if (hullPoints == null)
            {
                return ConvexHullResult.InsufficientUniquePoints;
            }

            hullPoints.Clear();

            if (inputPoints == null || inputPoints.Count == 0)
            {
                return ConvexHullResult.InsufficientUniquePoints;
            }

            float safeEpsilon = Mathf.Max(epsilon, 0.0000001f);
            float epsilonSquared = safeEpsilon * safeEpsilon;
            List<Vector2> sortedPoints = new List<Vector2>(inputPoints.Count);

            for (int i = 0; i < inputPoints.Count; i++)
            {
                sortedPoints.Add(inputPoints[i]);
            }

            sortedPoints.Sort(CompareByXThenY);

            List<Vector2> uniquePoints = new List<Vector2>(sortedPoints.Count);
            for (int i = 0; i < sortedPoints.Count; i++)
            {
                if (!ContainsNearbyPoint(uniquePoints, sortedPoints[i], epsilonSquared))
                {
                    uniquePoints.Add(sortedPoints[i]);
                }
            }

            if (uniquePoints.Count < 3)
            {
                return ConvexHullResult.InsufficientUniquePoints;
            }

            List<Vector2> lowerHull = new List<Vector2>(uniquePoints.Count);
            for (int i = 0; i < uniquePoints.Count; i++)
            {
                AddPointToHull(lowerHull, uniquePoints[i], safeEpsilon);
            }

            List<Vector2> upperHull = new List<Vector2>(uniquePoints.Count);
            for (int i = uniquePoints.Count - 1; i >= 0; i--)
            {
                AddPointToHull(upperHull, uniquePoints[i], safeEpsilon);
            }

            for (int i = 0; i < lowerHull.Count - 1; i++)
            {
                hullPoints.Add(lowerHull[i]);
            }

            for (int i = 0; i < upperHull.Count - 1; i++)
            {
                hullPoints.Add(upperHull[i]);
            }

            if (hullPoints.Count < 3 || Mathf.Abs(SignedArea(hullPoints)) <= safeEpsilon)
            {
                hullPoints.Clear();
                return ConvexHullResult.CollinearPoints;
            }

            if (SignedArea(hullPoints) < 0f)
            {
                hullPoints.Reverse();
            }

            return ConvexHullResult.Success;
        }

        private static bool ContainsNearbyPoint(IReadOnlyList<Vector2> points, Vector2 candidate, float epsilonSquared)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if ((candidate - points[i]).sqrMagnitude <= epsilonSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddPointToHull(List<Vector2> hull, Vector2 point, float epsilon)
        {
            while (hull.Count >= 2)
            {
                Vector2 previous = hull[hull.Count - 2];
                Vector2 current = hull[hull.Count - 1];
                if (Cross(current - previous, point - current) > epsilon)
                {
                    break;
                }

                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        private static int CompareByXThenY(Vector2 a, Vector2 b)
        {
            int xComparison = a.x.CompareTo(b.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            return a.y.CompareTo(b.y);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static float SignedArea(IReadOnlyList<Vector2> points)
        {
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                area += current.x * next.y - next.x * current.y;
            }

            return area * 0.5f;
        }
    }
}
