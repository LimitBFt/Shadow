#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using ShadowPath.Camera;
using ShadowPath.Interaction;
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

        [Test]
        public void InteractionCameraPreservesManualTargetAcrossSelectingAndRotating()
        {
            var owner = new GameObject("Interaction Camera Test");
            owner.SetActive(false);
            var player = new GameObject("Player");
            var plane = new GameObject("Plane");
            var stationObject = new GameObject("Station");
            try
            {
                var source = player.AddComponent<PlayerInteractionController>();
                stationObject.AddComponent<BoxCollider>();
                var station = stationObject.AddComponent<InteractionStation>();
                stationObject.transform.position = new Vector3(4f, 1f, 0f);
                var controller = owner.AddComponent<CameraFollowController>();
                Set(controller, "target", player.transform);
                Set(controller, "projectionPlane", plane.transform);
                Set(controller, "planeNormalOffset", -15f);
                Invoke(controller, "TrySnapToTarget");
                Set(controller, "targetPlaneNormalOffset", -12f);
                SetInteractionState(source, InteractionState.Selecting, station);
                Invoke(controller, "UpdateInteractionStation");
                Assert.That(Get(controller, "savedManualZoomOffset"), Is.EqualTo(-12f));
                for (int i = 0; i < 150; i++)
                {
                    Invoke(controller, "UpdateManualZoom", 0.02f);
                    Invoke(controller, "UpdateInteractionComposition", Vector3.zero, plane.transform.forward, 0.02f);
                }
                Assert.That((float)Get(controller, "currentPlaneNormalOffset")
                    + (float)Get(controller, "interactionZoomDelta"), Is.EqualTo(-18f).Within(0.01f));
                Assert.That(Get(controller, "targetPlaneNormalOffset"), Is.EqualTo(-12f));

                Vector3 focusBefore = (Vector3)Get(controller, "interactionFocusOffset");
                SetInteractionState(source, InteractionState.Rotating, station);
                Invoke(controller, "UpdateInteractionStation");
                Assert.That(Get(controller, "interactionFocusOffset"), Is.EqualTo(focusBefore));
                Assert.That(Get(controller, "savedManualZoomOffset"), Is.EqualTo(-12f));
                SetInteractionState(source, InteractionState.Selecting, station);
                Invoke(controller, "UpdateInteractionStation");
                Assert.That(Get(controller, "interactionCameraActive"), Is.True);

                source.ExitInteractionMode();
                Invoke(controller, "UpdateInteractionStation");
                Assert.That(Get(controller, "interactionCameraActive"), Is.False);
                for (int i = 0; i < 300; i++)
                {
                    Invoke(controller, "UpdateManualZoom", 0.02f);
                    // A moving normal focus must not prevent the return transition from finishing.
                    Invoke(controller, "UpdateInteractionComposition", new Vector3(i * 0.1f, 0f, 0f),
                        plane.transform.forward, 0.02f);
                }
                Assert.That(Get(controller, "interactionTransitionActive"), Is.False);
                Assert.That(Get(controller, "targetPlaneNormalOffset"), Is.EqualTo(-12f));
                Assert.That(Get(controller, "currentPlaneNormalOffset"), Is.EqualTo(-12f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(stationObject);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(plane);
            }
        }

        [Test]
        public void InterruptedInteractionTransitionsPreserveCurrentOffsetsAndReleaseOnDisable()
        {
            var owner = new GameObject("Camera Lifecycle Test");
            owner.SetActive(false);
            var player = new GameObject("Player");
            var plane = new GameObject("Plane");
            var stationObject = new GameObject("Station");
            try
            {
                var source = player.AddComponent<PlayerInteractionController>();
                stationObject.AddComponent<BoxCollider>();
                var station = stationObject.AddComponent<InteractionStation>();
                stationObject.transform.position = new Vector3(6f, 2f, 0f);
                var controller = owner.AddComponent<CameraFollowController>();
                Set(controller, "target", player.transform);
                Set(controller, "projectionPlane", plane.transform);
                Set(controller, "planeNormalOffset", -15f);
                Invoke(controller, "TrySnapToTarget");
                Set(controller, "targetPlaneNormalOffset", -12f);
                SetInteractionState(source, InteractionState.Selecting, station);
                Invoke(controller, "UpdateInteractionStation");
                Invoke(controller, "UpdateInteractionComposition", Vector3.zero, plane.transform.forward, 0.1f);
                Vector3 offset = (Vector3)Get(controller, "interactionFocusOffset");
                float zoomDelta = (float)Get(controller, "interactionZoomDelta");
                Vector3 position = owner.transform.position;
                source.ExitInteractionMode();
                Invoke(controller, "UpdateInteractionStation");
                SetInteractionState(source, InteractionState.Selecting, station);
                Invoke(controller, "UpdateInteractionStation");
                Assert.That(Get(controller, "interactionFocusOffset"), Is.EqualTo(offset));
                Assert.That(Get(controller, "interactionZoomDelta"), Is.EqualTo(zoomDelta));
                Assert.That(owner.transform.position, Is.EqualTo(position));

                station.enabled = false;
                Invoke(controller, "UpdateInteractionStation");
                Assert.That(Get(controller, "interactionCameraActive"), Is.False);
                Assert.That(Get(controller, "targetPlaneNormalOffset"), Is.EqualTo(-12f));
                station.enabled = true;
                Invoke(controller, "UpdateInteractionStation");
                Assert.That(Get(controller, "interactionCameraActive"), Is.True);
                Invoke(controller, "OnDisable");
                Assert.That(Get(controller, "interactionCameraActive"), Is.False);
                Invoke(controller, "OnEnable");
                Assert.That(owner.transform.position, Is.EqualTo(position));
                Assert.That(Get(controller, "initialized"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(stationObject);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(plane);
            }
        }

        [Test]
        public void StationFocusUsesCombinedObjectBoundsOrExplicitMarker()
        {
            var stationObject = new GameObject("Focus Test");
            GameObject first = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject second = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var marker = new GameObject("Focus Marker");
            try
            {
                stationObject.AddComponent<BoxCollider>();
                var station = stationObject.AddComponent<InteractionStation>();
                first.transform.position = new Vector3(-4f, 0f, -3f);
                second.transform.position = new Vector3(6f, 2f, -5f);
                second.transform.localScale = Vector3.one * 3f;
                var firstObject = first.AddComponent<RotatableObject>();
                var secondObject = second.AddComponent<RotatableObject>();
                typeof(InteractionStation).GetField("controlledObjects", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(station, new List<RotatableObject> { firstObject, secondObject });
                Bounds expected = first.GetComponent<Renderer>().bounds;
                expected.Encapsulate(second.GetComponent<Renderer>().bounds);
                Assert.That(station.GetCameraFocusPosition(), Is.EqualTo(expected.center));
                marker.transform.position = new Vector3(10f, 3f, 0f);
                typeof(InteractionStation).GetField("cameraFocusPoint", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(station, marker.transform);
                Assert.That(station.GetCameraFocusPosition(), Is.EqualTo(marker.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(stationObject);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(marker);
            }
        }

        private static void SetInteractionState(PlayerInteractionController controller, InteractionState state,
            InteractionStation station)
        {
            typeof(PlayerInteractionController).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, state);
            typeof(PlayerInteractionController).GetField("currentStation", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, station);
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
