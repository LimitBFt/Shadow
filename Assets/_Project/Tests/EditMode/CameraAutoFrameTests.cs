#if SHADOWPATH_ENABLE_EDITMODE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShadowPath.Camera;
using ShadowPath.Interaction;
using UnityEngine;

namespace ShadowPath.Tests.EditMode
{
    public class CameraAutoFrameTests
    {
        [TestCase(1.7777778f, false)]
        [TestCase(0.75f, true)]
        public void TiltedPerspectiveFramesAllBoundsCornersWithPadding(float aspect, bool lensShift)
        {
            var corners = new List<Vector3>();
            AddCorners(new Bounds(new Vector3(-8f, 4f, -1f), new Vector3(3f, 4f, 7f)), corners);
            AddCorners(new Bounds(new Vector3(7f, -3f, 2f), new Vector3(2f, 9f, 3f)), corners);
            Quaternion plane = Quaternion.Euler(10f, 25f, 12f);
            CameraAutoFrameMath.View view = View(aspect);
            view.Rotation = plane * Quaternion.Euler(25f, 8f, 0f);
            view.PlaneNormal = plane * Vector3.forward;
            view.LensOffset = view.Rotation * new Vector3(0.35f, -0.2f, 0.8f);
            view.CompositionOffset = plane * new Vector3(1.5f, 4f, 0f);
            if (lensShift)
            {
                view.Projection.m02 = 0.2f;
                view.Projection.m12 = -0.1f;
            }
            Assert.That(CameraAutoFrameMath.TryFrame(corners, view, new Vector2(-80f, -5f), 0.15f,
                out CameraAutoFrameMath.Frame frame), Is.True);
            Assert.That(frame.FitsAll, Is.True);
            Assert.That(frame.NormalOffset, Is.InRange(-80f, -5f));
            AssertViewportContains(corners, view, frame, 0.15f);
        }

        [Test]
        public void VerticalExtentAndPortraitAspectRequireMoreDistance()
        {
            var wide = new List<Vector3>();
            AddCorners(new Bounds(Vector3.zero, new Vector3(16f, 2f, 2f)), wide);
            CameraAutoFrameMath.View landscape = View(2f);
            CameraAutoFrameMath.View portrait = View(0.75f);
            Vector2 range = new Vector2(-100f, -2f);
            Assert.That(CameraAutoFrameMath.TryFrame(wide, landscape, range, 0.15f, out var broadFrame), Is.True);
            Assert.That(CameraAutoFrameMath.TryFrame(wide, portrait, range, 0.15f, out var narrowFrame), Is.True);
            Assert.That(Mathf.Abs(narrowFrame.NormalOffset), Is.GreaterThan(Mathf.Abs(broadFrame.NormalOffset)));

            var tall = new List<Vector3>();
            AddCorners(new Bounds(Vector3.zero, new Vector3(2f, 30f, 2f)), tall);
            Assert.That(CameraAutoFrameMath.TryFrame(tall, landscape, range, 0.15f, out var tallFrame), Is.True);
            Assert.That(tallFrame.FitsAll, Is.True);
            Assert.That(Mathf.Abs(tallFrame.NormalOffset), Is.GreaterThan(Mathf.Abs(broadFrame.NormalOffset)));
            AssertViewportContains(tall, landscape, tallFrame, 0.15f);
        }

        [Test]
        public void OversizedObjectsUseFarthestAllowedOffset()
        {
            var corners = new List<Vector3>();
            AddCorners(new Bounds(Vector3.zero, new Vector3(1000f, 2f, 2f)), corners);
            Assert.That(CameraAutoFrameMath.TryFrame(corners, View(1.7777778f), new Vector2(-20f, -10f),
                0.15f, out var frame), Is.True);
            Assert.That(frame.FitsAll, Is.False);
            Assert.That(frame.NormalOffset, Is.EqualTo(-20f));
        }

        [Test]
        public void EmptyObjectsAndInvalidRangeFailForFixedCameraFallback()
        {
            var corners = new List<Vector3>();
            Assert.That(CameraAutoFrameMath.TryFrame(corners, View(1f), new Vector2(-20f, -10f),
                0.15f, out _), Is.False);
            AddCorners(new Bounds(Vector3.zero, Vector3.one), corners);
            Assert.That(CameraAutoFrameMath.TryFrame(corners, View(1f), new Vector2(-20f, 10f),
                0.15f, out _), Is.False);
        }

        [Test]
        public void StationCollectsOnlyControlledBoundsAndUsesColliderFallback()
        {
            var root = new GameObject("Frame Point Test");
            try
            {
                root.AddComponent<BoxCollider>();
                var station = root.AddComponent<InteractionStation>();
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform);
                var first = visual.AddComponent<RotatableObject>();
                GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.transform.SetParent(visual.transform);
                child.transform.position = new Vector3(3f, 2f, 0f);
                var physical = new GameObject("Collider Only");
                physical.transform.SetParent(root.transform);
                var second = physical.AddComponent<RotatableObject>();
                physical.AddComponent<BoxCollider>().size = Vector3.one * 2f;
                GameObject unrelated = GameObject.CreatePrimitive(PrimitiveType.Cube);
                unrelated.transform.SetParent(root.transform);
                unrelated.transform.position = Vector3.one * 1000f;
                typeof(InteractionStation).GetField("controlledObjects", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(station, new List<RotatableObject> { first, second });
                var corners = new List<Vector3>();
                Physics.SyncTransforms();
                station.CollectCameraFramePoints(corners);
                Assert.That(station.AutoFrameControlledObjects, Is.False);
                Assert.That(corners.Count, Is.EqualTo(16));
                foreach (Vector3 corner in corners)
                    Assert.That(corner.magnitude, Is.LessThan(10f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CameraAutoFrameMath.View View(float aspect) => new CameraAutoFrameMath.View
        {
            Rotation = Quaternion.identity,
            Projection = Matrix4x4.Perspective(52f, aspect, 0.3f, 500f),
            NearClip = 0.3f,
            FarClip = 500f,
            PlaneNormal = Vector3.forward,
            CompositionOffset = Vector3.up * 4f
        };

        private static void AssertViewportContains(List<Vector3> corners, CameraAutoFrameMath.View view,
            CameraAutoFrameMath.Frame frame, float padding)
        {
            var owner = new GameObject("Projection Verification Camera");
            try
            {
                var camera = owner.AddComponent<UnityEngine.Camera>();
                camera.enabled = false;
                camera.nearClipPlane = view.NearClip;
                camera.farClipPlane = view.FarClip;
                camera.projectionMatrix = view.Projection;
                owner.transform.SetPositionAndRotation(frame.Focus + view.CompositionOffset
                    + view.PlaneNormal.normalized * frame.NormalOffset + view.LensOffset, view.Rotation);
                foreach (Vector3 corner in corners)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(corner);
                    Assert.That(viewport.x, Is.InRange(padding - 0.0001f, 1f - padding + 0.0001f));
                    Assert.That(viewport.y, Is.InRange(padding - 0.0001f, 1f - padding + 0.0001f));
                    Assert.That(viewport.z, Is.InRange(view.NearClip, view.FarClip));
                }
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void AddCorners(Bounds bounds, List<Vector3> points)
        {
            for (int i = 0; i < 8; i++)
                points.Add(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
        }
    }
}
#endif
