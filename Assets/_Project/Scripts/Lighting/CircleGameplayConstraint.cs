using System;

namespace ShadowPath.Lighting
{
    public static class CircleGameplayConstraint
    {
        public static void RemoveOutward(float normalX, float normalY, float x, float y,
            out float resultX, out float resultY)
        {
            double length = Math.Sqrt((double)normalX * normalX + (double)normalY * normalY);
            if (length <= 0.000001) { resultX = x; resultY = y; return; }
            float nx = (float)(normalX / length), ny = (float)(normalY / length);
            float outward = Math.Max(0f, x * nx + y * ny);
            resultX = x - nx * outward;
            resultY = y - ny * outward;
        }

        // Sweep a finite 2D movement. Only the part exceeding the radial budget is
        // blocked, then a curvature correction keeps tangent travel on the circle.
        public static bool ConstrainMotion(float currentX, float currentY, float deltaX, float deltaY,
            float centerX, float centerY, float radius, out float finalX, out float finalY)
        {
            radius = Math.Max(0f, radius);
            finalX = currentX + deltaX;
            finalY = currentY + deltaY;
            double finalDX = (double)finalX - centerX, finalDY = (double)finalY - centerY;
            double finalDistance = Math.Sqrt(finalDX * finalDX + finalDY * finalDY);
            if (finalDistance <= radius)
                return finalDistance >= Math.Max(0f, radius - 0.00001f);

            double currentDX = (double)currentX - centerX, currentDY = (double)currentY - centerY;
            double currentDistance = Math.Sqrt(currentDX * currentDX + currentDY * currentDY);
            if (currentDistance > 0.000001)
            {
                float nx = (float)(currentDX / currentDistance), ny = (float)(currentDY / currentDistance);
                float outward = deltaX * nx + deltaY * ny;
                float budget = (float)Math.Max(0.0, radius - currentDistance);
                float blocked = Math.Max(0f, outward - budget);
                finalX -= nx * blocked;
                finalY -= ny * blocked;
            }
            // If already outside after an external edit, recover gradually rather than snapping inward.
            double allowedRadius = Math.Max(radius, currentDistance);
            finalDX = (double)finalX - centerX;
            finalDY = (double)finalY - centerY;
            finalDistance = Math.Sqrt(finalDX * finalDX + finalDY * finalDY);
            if (finalDistance > allowedRadius && finalDistance > 0.0)
            {
                // A micrometer of numerical skin avoids a rounded float landing just outside.
                double safeRadius = Math.Max(0.0, allowedRadius - 0.000001);
                finalX = centerX + (float)(finalDX * safeRadius / finalDistance);
                finalY = centerY + (float)(finalDY * safeRadius / finalDistance);
            }
            return true;
        }

        public static float HorizontalRadius(float radius, float deltaY)
        {
            if (radius <= 0f || Math.Abs(deltaY) >= radius) return 0f;
            return (float)Math.Sqrt(Math.Max(0.0, (double)radius * radius - (double)deltaY * deltaY));
        }

        public static bool Contains(float x, float y, float centerX, float centerY, float radius)
        {
            double dx = (double)x - centerX;
            double dy = (double)y - centerY;
            return radius >= 0f && dx * dx + dy * dy <= (double)radius * radius + 0.000001;
        }

        // Outside a shrinking/vanishing horizontal section, permit recovery without teleporting.
        public static float ConstrainHorizontal(float current, float proposed, float minimum, float maximum)
        {
            if (current > maximum) return proposed >= current ? current : Math.Max(proposed, minimum);
            if (current < minimum) return proposed <= current ? current : Math.Min(proposed, maximum);
            return Clamp(proposed, minimum, maximum);
        }

        // Exact closest point in the intersection of a disk and an axis-aligned rectangle.
        // Projecting onto the disk and then onto the rectangle does not generally satisfy both.
        public static bool TryClamp(float x, float y, float centerX, float centerY, float radius,
            float minX, float minY, float maxX, float maxY, out float resultX, out float resultY)
        {
            resultX = resultY = 0f;
            if (radius < 0f || minX > maxX || minY > maxY) return false;
            float nearestX = Clamp(centerX, minX, maxX);
            float nearestY = Clamp(centerY, minY, maxY);
            if (!Contains(nearestX, nearestY, centerX, centerY, radius)) return false;

            float boxX = Clamp(x, minX, maxX);
            float boxY = Clamp(y, minY, maxY);
            if (Contains(boxX, boxY, centerX, centerY, radius))
            {
                resultX = boxX;
                resultY = boxY;
                return true;
            }

            double dx = (double)x - centerX;
            double dy = (double)y - centerY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            float circleX = centerX + (float)(dx * radius / length);
            float circleY = centerY + (float)(dy * radius / length);
            if (circleX >= minX && circleX <= maxX && circleY >= minY && circleY <= maxY)
            {
                resultX = circleX;
                resultY = circleY;
                return true;
            }

            double bestDistance = double.PositiveInfinity;
            Edge(true, minX, minY, maxY, x, y, centerX, centerY, radius,
                ref bestDistance, ref resultX, ref resultY);
            Edge(true, maxX, minY, maxY, x, y, centerX, centerY, radius,
                ref bestDistance, ref resultX, ref resultY);
            Edge(false, minY, minX, maxX, x, y, centerX, centerY, radius,
                ref bestDistance, ref resultX, ref resultY);
            Edge(false, maxY, minX, maxX, x, y, centerX, centerY, radius,
                ref bestDistance, ref resultX, ref resultY);
            return !double.IsPositiveInfinity(bestDistance);
        }

        private static void Edge(bool vertical, float fixedPosition, float minimum, float maximum,
            float x, float y, float centerX, float centerY, float radius,
            ref double bestDistance, ref float resultX, ref float resultY)
        {
            double offset = (double)fixedPosition - (vertical ? centerX : centerY);
            if (Math.Abs(offset) > radius) return;
            float halfWidth = (float)Math.Sqrt(Math.Max(0.0, (double)radius * radius - offset * offset));
            float center = vertical ? centerY : centerX;
            float low = Math.Max(minimum, center - halfWidth);
            float high = Math.Min(maximum, center + halfWidth);
            if (low > high) return;
            float varying = Clamp(vertical ? y : x, low, high);
            float candidateX = vertical ? fixedPosition : varying;
            float candidateY = vertical ? varying : fixedPosition;
            double deltaX = (double)candidateX - x;
            double deltaY = (double)candidateY - y;
            double distance = deltaX * deltaX + deltaY * deltaY;
            if (distance >= bestDistance) return;
            bestDistance = distance;
            resultX = candidateX;
            resultY = candidateY;
        }

        private static float Clamp(float value, float minimum, float maximum) => Math.Max(minimum, Math.Min(value, maximum));
    }
}
