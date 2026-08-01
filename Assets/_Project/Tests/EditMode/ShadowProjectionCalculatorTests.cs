#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using ShadowPath.Shadow;
using UnityEngine;

namespace ShadowPath.Tests.EditMode
{
    public class ShadowProjectionCalculatorTests
    {
        [Test]
        public void TryProjectPoint_WhenRayHitsPlane_ReturnsProjectedPoint()
        {
            bool result = ShadowProjectionCalculator.TryProjectPoint(
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 1f, 1f),
                new Vector3(0f, 0f, 2f),
                Vector3.forward,
                out Vector3 projectedPoint,
                out ShadowProjectionFailureReason failureReason);

            Assert.That(result, Is.True);
            Assert.That(failureReason, Is.EqualTo(ShadowProjectionFailureReason.None));
            AssertVectorApproximately(new Vector3(2f, 2f, 2f), projectedPoint);
        }

        [Test]
        public void TryProjectPoint_WhenRayIsParallelToPlane_ReturnsParallelFailure()
        {
            bool result = ShadowProjectionCalculator.TryProjectPoint(
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                Vector3.forward,
                out Vector3 projectedPoint,
                out ShadowProjectionFailureReason failureReason);

            Assert.That(result, Is.False);
            Assert.That(failureReason, Is.EqualTo(ShadowProjectionFailureReason.RayParallelToPlane));
            AssertVectorApproximately(Vector3.zero, projectedPoint);
        }

        [Test]
        public void TryProjectPoint_WhenIntersectionIsBehindLight_ReturnsBehindLightFailure()
        {
            bool result = ShadowProjectionCalculator.TryProjectPoint(
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, -1f),
                Vector3.forward,
                out _,
                out ShadowProjectionFailureReason failureReason);

            Assert.That(result, Is.False);
            Assert.That(failureReason, Is.EqualTo(ShadowProjectionFailureReason.IntersectionBehindLight));
        }

        [Test]
        public void TryProjectPoint_WhenPlaneNormalIsNotUnitLength_NormalizesAndReturnsProjectedPoint()
        {
            bool result = ShadowProjectionCalculator.TryProjectPoint(
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 1f, 1f),
                new Vector3(0f, 0f, 2f),
                new Vector3(0f, 0f, 10f),
                out Vector3 projectedPoint,
                out ShadowProjectionFailureReason failureReason);

            Assert.That(result, Is.True);
            Assert.That(failureReason, Is.EqualTo(ShadowProjectionFailureReason.None));
            AssertVectorApproximately(new Vector3(2f, 2f, 2f), projectedPoint);
        }

        [Test]
        public void TryProjectPoint_WhenLightAndVertexCoincident_ReturnsCoincidentFailure()
        {
            bool result = ShadowProjectionCalculator.TryProjectPoint(
                new Vector3(1f, 2f, 3f),
                new Vector3(1f, 2f, 3f),
                Vector3.zero,
                Vector3.up,
                out _,
                out ShadowProjectionFailureReason failureReason);

            Assert.That(result, Is.False);
            Assert.That(failureReason, Is.EqualTo(ShadowProjectionFailureReason.LightAndVertexCoincident));
        }

        [Test]
        public void TryProjectPoint_WhenLightIsOnPlane_ReturnsLightPosition()
        {
            Vector3 lightPosition = new Vector3(1f, 2f, 0f);

            bool result = ShadowProjectionCalculator.TryProjectPoint(
                lightPosition,
                new Vector3(1f, 2f, 1f),
                Vector3.zero,
                Vector3.forward,
                out Vector3 projectedPoint,
                out ShadowProjectionFailureReason failureReason);

            Assert.That(result, Is.True);
            Assert.That(failureReason, Is.EqualTo(ShadowProjectionFailureReason.None));
            AssertVectorApproximately(lightPosition, projectedPoint);
        }

        private static void AssertVectorApproximately(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }
    }
}
#endif
