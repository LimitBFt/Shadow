#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using ShadowPath.Lighting;

namespace ShadowPath.Tests.EditMode
{
    public class LightMovementMathTests
    {
        [Test]
        public void Gamepad_DeadZoneAndResponseKeepFineControlAndFullSpeed()
        {
            LightMovementMath.ProcessGamepad(0.1f, 0f, 0.15f, 1.5f, out float x, out float y);
            Assert.That(x, Is.Zero);
            LightMovementMath.ProcessGamepad(1f, 0f, 0.15f, 1.5f, out x, out y);
            Assert.That(x, Is.EqualTo(1f));
            LightMovementMath.ProcessGamepad(0.5f, 0f, 0.15f, 1.5f, out x, out y);
            Assert.That(x, Is.InRange(0f, 0.5f));
            LightMovementMath.ProcessGamepad(1f, 1f, 0.15f, 1.5f, out x, out y);
            Assert.That(x * x + y * y, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void Velocity_AcceleratesDeceleratesAndTurnsWithoutRightAngleSnap()
        {
            LightMovementMath.AdvanceVelocity(0f, 0f, 3f, 0f, 6f, 8f, 10f, 0.02f, out float x, out float y);
            Assert.That(x, Is.EqualTo(0.12f).Within(0.00001f));
            LightMovementMath.AdvanceVelocity(3f, 0f, 0f, 0f, 6f, 8f, 10f, 0.02f, out x, out y);
            Assert.That(x, Is.EqualTo(2.84f).Within(0.00001f));
            LightMovementMath.AdvanceVelocity(3f, 0f, 0f, 2.5f, 6f, 8f, 10f, 0.02f, out x, out y);
            Assert.That(x, Is.GreaterThan(0f));
            Assert.That(y, Is.GreaterThan(0f));
        }

        [Test]
        public void BoundaryContact_RemovesOnlyBlockedDirectionsAndAllowsImmediateReverse()
        {
            LightMovementMath.RemoveBlockedVelocity(3f, 1f, 1f, 0f, -1f, -1f, 1f, 1f,
                false, 0f, 0f, 0f, out float x, out float y);
            Assert.That(x, Is.Zero);
            Assert.That(y, Is.EqualTo(1f));
            LightMovementMath.SmoothKeyboard(0f, 0f, -1f, 0f, 0.08f, 0.02f, out x, out y);
            LightMovementMath.AdvanceVelocity(0f, 0f, x * 3f, y * 2.5f, 6f, 8f, 10f, 0.02f, out x, out y);
            Assert.That(x, Is.LessThan(0f));
        }

        [Test]
        public void CircleAndLevelCorner_DoNotReintroduceBlockedVelocity()
        {
            LightMovementMath.RemoveBlockedVelocity(-1f, 4f, 3f, 4f, 3f, -10f, 10f, 10f,
                true, 0f, 0f, 5f, out float x, out float y);
            Assert.That(x, Is.GreaterThanOrEqualTo(-0.00001f));
            Assert.That(3f * x + 4f * y, Is.LessThanOrEqualTo(0.00001f));
        }
    }
}
#endif
