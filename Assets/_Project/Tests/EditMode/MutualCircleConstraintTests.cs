#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using ShadowPath.Lighting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowPath.Tests.EditMode
{
    public class MutualCircleConstraintTests
    {
        [TestCase(5f, 0f, -0.1f, 0f)]
        [TestCase(-5f, 0f, 0.1f, 0f)]
        [TestCase(3f, 4f, -0.03f, -0.04f)]
        [TestCase(5.1f, 0f, -0.01f, 0.1f)]
        [TestCase(-5.1f, 0f, 0.01f, 0.1f)]
        public void DistanceReducingMotion_IsNeverRejected(float x, float y, float dx, float dy)
        {
            CircleGameplayConstraint.ConstrainMotion(x, y, dx, dy, 0f, 0f, 5f, out float fx, out float fy);
            Assert.That(fx, Is.EqualTo(x + dx).Within(0.00001f));
            Assert.That(fy, Is.EqualTo(y + dy).Within(0.00001f));
        }

        [Test]
        public void SwappingPlayerAndLight_UsesTheSameRadialRule()
        {
            CircleGameplayConstraint.ConstrainMotion(5f, 0f, 0.1f, 0.2f, 0f, 0f, 5f, out float px, out float py);
            CircleGameplayConstraint.ConstrainMotion(0f, 0f, -0.1f, -0.2f, 5f, 0f, 5f, out float lx, out float ly);
            Assert.That(px - 5f, Is.EqualTo(-lx).Within(0.00001f));
            Assert.That(py, Is.EqualTo(-ly).Within(0.00001f));
            Assert.That(py, Is.GreaterThan(0f), "Tangent movement must survive radial blocking.");
        }

        [Test]
        public void OutsideCircle_MixedOutwardMotionKeepsTangentAndDoesNotIncreaseDistance()
        {
            CircleGameplayConstraint.ConstrainMotion(5.1f, 0f, 0.1f, 0.2f, 0f, 0f, 5f, out float x, out float y);
            Assert.That(y, Is.GreaterThan(0f));
            Assert.That(x * x + y * y, Is.LessThanOrEqualTo(5.1f * 5.1f + 0.00001f));
        }

        [Test]
        public void InfiniteBoxContacts_DoNotBlockInwardVelocityOutsideOldLevelBounds()
        {
            LightMovementMath.RemoveBlockedVelocity(-1f, 0f, 30f, 0f,
                float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity,
                true, 25f, 0f, 5f, out float vx, out float vy);
            Assert.That(vx, Is.EqualTo(-1f));
            Assert.That(vy, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Controller_SkipsAllLevelBoundsWhenDisabledIncludingInitialization(bool autoFollow)
        {
            var root = new GameObject("Inactive Mutual Constraint Test");
            root.SetActive(false);
            try
            {
                Transform plane = Child(root, "Plane");
                Transform player = Child(root, "Player Sample");
                Transform light = Child(root, "Light");
                player.position = new Vector3(25f, 0f, 0f);
                light.position = new Vector3(30f, 0f, -10f);
                var area = root.AddComponent<LightGameplayArea>();
                Set(area, "boundaryMode", LightGameplayArea.BoundaryMode.Manual);
                Set(area, "projectionPlane", plane);
                Set(area, "player", player);
                Set(area, "lightSource", light);
                Set(area, "gameplayHalfWidth", 5f);
                var controller = light.gameObject.AddComponent<LightSourceController>();
                Set(controller, "projectionPlane", plane);
                Set(controller, "lightGameplayArea", area);
                controller.SetAutoFollow(autoFollow);
                Assert.That(Invoke(controller, "TryInitializeMovement"), Is.True);
                Invoke(controller, "Start");
                Assert.That(Get(controller, "initialMovementValid"), Is.True);
                Set(controller, "rawProposedLightPosition", new Vector2(29.9f, 0f));
                Set(controller, "currentPlaneVelocity", Vector2.left);
                Invoke(controller, "ApplyConstrainedMovement");
                Assert.That(light.position.x, Is.EqualTo(29.9f).Within(0.00001f));
                Assert.That(light.position.z, Is.EqualTo(-10f));
                Assert.That(Get(controller, "isLevelBoundsBlocking"), Is.False);
                Assert.That(Get(controller, "isGameplayCircleBlocking"), Is.False);
                Assert.That(Get(controller, "movementReducesDistance"), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void OptionalLevelBounds_ApplyOnlyWhenEnabled()
        {
            var root = new GameObject("Inactive Level Bounds Test");
            root.SetActive(false);
            try
            {
                Transform plane = Child(root, "Plane");
                var controller = Child(root, "Light").gameObject.AddComponent<LightSourceController>();
                Set(controller, "projectionPlane", plane);
                Set(controller, "currentLightPlanePosition", new Vector2(11.9f, 0f));
                Set(controller, "rawProposedLightPosition", new Vector2(12.1f, 0f));
                Set(controller, "useLevelLightBounds", true);
                Invoke(controller, "ApplyConstrainedMovement");
                Assert.That(Get(controller, "finalLightPosition"), Is.EqualTo(new Vector2(12f, 0f)));
                Assert.That(Get(controller, "isLevelBoundsBlocking"), Is.True);
                Set(controller, "useLevelLightBounds", false);
                Invoke(controller, "ApplyConstrainedMovement");
                Assert.That(Get(controller, "finalLightPosition"), Is.EqualTo(new Vector2(12.1f, 0f)));
                Assert.That(Get(controller, "isLevelBoundsBlocking"), Is.False);
                Set(controller, "useLevelLightBounds", true);
                Set(controller, "currentLightPlanePosition", new Vector2(15f, 0f));
                Set(controller, "rawProposedLightPosition", new Vector2(14.9f, 0f));
                Invoke(controller, "ApplyConstrainedMovement");
                Assert.That(Get(controller, "finalLightPosition"), Is.EqualTo(new Vector2(14.9f, 0f)),
                    "Enabling level bounds outside the box must not cause a multi-meter snap.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform Child(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child.transform;
        }
        private static object Invoke(object owner, string name) => owner.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
        private static FieldInfo Field(object owner, string name) => owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        private static object Get(object owner, string name) => Field(owner, name).GetValue(owner);
    }
}
#endif
