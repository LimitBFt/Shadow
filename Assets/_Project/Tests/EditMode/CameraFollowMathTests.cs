#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using ShadowPath.Camera;

namespace ShadowPath.Tests.EditMode
{
    public class CameraFollowMathTests
    {
        [Test]
        public void DeadZone_HoldsSmallMovesAndRetainsAnchorWhenPlayerStops()
        {
            float anchor = CameraFollowMath.AdvanceDeadZone(0f, 0.5f, 0.6f);
            Assert.That(anchor, Is.Zero);
            anchor = CameraFollowMath.AdvanceDeadZone(anchor, 1f, 0.6f);
            Assert.That(anchor, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(CameraFollowMath.AdvanceDeadZone(anchor, 0.8f, 0.6f), Is.EqualTo(anchor));
        }

        [Test]
        public void CatchUp_TransitionsContinuouslyAndIsBounded()
        {
            Assert.That(CameraFollowMath.CatchUpGain(3f, 4f, 1.8f), Is.EqualTo(1f));
            Assert.That(CameraFollowMath.CatchUpGain(4f, 4f, 1.8f), Is.EqualTo(1f));
            Assert.That(CameraFollowMath.CatchUpGain(6f, 4f, 1.8f), Is.EqualTo(1.4f).Within(0.00001f));
            Assert.That(CameraFollowMath.CatchUpGain(100f, 4f, 1.8f), Is.EqualTo(1.8f));
        }

        [Test]
        public void LookAhead_UsesSpeedDirectionAndReturnsToZeroAtRestOrWhenDisabled()
        {
            Assert.That(CameraFollowMath.LookAheadTarget(5f, true, 1f, 0.05f), Is.EqualTo(1f));
            Assert.That(CameraFollowMath.LookAheadTarget(-5f, true, 1f, 0.05f), Is.EqualTo(-1f));
            Assert.That(CameraFollowMath.LookAheadTarget(0f, true, 1f, 0.05f), Is.Zero);
            Assert.That(CameraFollowMath.LookAheadTarget(5f, false, 1f, 0.05f), Is.Zero);
        }
    }
}
#endif
