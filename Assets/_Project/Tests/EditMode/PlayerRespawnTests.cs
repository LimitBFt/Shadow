#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using ShadowPath.Interaction;
using ShadowPath.Lighting;
using ShadowPath.Player;
using ShadowPath.Shadow;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace ShadowPath.Tests.EditMode
{
    public class PlayerRespawnTests
    {
        private GameObject root;
        private PlayerController player;
        private Rigidbody body;
        private PlayerRespawnController respawn;
        private GameObject world;
        private Transform plane;
        private LightSourceController light;
        private LightGameplayArea area;
        private BoxCollider ground;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Respawn Test");
            root.SetActive(false);
            GameObject playerObject = Child("Player");
            player = playerObject.AddComponent<PlayerController>();
            Set(player, "capsuleCollider", playerObject.GetComponent<CapsuleCollider>());
            body = playerObject.GetComponent<Rigidbody>();
            body.position = new Vector3(0f, 0f, 3f);
            body.constraints = RigidbodyConstraints.FreezePositionZ;
            respawn = playerObject.AddComponent<PlayerRespawnController>();
            plane = Child("Projection Plane").transform;
            GameObject lightObject = Child("Light");
            lightObject.transform.position = new Vector3(0f, 0f, -9f);
            light = lightObject.AddComponent<LightSourceController>();
            Set(light, "projectionPlane", plane);
            area = root.AddComponent<LightGameplayArea>();
            Set(area, "projectionPlane", plane);
            Set(area, "lightSource", lightObject.transform);
            Set(area, "player", player.transform);
            Set(area, "boundaryMode", LightGameplayArea.BoundaryMode.Manual);
            Set(area, "gameplayHalfWidth", 30f);
            world = new GameObject("Active Checkpoint World");
            var shadows = world.AddComponent<ShadowSystem>();
            Set(shadows, "autoDetectShadowCasters", false);
            Set(shadows, "lightSource", lightObject.transform);
            Set(shadows, "projectionPlane", plane);
            var floor = new GameObject("Fixed Ground");
            floor.transform.SetParent(world.transform);
            floor.transform.position = new Vector3(5f, -1.5f, 3f);
            ground = floor.AddComponent<BoxCollider>();
            ground.size = new Vector3(40f, 1f, 40f);
            Set(respawn, "lightGameplayArea", area);
            Set(respawn, "shadowSystems", new[] { shadows });
            Invoke(respawn, "Awake");
            Invoke(respawn, "Start");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(world);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RespawnRestoresExactSnapshotAndClearsMotionAndRideState()
        {
            light.SetManualControlEnabled(false);
            light.SetAutoFollow(true);
            var probe = player.gameObject.AddComponent<ShadowRideProbe>();
            Set(probe, "hasShadowSupport", true);
            var ride = player.gameObject.AddComponent<ShadowRideController>();
            Set(ride, "probe", probe);
            Set(ride, "isAttached", true);
            Invoke(respawn, "Awake");
            var rotatable = Child("Configured Rotatable").AddComponent<RotatableObject>();
            Invoke(rotatable, "Awake");
            rotatable.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Set(rotatable, "rotationStepIndex", 1);
            Set(respawn, "checkpointObjects", new[] { rotatable });

            Transform checkpoint = Child("Checkpoint").transform;
            checkpoint.SetPositionAndRotation(new Vector3(10f, 0f, 8f), Quaternion.Euler(0f, 90f, 0f));
            Quaternion savedRotation = checkpoint.rotation;
            respawn.SetCheckpoint(checkpoint);
            light.SetManualControlEnabled(true);
            light.SetAutoFollow(false);
            Set(light, "currentPlaneVelocity", Vector2.one);
            Set(light, "autoFollowVelocity", Vector2.one);
            checkpoint.SetPositionAndRotation(new Vector3(20f, 20f, 20f), Quaternion.identity);
            light.transform.position = new Vector3(40f, 0f, -9f);
            rotatable.transform.localRotation = Quaternion.Euler(0f, 0f, 135f);
            Set(rotatable, "rotationStepIndex", 3);
            Set(player, "pendingExternalDisplacement", Vector3.one);
            Set(player, "consumedExternalDisplacement", Vector3.one);
            Set(player, "jumpRequested", true);
            Set(player, "moveInput", 1f);
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;

            respawn.Respawn();

            Assert.That(body.position, Is.EqualTo(new Vector3(10f, 0f, 3f)));
            Assert.That(Quaternion.Angle(body.rotation, savedRotation), Is.LessThan(0.01f));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints.FreezePositionZ));
            Assert.That(Get(player, "pendingExternalDisplacement"), Is.EqualTo(Vector3.zero));
            Assert.That(Get(player, "consumedExternalDisplacement"), Is.EqualTo(Vector3.zero));
            Assert.That(Get(player, "jumpRequested"), Is.False);
            Assert.That(Get(player, "moveInput"), Is.EqualTo(0f));
            Assert.That(ride.IsAttached, Is.False);
            Assert.That(probe.HasShadowSupport, Is.False);
            Assert.That(light.ManualControlEnabled, Is.True);
            Assert.That(light.AutoFollowPlayer, Is.False);
            Assert.That(Get(light, "currentPlaneVelocity"), Is.EqualTo(Vector2.zero));
            Assert.That(Get(light, "autoFollowVelocity"), Is.EqualTo(Vector2.zero));
            Assert.That(light.transform.position.z, Is.EqualTo(-9f));
            Assert.That(light.transform.position.x, Is.Zero, "Light pose must be exact, independent of the old Player circle.");
            Assert.That(Quaternion.Angle(rotatable.transform.localRotation, Quaternion.Euler(0f, 0f, 45f)), Is.LessThan(0.01f));
            Assert.That(Get(rotatable, "rotationStepIndex"), Is.EqualTo(1));
            Assert.That(area.TryGetMutualCircle(out Vector2 sample, out Vector2 center, out float radius), Is.True);
            Assert.That(CircleGameplayConstraint.Contains(sample.x, sample.y, center.x, center.y, radius), Is.True);
        }

        [Test]
        public void AutomaticCheckpointCapturesRootPoseAndAllowsFootContact()
        {
            Vector3 rootPosition = new Vector3(3f, 0f, 3f);
            Quaternion rootRotation = Quaternion.Euler(0f, 90f, 0f);
            player.transform.SetPositionAndRotation(rootPosition, rootRotation);
            body.linearVelocity = Vector3.zero;

            Assert.That(respawn.TrySetCheckpointFromPlayer(), Is.True);
            Vector3 savedPosition = (Vector3)Get(respawn, "checkpointPosition");
            Assert.That(savedPosition.x, Is.EqualTo(rootPosition.x).Within(0.0001f));
            Assert.That(savedPosition.z, Is.EqualTo(rootPosition.z).Within(0.0001f));
            Assert.That(savedPosition.y, Is.EqualTo(0.01f).Within(0.002f));
            Assert.That(Vector3.Distance(savedPosition, rootPosition), Is.LessThanOrEqualTo(0.1f));
            Assert.That(Quaternion.Angle((Quaternion)Get(respawn, "checkpointRotation"), rootRotation), Is.LessThan(0.01f));
            body.position = new Vector3(10f, 4f, 3f);
            respawn.Respawn();
            Assert.That(body.position, Is.EqualTo(savedPosition));
        }

        [TestCase(0.5f, 0f)]
        [TestCase(0f, 7f)]
        [TestCase(-0.25f, 0f)]
        public void AutomaticCheckpointRejectsAirborneOrDeeplyPenetratingPlayer(float height, float verticalSpeed)
        {
            object previousSnapshot = Get(respawn, "checkpointSnapshot");
            player.transform.position = new Vector3(0f, height, 3f);
            body.linearVelocity = Vector3.up * verticalSpeed;
            LogAssert.Expect(LogType.Warning, new Regex("PlayerPosition checkpoint was not saved"));
            Assert.That(respawn.TrySetCheckpointFromPlayer(), Is.False);
            Assert.That(Get(respawn, "checkpointSnapshot"), Is.SameAs(previousSnapshot));
        }

        [Test]
        public void AutomaticCheckpointCorrectsSmallContactPenetration()
        {
            player.transform.position = new Vector3(0f, -0.005f, 3f);
            body.linearVelocity = Vector3.zero;
            Assert.That(respawn.TrySetCheckpointFromPlayer(), Is.True);
            Assert.That(((Vector3)Get(respawn, "checkpointPosition")).y, Is.EqualTo(0.01f).Within(0.002f));
        }

        [Test]
        public void AutomaticCheckpointCanUseAValidatedManualFallback()
        {
            player.transform.position = new Vector3(0f, 2f, 3f);
            Transform point = Child("Manual Fallback").transform;
            point.position = new Vector3(5f, 0f, 3f);
            LogAssert.Expect(LogType.Warning, new Regex("PlayerPosition checkpoint was not saved.*Trying Manual"));
            Assert.That(respawn.TrySetCheckpointFromPlayer(point), Is.True);
            Assert.That(Get(respawn, "checkpointPosition"), Is.EqualTo(point.position));
        }

        [Test]
        public void InvalidAutomaticAndManualPosesKeepPreviousSnapshot()
        {
            object previousSnapshot = Get(respawn, "checkpointSnapshot");
            player.transform.position = new Vector3(0f, 2f, 3f);
            Transform point = Child("Unsafe Manual Fallback").transform;
            point.position = new Vector3(0f, -0.5f, 3f);
            LogAssert.Expect(LogType.Warning, new Regex("PlayerPosition checkpoint was not saved"));
            LogAssert.Expect(LogType.Warning, new Regex("Manual checkpoint was not saved"));
            Assert.That(respawn.TrySetCheckpointFromPlayer(point), Is.False);
            Assert.That(Get(respawn, "checkpointSnapshot"), Is.SameAs(previousSnapshot));
        }

        [Test]
        public void AutomaticCheckpointRejectsPoseOutsideCurrentGameplayCircle()
        {
            object previousSnapshot = Get(respawn, "checkpointSnapshot");
            Set(area, "gameplayHalfWidth", 2f);
            player.transform.position = new Vector3(10f, 0f, 3f);
            LogAssert.Expect(LogType.Warning, new Regex("PlayerPosition checkpoint was not saved.*Gameplay Circle"));
            Assert.That(respawn.TrySetCheckpointFromPlayer(), Is.False);
            Assert.That(Get(respawn, "checkpointSnapshot"), Is.SameAs(previousSnapshot));
        }

        [Test]
        public void TouchingShadowRideSupportAllowsCarryVelocityButNotAnAirGap()
        {
            var support = ground.gameObject.AddComponent<GeneratedShadow>();
            Set(support, "hasValidGeometry", true);
            var ride = player.gameObject.AddComponent<ShadowRideController>();
            Set(ride, "isAttached", true);
            Set(ride, "attachedShadow", support);
            Set(respawn, "shadowRide", ride);
            body.linearVelocity = Vector3.up * 2f;
            Assert.That(respawn.TrySetCheckpointFromPlayer(), Is.True);
            object previousSnapshot = Get(respawn, "checkpointSnapshot");
            player.transform.position = new Vector3(0f, 0.05f, 3f);
            LogAssert.Expect(LogType.Warning, new Regex("PlayerPosition checkpoint was not saved"));
            Assert.That(respawn.TrySetCheckpointFromPlayer(), Is.False);
            Assert.That(Get(respawn, "checkpointSnapshot"), Is.SameAs(previousSnapshot));
        }

        [Test]
        public void AutomaticCheckpointHonorsMaximumWalkableSlope()
        {
            Quaternion slopeRotation = Quaternion.Euler(0f, 0f, 45f);
            Vector3 normal = slopeRotation * Vector3.up;
            CapsuleCollider playerCapsule = player.GetComponent<CapsuleCollider>();
            float halfSegment = playerCapsule.height * 0.5f - playerCapsule.radius;
            float capsuleExtent = playerCapsule.radius + halfSegment * Vector3.Dot(Vector3.up, normal);
            ground.transform.SetPositionAndRotation(new Vector3(0f, 0f, 3f)
                - normal * (capsuleExtent + ground.size.y * 0.5f), slopeRotation);
            Set(respawn, "maxWalkableSlopeAngle", 30f);
            LogAssert.Expect(LogType.Warning, new Regex("PlayerPosition checkpoint was not saved"));
            Assert.That(respawn.TrySetCheckpointFromPlayer(), Is.False);
            Set(respawn, "maxWalkableSlopeAngle", 60f);
            Assert.That(respawn.TrySetCheckpointFromPlayer(), Is.True);
        }

        [Test]
        public void LowerBoundUsesRotatedPlaneUpAndCanBeDisabled()
        {
            plane.rotation = Quaternion.Euler(0f, 0f, 90f);
            ground.transform.position = new Vector3(1f, 0f, 3f);
            ground.size = new Vector3(1f, 40f, 40f);
            Set(respawn, "projectionPlane", plane);
            Set(respawn, "useLevelLowerBound", true);
            Set(respawn, "minimumPlaneHeight", -10f);
            Invoke(respawn, "Awake");
            body.position = new Vector3(20f, 100f, 3f);
            Invoke(respawn, "FixedUpdate");
            Assert.That(body.position, Is.EqualTo(new Vector3(0f, 0f, 3f)));
            body.position = new Vector3(0f, -100f, 3f);
            Invoke(respawn, "FixedUpdate");
            Assert.That(body.position.y, Is.EqualTo(-100f));
            Set(respawn, "useLevelLowerBound", false);
            body.position = new Vector3(20f, 100f, 3f);
            Invoke(respawn, "FixedUpdate");
            Assert.That(body.position.x, Is.EqualTo(20f));
        }

        [Test]
        public void SnapshotRebuildPublishesNewGeometryAndColliderBeforePlayerIsReleased()
        {
            plane.SetParent(world.transform, true);
            GameObject casterObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            casterObject.transform.SetParent(world.transform);
            casterObject.transform.position = new Vector3(0f, 0f, -4f);
            var caster = casterObject.AddComponent<ShadowCaster>();
            var rotatable = casterObject.AddComponent<RotatableObject>();
            Invoke(rotatable, "Awake");
            ShadowSystem system = world.GetComponent<ShadowSystem>();
            Set(system, "shadowCasters", new List<ShadowCaster> { caster });
            Invoke(system, "InitializeShadows");
            var generated = (Dictionary<ShadowCaster, GeneratedShadow>)Get(system, "generatedShadows");
            GeneratedShadow shadow = generated[caster];
            Set(respawn, "checkpointObjects", new[] { rotatable });
            Transform point = Child("Checkpoint").transform;
            point.position = new Vector3(0f, 0f, 3f);
            respawn.SetCheckpoint(point);

            light.transform.position = new Vector3(8f, 4f, -9f);
            rotatable.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Assert.That(system.TryRebuildForCheckpoint(out string reason), Is.True, reason);
            int changedVersion = shadow.GeometryVersion;
            respawn.Respawn();
            Assert.That(shadow.GeometryVersion, Is.GreaterThan(changedVersion));
            Assert.That(shadow.HasCurrentCollider, Is.True);
            Assert.That(light.transform.position, Is.EqualTo(new Vector3(0f, 0f, -9f)));
            Assert.That(Quaternion.Angle(rotatable.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(body.isKinematic, Is.False);
        }

        [Test]
        public void UnsupportedCheckpointRollsBackEnvironmentAndKeepsPlayerAtOriginalPose()
        {
            Transform point = Child("Unsupported Checkpoint").transform;
            point.position = new Vector3(5f, 10f, 3f);
            // Inject an invalid saved pose to exercise the restore-time safety net.
            Assert.That(Invoke(respawn, "CaptureCheckpoint", point.position, point.rotation), Is.True);
            Vector3 previousLight = new Vector3(2f, 3f, -9f);
            light.transform.position = previousLight;
            Vector3 previousPlayer = body.position;
            LogAssert.Expect(LogType.Error, new Regex("Respawn failed.*no valid support"));
            respawn.Respawn();
            Assert.That(body.position, Is.EqualTo(previousPlayer));
            Assert.That(light.transform.position, Is.EqualTo(previousLight));
            Assert.That(body.isKinematic, Is.False);
        }

        [Test]
        public void CheckpointInsideSolidColliderIsRejectedWithoutTeleporting()
        {
            Transform point = Child("Overlapping Checkpoint").transform;
            point.position = new Vector3(0f, -0.5f, 3f);
            Assert.That(Invoke(respawn, "CaptureCheckpoint", point.position, point.rotation), Is.True);
            Vector3 previousPlayer = body.position;
            LogAssert.Expect(LogType.Error, new Regex("Respawn failed.*overlaps Fixed Ground"));
            respawn.Respawn();
            Assert.That(body.position, Is.EqualTo(previousPlayer));
            Assert.That(body.isKinematic, Is.False);
        }

        [Test]
        public void UnsupportedCheckpointUsesOnlyAValidatedConfiguredFallback()
        {
            Transform safe = Child("Fixed Safe Pose").transform;
            safe.position = new Vector3(0f, 0f, 3f);
            Set(respawn, "fixedSafeRespawnPoint", safe);
            Transform point = Child("Unsupported Checkpoint").transform;
            point.position = new Vector3(5f, 10f, 3f);
            Assert.That(Invoke(respawn, "CaptureCheckpoint", point.position, point.rotation), Is.True);
            safe.position = new Vector3(0f, 20f, 3f);
            body.position = new Vector3(10f, 0f, 3f);
            respawn.Respawn();
            Assert.That(body.position, Is.EqualTo(new Vector3(0f, 0f, 3f)));
            Assert.That(body.isKinematic, Is.False);
        }

        [Test]
        public void RespawnExitsInteractionAndForgetsDistantStation()
        {
            var interaction = player.gameObject.AddComponent<PlayerInteractionController>();
            Set(interaction, "playerController", player);
            GameObject stationObject = Child("Station");
            stationObject.transform.position = new Vector3(20f, 0f, 3f);
            stationObject.AddComponent<BoxCollider>().isTrigger = true;
            var station = stationObject.AddComponent<InteractionStation>();
            Set(interaction, "nearbyStation", station);
            Set(interaction, "currentStation", station);
            Set(interaction, "state", InteractionState.Rotating);
            Set(interaction, "selectedIndex", 2);
            Invoke(respawn, "Awake");
            respawn.Respawn();
            Assert.That(interaction.IsInteracting, Is.False);
            Assert.That(Get(interaction, "nearbyStation"), Is.Null);
            Assert.That(Get(interaction, "currentStation"), Is.Null);
            Assert.That(Get(interaction, "selectedIndex"), Is.EqualTo(-1));
        }

        [Test]
        public void DeathZoneRecognizesPlayerThroughChildCollider()
        {
            Invoke(respawn, "Awake");
            GameObject childCollider = Child("Player Collider");
            childCollider.transform.SetParent(player.transform, false);
            Collider collider = childCollider.AddComponent<BoxCollider>();
            var zoneObject = new GameObject("Death Zone");
            try
            {
                var zone = zoneObject.AddComponent<DeathZone>();
                body.position = new Vector3(2f, -20f, 3f);
                Invoke(zone, "OnTriggerEnter", collider);
                Assert.That(body.position, Is.EqualTo(new Vector3(0f, 0f, 3f)));
            }
            finally
            {
                Object.DestroyImmediate(zoneObject);
            }
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static object Get(object target, string name) => Field(target, name).GetValue(target);
        private static FieldInfo Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
#endif
