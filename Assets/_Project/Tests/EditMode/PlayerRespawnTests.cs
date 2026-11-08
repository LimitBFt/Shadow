#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using ShadowPath.Interaction;
using ShadowPath.Lighting;
using ShadowPath.Player;
using UnityEngine;

namespace ShadowPath.Tests.EditMode
{
    public class PlayerRespawnTests
    {
        private GameObject root;
        private PlayerController player;
        private Rigidbody body;
        private PlayerRespawnController respawn;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Respawn Test");
            root.SetActive(false);
            GameObject playerObject = Child("Player");
            player = playerObject.AddComponent<PlayerController>();
            body = playerObject.GetComponent<Rigidbody>();
            body.position = new Vector3(0f, 0f, 3f);
            body.constraints = RigidbodyConstraints.FreezePositionZ;
            respawn = playerObject.AddComponent<PlayerRespawnController>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void RespawnClearsMotionAndRideStateAndRestoresLightInsideCircle()
        {
            Transform plane = Child("Projection Plane").transform;
            GameObject lightObject = Child("Light");
            lightObject.transform.position = new Vector3(0f, 0f, -9f);
            var light = lightObject.AddComponent<LightSourceController>();
            Set(light, "projectionPlane", plane);
            Set(light, "manualControlEnabled", true);
            Set(light, "autoFollowPlayer", false);
            Set(light, "currentPlaneVelocity", Vector2.one);
            Set(light, "autoFollowVelocity", Vector2.one);
            var area = root.AddComponent<LightGameplayArea>();
            Set(area, "projectionPlane", plane);
            Set(area, "lightSource", lightObject.transform);
            Set(area, "player", player.transform);
            Set(area, "boundaryMode", LightGameplayArea.BoundaryMode.Manual);
            Set(area, "gameplayHalfWidth", 2f);
            Set(respawn, "lightGameplayArea", area);
            var probe = player.gameObject.AddComponent<ShadowRideProbe>();
            Set(probe, "hasShadowSupport", true);
            var ride = player.gameObject.AddComponent<ShadowRideController>();
            Set(ride, "probe", probe);
            Set(ride, "isAttached", true);
            Invoke(respawn, "Awake");

            Transform checkpoint = Child("Checkpoint").transform;
            checkpoint.SetPositionAndRotation(new Vector3(10f, 0f, 8f), Quaternion.Euler(0f, 90f, 0f));
            respawn.SetCheckpoint(checkpoint);
            Set(player, "pendingExternalDisplacement", Vector3.one);
            Set(player, "consumedExternalDisplacement", Vector3.one);
            Set(player, "jumpRequested", true);
            Set(player, "moveInput", 1f);
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;

            respawn.Respawn();

            Assert.That(body.position, Is.EqualTo(new Vector3(10f, 0f, 3f)));
            Assert.That(Quaternion.Angle(body.rotation, checkpoint.rotation), Is.LessThan(0.01f));
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
            Assert.That(area.TryGetMutualCircle(out Vector2 sample, out Vector2 center, out float radius), Is.True);
            Assert.That(CircleGameplayConstraint.Contains(sample.x, sample.y, center.x, center.y, radius), Is.True);
        }

        [Test]
        public void LowerBoundUsesRotatedPlaneUpAndCanBeDisabled()
        {
            Transform plane = Child("Projection Plane").transform;
            plane.rotation = Quaternion.Euler(0f, 0f, 90f);
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
        private static void Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
#endif
