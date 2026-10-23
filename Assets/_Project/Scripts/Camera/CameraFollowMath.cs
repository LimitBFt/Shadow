using System;

namespace ShadowPath.Camera
{
    public static class CameraFollowMath
    {
        public static float AdvanceDeadZone(float anchor, float target, float halfExtent)
        {
            float extent = Math.Max(0f, halfExtent);
            float delta = target - anchor;
            return Math.Abs(delta) <= extent ? anchor : target - Math.Sign(delta) * extent;
        }

        public static float CatchUpGain(float error, float threshold, float multiplier)
        {
            threshold = Math.Max(0.0001f, threshold);
            float blend = Math.Max(0f, Math.Min(1f, (error - threshold) / threshold));
            return 1f + (Math.Max(1f, multiplier) - 1f) * blend;
        }

        public static float LookAheadTarget(float horizontalSpeed, bool enabled, float distance, float threshold)
        {
            return !enabled || Math.Abs(horizontalSpeed) <= Math.Max(0f, threshold)
                ? 0f : Math.Sign(horizontalSpeed) * Math.Max(0f, distance);
        }
    }
}
