using System;

namespace ShadowPath.Camera
{
    public static class CameraRotationMath
    {
        public static float DeadZoneExcess(float offset, float halfExtent)
        {
            float extent = Math.Max(0f, halfExtent);
            return offset - Math.Max(-extent, Math.Min(extent, offset));
        }

        public static float ClampedAimAngle(float offset, float depth, float maxAngle)
        {
            float limit = Math.Max(0f, Math.Min(85f, maxAngle));
            double angle = Math.Atan2(offset, Math.Max(0.0001f, Math.Abs(depth))) * (180.0 / Math.PI);
            return (float)Math.Max(-limit, Math.Min(limit, angle));
        }
    }
}
