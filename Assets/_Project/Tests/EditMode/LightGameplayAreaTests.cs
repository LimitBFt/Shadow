#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShadowPath.Lighting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowPath.Tests.EditMode
{
    public class LightGameplayAreaTests
    {
        private GameObject root;
        private Transform light;
        private Transform plane;
        private Transform player;
        private LightGameplayArea area;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Light constraint test");
            root.SetActive(false);
            plane = CreateTransform("Plane");
            light = CreateTransform("Light");
            player = CreateTransform("Player");
            area = root.AddComponent<LightGameplayArea>();
            SetField("boundaryMode", LightGameplayArea.BoundaryMode.Manual);
            SetField("projectionPlane", plane);
            SetField("lightSource", light);
            SetField("player", player);
            SetField("gameplayHalfWidth", 3.5f);
            SetField("playerSafetyMargin", 0.25f);
            player.position = new Vector3(-9.77841f, 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AllowedRange_UsesTheSameEffectiveRadiusAsPlayer()
        {
            Assert.That(area.TryGetAllowedLightRange(out float minimum, out float maximum), Is.True);
            Assert.That(minimum, Is.EqualTo(-13.27841f).Within(0.00001f));
            Assert.That(maximum, Is.EqualTo(-6.27841f).Within(0.00001f));
        }

        [Test]
        public void InvalidInitialPosition_WarnsButAllowsInputSizedRecovery()
        {
            light.position = new Vector3(-2.48877f, 1f, -9f);
            Vector3 initialWorldPosition = light.position;
            LogAssert.Expect(LogType.Warning, new Regex("Initial Light gameplay constraint is INVALID"));
            Assert.That(area.InitializeLightConstraint(), Is.False);
            Assert.That(area.InitializeLightConstraint(), Is.False);
            Vector2 proposed = new Vector2(-2.5f, 1f);
            Assert.That(area.ClampLightPlanePosition(proposed), Is.EqualTo(proposed));
            Assert.That(light.position, Is.EqualTo(initialWorldPosition));
            // The debug snapshot remains invalid; it no longer latches movement off.
            player.position = light.position;
            Assert.That(area.InitializeLightConstraint(), Is.False);
        }

        [Test]
        public void BeforeInitialization_ClampDoesNotCorrectInitialLight()
        {
            light.position = new Vector3(-2.48877f, 1f, 0f);
            Assert.That(area.ClampLightPlanePosition(new Vector2(-2.5f, 1f)),
                Is.EqualTo(new Vector2(-2.48877f, 1f)));
        }

        [Test]
        public void ValidInitialPosition_FirstInteriorMoveHasZeroCorrection()
        {
            light.position = new Vector3(-9.77841f, 1f, -9f);
            Vector3 initialWorldPosition = light.position;
            Assert.That(area.InitializeLightConstraint(), Is.True);
            Vector2 proposed = new Vector2(-9.7f, 1.03f);
            Assert.That(area.ClampLightPlanePosition(proposed) - proposed, Is.EqualTo(Vector2.zero));
            Assert.That(light.position, Is.EqualTo(initialWorldPosition));
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void Boundary_RepeatedOutwardInputThenReverseMovesImmediately(float direction)
        {
            area.TryGetAllowedLightRange(out float minimum, out float maximum);
            float boundary = direction > 0f ? maximum : minimum;
            light.position = new Vector3(boundary, 0f, 0f);
            Assert.That(area.InitializeLightConstraint(), Is.True);
            Vector2 current = new Vector2(boundary, 0f);
            for (int i = 0; i < 120; i++)
            {
                current = area.ClampLightPlanePosition(current + Vector2.right * direction * 0.1f);
                Assert.That(current.x, Is.EqualTo(boundary));
            }

            Vector2 reversed = area.ClampLightPlanePosition(current - Vector2.right * direction * 0.1f);
            Assert.That(reversed.x, Is.EqualTo(boundary - direction * 0.1f).Within(0.00001f));
        }

        private Transform CreateTransform(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            return child.transform;
        }

        [Test]
        public void SpotMode_UsesLightAngleAndDistanceWithoutChangingManualWidth()
        {
            Light spot = ConfigureSpot();
            spot.spotAngle = 90f;
            Assert.That(area.EffectiveGameplayHalfWidth, Is.EqualTo(9.8f).Within(0.00001f));
            spot.spotAngle = 50f;
            float expected = 10f * Mathf.Tan(25f * Mathf.Deg2Rad) - 0.2f;
            Assert.That(area.EffectiveGameplayHalfWidth, Is.EqualTo(expected).Within(0.00001f));
            plane.position = Vector3.forward * 2f;
            expected = 12f * Mathf.Tan(25f * Mathf.Deg2Rad) - 0.2f;
            Assert.That(area.EffectiveGameplayHalfWidth, Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void SpotMode_ConstrainsBothPlaneAxesAndPreservesWorldZ()
        {
            ConfigureSpot();
            Vector3 proposed = light.position + new Vector3(100f, 20f, 5f);
            Vector3 constrained = area.ConstrainPlayerWorldPosition(light.position, proposed);
            Vector2 offset = new Vector2(constrained.x - light.position.x, constrained.y - light.position.y);
            Assert.That(offset.magnitude, Is.LessThanOrEqualTo(area.EffectiveGameplayHalfWidth + 0.00001f));
            Assert.That(constrained.y, Is.LessThan(proposed.y));
            Assert.That(constrained.z, Is.EqualTo(proposed.z));
        }

        [Test]
        public void SpotMode_InvalidRangeDoesNotFallBackToManualWidth()
        {
            Light spot = ConfigureSpot();
            spot.range = 5f;
            Assert.That(area.EffectiveGameplayHalfWidth, Is.Zero);
            Assert.That(area.TryGetAllowedLightRange(out _, out _), Is.False);
        }

        [Test]
        public void TargetRadiusMode_UpdatesAnglesAndBoundaryFromOneRadius()
        {
            Light spot = ConfigureSpot();
            SetField("lightSizeMode", LightGameplayArea.LightSizeMode.TargetRadius);
            SetField("targetLightRadius", 3.8f);
            SetField("innerAngleRatio", 0.75f);
            Assert.That(area.EffectiveGameplayHalfWidth, Is.EqualTo(3.6f).Within(0.00001f));
            float expectedAngle = 2f * Mathf.Atan(3.8f / 10f) * Mathf.Rad2Deg;
            Assert.That(spot.spotAngle, Is.EqualTo(expectedAngle).Within(0.00001f));
            Assert.That(spot.innerSpotAngle, Is.EqualTo(expectedAngle * 0.75f).Within(0.00001f));

            plane.position = Vector3.forward * 2f;
            Assert.That(area.EffectiveGameplayHalfWidth, Is.EqualTo(3.6f).Within(0.00001f));
            Assert.That(spot.spotAngle, Is.LessThan(expectedAngle));
            Assert.That(spot.innerSpotAngle, Is.EqualTo(spot.spotAngle * 0.75f).Within(0.00001f));
        }

        [Test]
        public void TargetRadiusMode_AngleLimitNeverCreatesBoundaryOutsideActualLight()
        {
            Light spot = ConfigureSpot();
            SetField("lightSizeMode", LightGameplayArea.LightSizeMode.TargetRadius);
            SetField("targetLightRadius", 100000f);
            Assert.That(area.EffectiveGameplayHalfWidth,
                Is.EqualTo(10f * Mathf.Tan(60f * Mathf.Deg2Rad) - 0.2f).Within(0.0001f));
            Assert.That(spot.spotAngle, Is.EqualTo(120f));
        }

        [Test]
        public void TargetRadiusMode_SmallRadiusCannotInvertBoundary()
        {
            ConfigureSpot();
            SetField("lightSizeMode", LightGameplayArea.LightSizeMode.TargetRadius);
            SetField("targetLightRadius", 0.01f);
            Assert.That(area.EffectiveGameplayHalfWidth, Is.Zero);
        }

        private Light ConfigureSpot()
        {
            light.position = new Vector3(player.position.x, 1f, -10f);
            Transform child = CreateTransform("VisualSpotLight");
            child.SetParent(light, false);
            Light spot = child.gameObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 50f;
            spot.range = 30f;
            SetField("spotLight", spot);
            SetField("lightSizeMode", LightGameplayArea.LightSizeMode.SpotAngle);
            SetField("boundaryMode", LightGameplayArea.BoundaryMode.FromSpotLight);
            SetField("gameplayBoundaryInset", 0.2f);
            return spot;
        }

        [Test]
        public void CircleBoundary_AllowsTangentSlidingAndInwardRecovery()
        {
            ConfigureSpot();
            float radius = area.EffectiveGameplayHalfWidth;
            Vector3 current = new Vector3(light.position.x + radius, light.position.y, 0f);
            Vector3 result = area.ConstrainPlayerWorldPosition(current, current + Vector3.up * 0.1f);
            Assert.That(result.y, Is.GreaterThan(current.y));
            Assert.That(Vector2.Distance(new Vector2(result.x, result.y), new Vector2(light.position.x, light.position.y)),
                Is.LessThanOrEqualTo(radius + 0.00001f));
            Assert.That(area.ConstrainPlayerWorldPosition(current, current - Vector3.right * 0.1f),
                Is.EqualTo(current - Vector3.right * 0.1f));
        }

        [Test]
        public void PlayerCircle_TopStopsUpwardJumpButImmediatelyAllowsFalling()
        {
            ConfigureSpot();
            Vector3 top = new Vector3(light.position.x, light.position.y + area.EffectiveGameplayHalfWidth, 0f);
            area.ConstrainPlayerMovement(top, Vector3.zero, Vector3.up * 7f, 0.02f,
                out Vector3 external, out Vector3 velocity);
            Assert.That(velocity.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(external.magnitude, Is.LessThan(0.00001f));
            area.ConstrainPlayerMovement(top, Vector3.zero, Vector3.down, 0.02f, out external, out velocity);
            Assert.That(velocity, Is.EqualTo(Vector3.down));
            Assert.That(external, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PlayerCircle_NormalInteriorJumpIsUnchanged()
        {
            ConfigureSpot();
            Vector3 center = new Vector3(light.position.x, light.position.y, 0f);
            area.ConstrainPlayerMovement(center, Vector3.zero, Vector3.up * 7f, 0.02f,
                out Vector3 external, out Vector3 velocity);
            Assert.That(velocity, Is.EqualTo(Vector3.up * 7f));
            Assert.That(external, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PlayerCircle_ExternalCarryAndVelocityShareOneFinalSweep()
        {
            ConfigureSpot();
            Vector3 center = new Vector3(light.position.x, light.position.y, 0f);
            Vector3 current = center + Vector3.right * (area.EffectiveGameplayHalfWidth - 0.1f);
            area.ConstrainPlayerMovement(current, new Vector3(1f, 0.2f, 0f), new Vector3(2f, 3f, 0f), 0.02f,
                out Vector3 external, out Vector3 velocity);
            Vector3 final = current + external + velocity * 0.02f;
            Assert.That(Vector3.Distance(final, center), Is.LessThanOrEqualTo(area.EffectiveGameplayHalfWidth + 0.00001f));
            Assert.That(Vector3.Dot(velocity, (final - center).normalized), Is.LessThanOrEqualTo(0.00001f));
        }

        [Test]
        public void CircleInitialization_ReportsVerticalInvalidityButAllowsRecoveryWithoutSnap()
        {
            ConfigureSpot();
            player.position = new Vector3(light.position.x, 20f, 0f);
            Vector2 current = new Vector2(light.position.x, light.position.y);
            LogAssert.Expect(LogType.Warning, new Regex("Initial Light gameplay constraint is INVALID"));
            Assert.That(area.InitializeLightConstraint(), Is.False);
            Assert.That(area.TryConstrainLightPlanePosition(current, current + Vector2.up * 0.1f,
                Vector2.one * -100f, Vector2.one * 100f, out Vector2 final), Is.True);
            Assert.That(final, Is.EqualTo(current + Vector2.up * 0.1f));
        }

        [Test]
        public void CircleLightSafety_RespectsBothAxesAndLevelBounds()
        {
            ConfigureSpot();
            Assert.That(area.InitializeLightConstraint(), Is.True);
            Vector2 current = new Vector2(light.position.x, light.position.y);
            Vector2 minimum = current - new Vector2(1f, 2f);
            Vector2 maximum = current + new Vector2(2f, 3f);
            Vector2 firstInput = current + Vector2.up * 0.1f;
            Assert.That(area.TryConstrainLightPlanePosition(current, firstInput, minimum, maximum,
                out Vector2 firstFinal), Is.True);
            Assert.That(firstFinal, Is.EqualTo(firstInput));
            Assert.That(area.TryConstrainLightPlanePosition(current, current + Vector2.one * 100f,
                minimum, maximum, out Vector2 final), Is.True);
            Assert.That(Vector2.Distance(final, new Vector2(player.position.x, player.position.y)),
                Is.LessThanOrEqualTo(area.EffectiveGameplayHalfWidth + 0.00001f));
            Assert.That(final.x, Is.InRange(minimum.x, maximum.x));
            Assert.That(final.y, Is.InRange(minimum.y, maximum.y));
        }

        [Test]
        public void PlayerJumpingOutsideCircle_LightOnlyTakesRecoveryInputSteps()
        {
            ConfigureSpot();
            Assert.That(area.InitializeLightConstraint(), Is.True);
            Vector2 current = new Vector2(light.position.x, light.position.y);
            player.position += Vector3.up * 20f;
            Vector2 minimum = Vector2.one * -100f;
            Vector2 maximum = Vector2.one * 100f;
            Assert.That(area.TryConstrainLightPlanePosition(current, current + Vector2.up * 0.1f,
                minimum, maximum, out Vector2 final), Is.True);
            Assert.That(final, Is.EqualTo(current + Vector2.up * 0.1f));
            area.TryConstrainLightPlanePosition(current, current - Vector2.up * 0.1f, minimum, maximum, out final);
            Assert.That(final, Is.EqualTo(current));
        }

        [Test]
        public void LightApproachingPlayerOnLeft_IsAllowedWithVerticalSeparation()
        {
            ConfigureSpot();
            light.position += Vector3.right * 3f;
            Assert.That(area.InitializeLightConstraint(), Is.True);
            Vector2 current = new Vector2(light.position.x, light.position.y);
            Vector2 proposed = current + Vector2.left * 0.1f;
            Assert.That(area.TryConstrainLightPlanePosition(current, proposed, out Vector2 final), Is.True);
            Assert.That(final, Is.EqualTo(proposed));
        }

        [Test]
        public void PlayerOutsideCircle_InwardPhysicsStepIsPreservedWithoutSnap()
        {
            ConfigureSpot();
            Vector3 current = new Vector3(light.position.x,
                light.position.y + area.EffectiveGameplayHalfWidth + 0.02f, 0f);
            Vector3 proposedVelocity = Vector3.down * 0.1f;
            area.ConstrainPlayerMovement(current, Vector3.zero, proposedVelocity, 0.02f,
                out Vector3 external, out Vector3 velocity);
            Assert.That(velocity, Is.EqualTo(proposedVelocity));
            Assert.That(external.magnitude, Is.LessThan(0.000001f));
        }

        private void SetField(string name, object value)
        {
            typeof(LightGameplayArea).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(area, value);
        }
    }
}
#endif
