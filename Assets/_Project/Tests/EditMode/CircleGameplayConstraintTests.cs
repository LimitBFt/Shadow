#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using ShadowPath.Lighting;

namespace ShadowPath.Tests.EditMode
{
    public class CircleGameplayConstraintTests
    {
        [Test]
        public void TopJumpIsBlockedButInwardVelocityIsPreserved()
        {
            Assert.That(CircleGameplayConstraint.ConstrainMotion(0f, 5f, 0f, 0.2f,
                0f, 0f, 5f, out float x, out float y), Is.True);
            Assert.That(x, Is.Zero);
            Assert.That(y, Is.EqualTo(5f));
            CircleGameplayConstraint.RemoveOutward(0f, 5f, 0f, 7f, out x, out y);
            Assert.That(y, Is.Zero);
            CircleGameplayConstraint.RemoveOutward(0f, 5f, 0f, -2f, out x, out y);
            Assert.That(y, Is.EqualTo(-2f));
        }

        [Test]
        public void FiniteTangentStepStaysOnCircleAndDoesNotFreeze()
        {
            CircleGameplayConstraint.ConstrainMotion(5f, 0f, 0f, 0.1f, 0f, 0f, 5f, out float x, out float y);
            Assert.That(y, Is.GreaterThan(0f));
            Assert.That(x * x + y * y, Is.EqualTo(25f).Within(0.00005f));
        }

        [TestCase(5f, 0f, 5f)]
        [TestCase(5f, 3f, 4f)]
        [TestCase(5f, -3f, 4f)]
        [TestCase(5f, 5f, 0f)]
        [TestCase(5f, 8f, 0f)]
        public void HorizontalSection_TracksActualHeight(float radius, float dy, float expected)
        {
            Assert.That(CircleGameplayConstraint.HorizontalRadius(radius, dy), Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void OutsideSection_PreventsWorseningAndAllowsImmediateRecoveryWithoutSnap()
        {
            Assert.That(CircleGameplayConstraint.ConstrainHorizontal(4f, 4.1f, 0f, 0f), Is.EqualTo(4f));
            Assert.That(CircleGameplayConstraint.ConstrainHorizontal(4f, 3.9f, 0f, 0f), Is.EqualTo(3.9f));
            Assert.That(CircleGameplayConstraint.ConstrainHorizontal(0.01f, 0.02f, 0f, 0f), Is.EqualTo(0.01f));
            Assert.That(CircleGameplayConstraint.ConstrainHorizontal(-4f, -3.9f, 0f, 0f), Is.EqualTo(-3.9f));
        }

        [Test]
        public void CircleAndBox_CanIntersectEvenWhenCircleCenterIsOutsideBox()
        {
            Assert.That(CircleGameplayConstraint.TryClamp(-10f, 10f, 0f, 0f, 5f,
                3f, -5f, 8f, 5f, out float x, out float y), Is.True);
            Assert.That(x, Is.EqualTo(3f).Within(0.00001f));
            Assert.That(y, Is.EqualTo(4f).Within(0.00001f));
        }

        [Test]
        public void DisjointCircleAndBox_HasNoLegalPosition()
        {
            Assert.That(CircleGameplayConstraint.TryClamp(1f, 1f, 0f, 0f, 1f,
                3f, 3f, 5f, 5f, out _, out _), Is.False);
        }

        [Test]
        public void AtCircleEdge_ReversingInputMovesImmediately()
        {
            CircleGameplayConstraint.TryClamp(5.1f, 0f, 0f, 0f, 5f, -10f, -10f, 10f, 10f, out float x, out float y);
            Assert.That(x, Is.EqualTo(5f));
            Assert.That(y, Is.Zero);
            CircleGameplayConstraint.TryClamp(x - 0.1f, y, 0f, 0f, 5f, -10f, -10f, 10f, 10f, out x, out y);
            Assert.That(x, Is.EqualTo(4.9f).Within(0.00001f));
        }
    }
}
#endif
