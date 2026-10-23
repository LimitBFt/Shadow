#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using ShadowPath.Camera;
using UnityEngine;

namespace ShadowPath.Tests.EditMode
{
    public class CameraRotationTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0f)]
        [TestCase(-0.6f, 0f)]
        [TestCase(0.7f, 0.1f)]
        [TestCase(-0.7f, -0.1f)]
        public void DeadZone_OnlyExcessContributesToAim(float offset, float expected)
        {
            Assert.That(CameraRotationMath.DeadZoneExcess(offset, 0.6f), Is.EqualTo(expected).Within(0.00001f));
        }

        [TestCase(0f, 0f)]
        [TestCase(1000f, 2.5f)]
        [TestCase(-1000f, -2.5f)]
        public void Aim_IsBoundedEvenWithDistantTargetOrZeroDepth(float offset, float expected)
        {
            Assert.That(CameraRotationMath.ClampedAimAngle(offset, 0f, 2.5f), Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void RotationModes_PreservePositionLockRollAndRespectLimits()
        {
            var owner = new GameObject("Rotation Test");
            var player = new GameObject("Target");
            var plane = new GameObject("Plane");
            owner.SetActive(false);
            try
            {
                plane.transform.rotation = Quaternion.Euler(0f, 30f, 10f);
                var controller = owner.AddComponent<CameraFollowController>();
                Set(controller, "target", player.transform);
                Set(controller, "projectionPlane", plane.transform);
                Set(controller, "rotationSmoothTime", 0f);
                Invoke(controller, "TrySnapToTarget");
                Quaternion initial = owner.transform.rotation;
                Vector3 position = owner.transform.position;

                player.transform.position = plane.transform.right * 100f + plane.transform.up * 100f;
                Set(controller, "rotationMode", CameraRotationMode.Fixed);
                Rotate(controller, plane.transform);
                Assert.That(Quaternion.Angle(initial, owner.transform.rotation), Is.LessThan(0.001f));

                Set(controller, "rotationMode", CameraRotationMode.LimitedFollow);
                Rotate(controller, plane.transform);
                Assert.That(Mathf.Abs((float)Get(controller, "currentHorizontalAimAngle")), Is.LessThanOrEqualTo(2.5f));
                Assert.That(Mathf.Abs((float)Get(controller, "currentVerticalAimAngle")), Is.LessThanOrEqualTo(2f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(initial.eulerAngles.z, owner.transform.eulerAngles.z)), Is.LessThan(0.001f));
                Assert.That(owner.transform.position, Is.EqualTo(position));

                player.transform.position = Vector3.zero;
                Rotate(controller, plane.transform);
                Assert.That(Get(controller, "targetHorizontalAimAngle"), Is.EqualTo(0f));
                Assert.That(Get(controller, "targetVerticalAimAngle"), Is.EqualTo(0f));
                Assert.That(Quaternion.Angle(initial, owner.transform.rotation), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(plane);
            }
        }

        private static void Rotate(CameraFollowController controller, Transform plane) =>
            Invoke(controller, "UpdateRotation", plane.right, plane.up, plane.forward);
        private static object Invoke(CameraFollowController controller, string name, params object[] args) =>
            typeof(CameraFollowController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, args);
        private static FieldInfo Field(string name) => typeof(CameraFollowController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Get(CameraFollowController controller, string name) => Field(name).GetValue(controller);
        private static void Set(CameraFollowController controller, string name, object value) => Field(name).SetValue(controller, value);
    }
}
#endif
