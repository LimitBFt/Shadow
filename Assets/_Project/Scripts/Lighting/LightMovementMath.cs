using System;

namespace ShadowPath.Lighting
{
    public static class LightMovementMath
    {
        public static void ProcessGamepad(float x, float y, float deadZone, float exponent, out float rx, out float ry)
        {
            double length = Math.Sqrt((double)x * x + (double)y * y);
            deadZone = Math.Max(0f, Math.Min(0.99f, deadZone));
            rx = ry = 0f;
            if (length <= deadZone || length <= 0.000001) return;
            double magnitude = Math.Min(1.0, length);
            double response = Math.Pow((magnitude - deadZone) / (1.0 - deadZone), Math.Max(0.01f, exponent));
            rx = (float)(x / length * response);
            ry = (float)(y / length * response);
        }

        public static void SmoothKeyboard(float currentX, float currentY, float x, float y,
            float smoothTime, float dt, out float rx, out float ry)
        {
            double magnitude = Math.Sqrt((double)x * x + (double)y * y);
            if (magnitude <= 0.000001) { rx = ry = 0f; return; } // Release feel is handled by velocity deceleration.
            if (magnitude > 1.0) { x /= (float)magnitude; y /= (float)magnitude; }
            if (smoothTime <= 0f) { rx = x; ry = y; return; }
            MoveTowards(currentX, currentY, x, y, Math.Max(0f, dt) / smoothTime, out rx, out ry);
        }

        public static void AdvanceVelocity(float currentX, float currentY, float desiredX, float desiredY,
            float acceleration, float deceleration, float turnAcceleration, float dt, out float vx, out float vy)
        {
            double desiredLength = Math.Sqrt((double)desiredX * desiredX + (double)desiredY * desiredY);
            double currentLength = Math.Sqrt((double)currentX * currentX + (double)currentY * currentY);
            float rate = acceleration;
            if (desiredLength <= 0.000001) rate = deceleration;
            else if (currentLength > 0.000001
                && (currentX * desiredX + currentY * desiredY) / (currentLength * desiredLength) < 0.7)
                rate = turnAcceleration;
            MoveTowards(currentX, currentY, desiredX, desiredY, Math.Max(0f, rate) * Math.Max(0f, dt), out vx, out vy);
        }

        private static void MoveTowards(float x, float y, float targetX, float targetY, float step, out float rx, out float ry)
        {
            double dx = (double)targetX - x, dy = (double)targetY - y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= step || distance <= 0.000001) { rx = targetX; ry = targetY; return; }
            rx = x + (float)(dx * step / distance);
            ry = y + (float)(dy * step / distance);
        }

        // Project velocity onto all active boundary tangent half-planes together.
        // This also handles a circle touching a Level Bounds edge/corner without ping-pong projections.
        public static void RemoveBlockedVelocity(float vx, float vy, float x, float y,
            float minX, float minY, float maxX, float maxY, bool hasCircle, float centerX, float centerY, float radius,
            out float resultX, out float resultY)
        {
            var contact = new Contacts
            {
                left = x <= minX + 0.0001f, right = x >= maxX - 0.0001f,
                bottom = y <= minY + 0.0001f, top = y >= maxY - 0.0001f
            };
            double dx = (double)x - centerX, dy = (double)y - centerY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (hasCircle && distance <= 0.000001 && radius <= 0f) { resultX = resultY = 0f; return; }
            contact.circle = hasCircle && distance >= radius - 0.0001f && distance > 0.000001;
            if (contact.circle)
            {
                contact.nx = (float)(dx / distance);
                contact.ny = (float)(dy / distance);
            }
            if (contact.Allows(vx, vy)) { resultX = vx; resultY = vy; return; }
            resultX = resultY = 0f;
            double best = (double)vx * vx + (double)vy * vy;
            if (contact.left || contact.right) Candidate(0f, vy, vx, vy, contact, ref best, ref resultX, ref resultY);
            if (contact.bottom || contact.top) Candidate(vx, 0f, vx, vy, contact, ref best, ref resultX, ref resultY);
            if (contact.circle)
            {
                float outward = vx * contact.nx + vy * contact.ny;
                Candidate(vx - outward * contact.nx, vy - outward * contact.ny, vx, vy, contact,
                    ref best, ref resultX, ref resultY);
            }
        }

        private static void Candidate(float x, float y, float vx, float vy, Contacts contact,
            ref double best, ref float resultX, ref float resultY)
        {
            if (!contact.Allows(x, y)) return;
            double dx = (double)x - vx, dy = (double)y - vy;
            double distance = dx * dx + dy * dy;
            if (distance > best) return;
            best = distance;
            resultX = x;
            resultY = y;
        }

        private struct Contacts
        {
            public bool left, right, bottom, top, circle;
            public float nx, ny;
            public bool Allows(float x, float y) => (!left || x >= -0.000001f) && (!right || x <= 0.000001f)
                && (!bottom || y >= -0.000001f) && (!top || y <= 0.000001f)
                && (!circle || x * nx + y * ny <= 0.000001f);
        }
    }
}
