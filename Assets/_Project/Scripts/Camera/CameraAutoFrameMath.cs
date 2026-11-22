using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Camera
{
    public static class CameraAutoFrameMath
    {
        private const int SearchIterations = 16;
        private const float Epsilon = 0.00001f;

        public struct View
        {
            public Quaternion Rotation;
            public Matrix4x4 Projection;
            public float NearClip;
            public float FarClip;
            public Vector3 PlaneNormal;
            public Vector3 LensOffset;
            public Vector3 CompositionOffset;
        }

        public struct Frame
        {
            public Vector3 Focus;
            public float NormalOffset;
            public bool FitsAll;
        }

        public static bool TryFrame(IReadOnlyList<Vector3> corners, View view, Vector2 offsetRange,
            float padding, out Frame frame)
        {
            frame = default;
            if (corners == null || corners.Count == 0 || !IsFinite(offsetRange.x) || !IsFinite(offsetRange.y)
                || !IsFinite(padding) || offsetRange.x * offsetRange.y <= 0f
                || !IsFinite(view.PlaneNormal) || view.PlaneNormal.sqrMagnitude < Epsilon
                || !IsFinite(view.LensOffset) || !IsFinite(view.CompositionOffset)
                || !IsFinite(view.NearClip) || !IsFinite(view.FarClip)
                || view.NearClip <= 0f || view.FarClip <= view.NearClip
                || !IsFinite(view.Rotation.x) || !IsFinite(view.Rotation.y)
                || !IsFinite(view.Rotation.z) || !IsFinite(view.Rotation.w)) return false;
            float rotationLength = Quaternion.Dot(view.Rotation, view.Rotation);
            if (!IsFinite(rotationLength) || rotationLength < Epsilon) return false;
            view.Rotation = view.Rotation.normalized;
            for (int i = 0; i < 16; i++)
                if (!IsFinite(view.Projection[i])) return false;
            // Unity's perspective projection consumes camera-local negative Z in its view matrix.
            Matrix4x4 projection = view.Projection;
            if (Mathf.Abs(projection.m32 + 1f) > Epsilon || Mathf.Abs(projection.m33) > Epsilon
                || Mathf.Abs(projection.m30) > Epsilon || Mathf.Abs(projection.m31) > Epsilon) return false;
            float determinant = projection.m00 * projection.m11 - projection.m01 * projection.m10;
            if (!IsFinite(determinant) || Mathf.Abs(determinant) < Epsilon) return false;
            Vector3 centerRay = new Vector3(
                (projection.m11 * projection.m02 - projection.m01 * projection.m12) / determinant,
                (projection.m00 * projection.m12 - projection.m10 * projection.m02) / determinant, 1f);
            Vector3 normal = view.PlaneNormal.normalized;
            Vector3 ray = view.Rotation * centerRay;
            float normalPerDepth = Vector3.Dot(ray, normal);
            if (!IsFinite(ray) || Mathf.Abs(normalPerDepth) < Epsilon) return false;
            float lensNormal = Vector3.Dot(view.LensOffset, normal);
            float nearOffset = Mathf.Abs(offsetRange.x) <= Mathf.Abs(offsetRange.y) ? offsetRange.x : offsetRange.y;
            float farOffset = Mathf.Abs(offsetRange.x) >= Mathf.Abs(offsetRange.y) ? offsetRange.x : offsetRange.y;
            float nearDepth = -(nearOffset + lensNormal) / normalPerDepth;
            float farDepth = -(farOffset + lensNormal) / normalPerDepth;
            if (!IsFinite(nearDepth) || !IsFinite(farDepth) || farDepth < nearDepth || farDepth <= 0f) return false;

            Quaternion inverse = Quaternion.Inverse(view.Rotation);
            Vector3 origin = corners[0];
            if (!IsFinite(origin)) return false;
            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 corner = corners[i];
                if (!IsFinite(corner)) return false;
                Vector3 point = inverse * (corner - origin);
                if (!IsFinite(point)) return false;
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
            Vector3 center = origin + view.Rotation * ((minimum + maximum) * 0.5f);
            float depthExtent = (maximum.z - minimum.z) * 0.5f;
            if (!IsFinite(center) || !IsFinite(depthExtent)) return false;
            frame = BuildFrame(center, ray, farDepth, farOffset, normal, view, false);
            if (!IsFinite(frame.Focus)) return false;
            float lower = Mathf.Max(nearDepth, view.NearClip + depthExtent + Epsilon);
            float upper = Mathf.Min(farDepth, view.FarClip - depthExtent - Epsilon);
            if (lower > upper || !Fits(corners, view, center - ray * upper, padding)) return true;
            if (!Fits(corners, view, center - ray * lower, padding))
            {
                for (int iteration = 0; iteration < SearchIterations; iteration++)
                {
                    float middle = (lower + upper) * 0.5f;
                    if (Fits(corners, view, center - ray * middle, padding)) upper = middle;
                    else lower = middle;
                }
                lower = upper;
            }
            float offset = Mathf.Clamp(-normalPerDepth * lower - lensNormal,
                Mathf.Min(offsetRange.x, offsetRange.y), Mathf.Max(offsetRange.x, offsetRange.y));
            frame = BuildFrame(center, ray, lower, offset, normal, view, true);
            return IsFinite(frame.Focus);
        }

        private static Frame BuildFrame(Vector3 center, Vector3 ray, float depth, float offset,
            Vector3 normal, View view, bool fits)
        {
            Vector3 rigPosition = center - ray * depth - view.LensOffset;
            return new Frame
            {
                Focus = rigPosition - view.CompositionOffset - normal * offset,
                NormalOffset = offset,
                FitsAll = fits
            };
        }

        private static bool Fits(IReadOnlyList<Vector3> corners, View view, Vector3 lensPosition, float padding)
        {
            Quaternion inverse = Quaternion.Inverse(view.Rotation);
            float limit = 1f - 2f * Mathf.Clamp(padding, 0f, 0.45f);
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 point = inverse * (corners[i] - lensPosition);
                if (!IsFinite(point)) return false;
                if (point.z < view.NearClip || point.z > view.FarClip) return false;
                Vector4 clip = view.Projection * new Vector4(point.x, point.y, -point.z, 1f);
                if (!IsFinite(clip.x) || !IsFinite(clip.y) || !IsFinite(clip.z) || !IsFinite(clip.w)
                    || clip.w <= Epsilon || Mathf.Abs(clip.x / clip.w) > limit + Epsilon
                    || Mathf.Abs(clip.y / clip.w) > limit + Epsilon) return false;
            }
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
