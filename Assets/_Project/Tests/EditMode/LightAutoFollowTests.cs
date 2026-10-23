#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using ShadowPath.Lighting;
using UnityEngine;

namespace ShadowPath.Tests.EditMode
{
    public class LightAutoFollowTests
    {
        [TestCase(2f, 20f, true, false, 0f, 0f)]
        [TestCase(3f, -20f, true, false, 0f, 0f)]
        [TestCase(5f, 20f, true, false, 2f, 0f)]
        [TestCase(-5f, -20f, true, false, -2f, 0f)]
        [TestCase(5f, -6f, true, true, 2f, -3f)]
        [TestCase(5f, -6f, false, true, 0f, -3f)]
        [TestCase(5f, -6f, false, false, 0f, 0f)]
        public void DeadZone_MovesOnlyEnabledAxesByExcess(float x, float y, bool horizontal,
            bool vertical, float expectedX, float expectedY)
        {
            Vector2 light = new Vector2(10f, 11f);
            Vector2 center = new Vector2(12f, 13f);
            Vector2 result = LightSourceController.CalculateAutoFollowTarget(light, center,
                center + new Vector2(x, y), horizontal, vertical, 3f);
            Assert.That(result.x, Is.EqualTo(light.x + expectedX).Within(0.00001f));
            Assert.That(result.y, Is.EqualTo(light.y + expectedY).Within(0.00001f));
        }

        [Test]
        public void ModeChanges_ClearMotionWithoutChangingCurrentPositionOrInitialization()
        {
            var owner = new GameObject("Inactive Auto Follow Test");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<LightSourceController>();
                owner.transform.position = new Vector3(7f, 8f, 9f);
                Set(controller, "currentLightPlanePosition", new Vector2(4f, 5f));
                Set(controller, "initialWorldPosition", new Vector3(-10f, -20f, -30f));
                foreach (bool enabled in new[] { true, false, true, false })
                {
                    Set(controller, "autoFollowVelocity", new Vector2(2f, 3f));
                    Set(controller, "currentPlaneVelocity", new Vector2(1f, 2f));
                    Set(controller, "smoothedKeyboardInput", Vector2.one);
                    controller.SetAutoFollow(enabled);
                    Assert.That(controller.AutoFollowPlayer, Is.EqualTo(enabled));
                    Assert.That(owner.transform.position, Is.EqualTo(new Vector3(7f, 8f, 9f)));
                    Assert.That(Get(controller, "currentLightPlanePosition"), Is.EqualTo(new Vector2(4f, 5f)));
                    Assert.That(Get(controller, "initialWorldPosition"), Is.EqualTo(new Vector3(-10f, -20f, -30f)));
                    Assert.That(Get(controller, "autoFollowVelocity"), Is.EqualTo(Vector2.zero));
                    Assert.That(Get(controller, "currentPlaneVelocity"), Is.EqualTo(Vector2.zero));
                    Assert.That(Get(controller, "smoothedKeyboardInput"), Is.EqualTo(Vector2.zero));
                }
                Set(controller, "autoFollowPlayer", true);
                typeof(LightSourceController).GetMethod("SynchronizeControlMode",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Assert.That(owner.transform.position, Is.EqualTo(new Vector3(7f, 8f, 9f)));
                Assert.That(Get(controller, "autoFollowVelocity"), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static FieldInfo Field(string name) => typeof(LightSourceController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Get(LightSourceController controller, string name) => Field(name).GetValue(controller);
        private static void Set(LightSourceController controller, string name, object value) => Field(name).SetValue(controller, value);
    }
}
#endif
