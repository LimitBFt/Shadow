#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using ShadowPath.Lighting;

namespace ShadowPath.Tests.EditMode
{
    public class SpotLightBoundaryCalculatorTests
    {
        [TestCase(10f, 4f)]
        [TestCase(-10f, 4f)]
        [TestCase(9.492931f, 3.8f)]
        public void TargetRadius_RoundTripsThroughSpotAngle(float distance, float targetRadius)
        {
            Assert.That(SpotLightBoundaryCalculator.TryCalculateSpotAngle(distance, targetRadius,
                out float angle, out bool limited), Is.True);
            Assert.That(limited, Is.False);
            Assert.That(SpotLightBoundaryCalculator.TryCalculate(distance, angle, 0.2f,
                out float radius, out float width), Is.True);
            Assert.That(radius, Is.EqualTo(targetRadius).Within(0.00001f));
            Assert.That(width, Is.EqualTo(targetRadius - 0.2f).Within(0.00001f));
        }

        [TestCase(0.000001f, SpotLightBoundaryCalculator.MinimumSpotAngleDegrees)]
        [TestCase(100000f, SpotLightBoundaryCalculator.MaximumSpotAngleDegrees)]
        public void TargetRadius_ExtremeAnglesAreLimited(float radius, float expectedAngle)
        {
            Assert.That(SpotLightBoundaryCalculator.TryCalculateSpotAngle(10f, radius,
                out float angle, out bool limited), Is.True);
            Assert.That(angle, Is.EqualTo(expectedAngle));
            Assert.That(limited, Is.True);
        }

        [TestCase(0f, 4f)]
        [TestCase(10f, 0f)]
        [TestCase(10f, -1f)]
        [TestCase(float.NaN, 4f)]
        [TestCase(10f, float.PositiveInfinity)]
        public void TargetRadius_InvalidGeometryIsRejected(float distance, float radius)
        {
            Assert.That(SpotLightBoundaryCalculator.TryCalculateSpotAngle(distance, radius, out _, out _), Is.False);
        }

        [TestCase(-9.492931f, 50f, 0.2f, 4.426627f, 4.226627f)]
        [TestCase(9.492931f, 50f, 0.2f, 4.426627f, 4.226627f)]
        [TestCase(10f, 90f, 0.2f, 10f, 9.8f)]
        [TestCase(10f, 90f, 20f, 10f, 0f)]
        [TestCase(10f, 90f, -1f, 10f, 10f)]
        public void PerpendicularCone_ComputesRadiusAndNonnegativeInsetWidth(
            float distance, float angle, float inset, float expectedRadius, float expectedHalfWidth)
        {
            Assert.That(SpotLightBoundaryCalculator.TryCalculate(distance, angle, inset,
                out float radius, out float halfWidth), Is.True);
            Assert.That(radius, Is.EqualTo(expectedRadius).Within(0.00001f));
            Assert.That(halfWidth, Is.EqualTo(expectedHalfWidth).Within(0.00001f));
        }

        [TestCase(0f, 50f, 0.2f)]
        [TestCase(10f, 0f, 0.2f)]
        [TestCase(10f, 180f, 0.2f)]
        [TestCase(float.NaN, 50f, 0.2f)]
        [TestCase(10f, float.PositiveInfinity, 0.2f)]
        [TestCase(10f, 50f, float.NaN)]
        public void InvalidGeometry_IsRejected(float distance, float angle, float inset)
        {
            Assert.That(SpotLightBoundaryCalculator.TryCalculate(distance, angle, inset, out _, out _), Is.False);
        }
    }
}
#endif
