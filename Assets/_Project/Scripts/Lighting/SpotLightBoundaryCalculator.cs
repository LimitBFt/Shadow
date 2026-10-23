using System;

namespace ShadowPath.Lighting
{
    public static class SpotLightBoundaryCalculator
    {
        public const float MinimumSpotAngleDegrees = 1f;
        public const float MaximumSpotAngleDegrees = 120f;

        public static bool TryCalculateSpotAngle(float signedPlaneDistance, float targetRadius,
            out float spotAngleDegrees, out bool angleLimited)
        {
            spotAngleDegrees = 0f;
            angleLimited = false;
            if (!IsFinite(signedPlaneDistance) || !IsFinite(targetRadius)
                || Math.Abs(signedPlaneDistance) < 0.0001f || targetRadius <= 0f) return false;

            float requestedAngle = (float)(2.0 * Math.Atan(
                (double)targetRadius / Math.Abs(signedPlaneDistance)) * 180.0 / Math.PI);
            spotAngleDegrees = Math.Max(MinimumSpotAngleDegrees, Math.Min(MaximumSpotAngleDegrees, requestedAngle));
            angleLimited = requestedAngle < MinimumSpotAngleDegrees || requestedAngle > MaximumSpotAngleDegrees;
            return true;
        }

        // A perpendicular cone/plane intersection. Range and transform alignment are checked by the caller.
        public static bool TryCalculate(float signedPlaneDistance, float spotAngleDegrees, float inset,
            out float spotRadius, out float gameplayHalfWidth)
        {
            spotRadius = gameplayHalfWidth = 0f;
            if (!IsFinite(signedPlaneDistance) || !IsFinite(spotAngleDegrees) || !IsFinite(inset)
                || Math.Abs(signedPlaneDistance) < 0.0001f
                || spotAngleDegrees <= 0f || spotAngleDegrees >= 180f)
            {
                return false;
            }

            spotRadius = (float)(Math.Abs(signedPlaneDistance)
                * Math.Tan(spotAngleDegrees * Math.PI / 360.0));
            if (!IsFinite(spotRadius)) return false;
            gameplayHalfWidth = Math.Max(0f, spotRadius - Math.Max(0f, inset));
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
