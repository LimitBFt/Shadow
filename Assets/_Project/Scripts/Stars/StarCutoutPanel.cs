using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Stars
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class StarCutoutPanel : MonoBehaviour
    {
        private const float Border = 0.05f;
        [SerializeField] private float panelWidth = 3f;
        [SerializeField] private float panelHeight = 3f;
        [SerializeField] private float panelThickness = 0.15f;
        [SerializeField] private float starOuterRadius = 0.85f;
        [SerializeField, Range(0.2f, 0.8f)] private float starInnerRatio = 0.45f;
        [SerializeField] private float starRotationDegrees;
        [SerializeField] private bool previewInEditMode = true;
        [SerializeField, HideInInspector] private Mesh bakedMesh;

        private Mesh previewMesh;
        private bool rebuildRequested;

        private void OnEnable() => rebuildRequested = true;
        // Validation can run during loading. Mesh and hierarchy work is deferred to Update.
        private void OnValidate() => rebuildRequested = true;

        private void Update()
        {
            if (!rebuildRequested) return;
            rebuildRequested = false;
            if (Application.isPlaying || previewInEditMode) RebuildMesh();
            else ReleasePreview();
        }

        public void RebuildMesh()
        {
            panelWidth = SafeClamp(panelWidth, 0.2f, 100f, 3f);
            panelHeight = SafeClamp(panelHeight, 0.2f, 100f, 3f);
            panelThickness = SafeClamp(panelThickness, 0.001f, 10f, 0.15f);
            starOuterRadius = SafeClamp(starOuterRadius, 0.01f,
                Mathf.Min(panelWidth, panelHeight) * 0.5f - Border, 0.85f);
            starInnerRatio = SafeClamp(starInnerRatio, 0.2f, 0.8f, 0.45f);
            starRotationDegrees = SafeClamp(starRotationDegrees, -360000f, 360000f, 0f) % 360f;

            if (previewMesh == null)
                previewMesh = new Mesh { name = "Star Cutout Panel Preview", hideFlags = HideFlags.HideAndDontSave };
            BuildGeometry(previewMesh);
            GetComponent<MeshFilter>().sharedMesh = previewMesh;

            BoxCollider detection = GetComponent<BoxCollider>();
            detection.center = Vector3.zero;
            detection.size = new Vector3(panelWidth, panelHeight, panelThickness);
            detection.isTrigger = false;
            // OverlapSphere queries still find this collider. Contact pairs are excluded.
            detection.includeLayers = 0;
            detection.excludeLayers = ~0;
            detection.layerOverridePriority = 100;

            Transform center = transform.Find("HoleCenter");
            if (center == null)
            {
                center = new GameObject("HoleCenter").transform;
                center.SetParent(transform, false);
            }
            center.localPosition = Vector3.zero;
            rebuildRequested = false;
        }

        private static float SafeClamp(float value, float minimum, float maximum, float fallback) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, minimum, maximum);

        private void BuildGeometry(Mesh mesh)
        {
            const double fullTurn = Math.PI * 2d;
            var star = new Vector2[10];
            var angles = new List<double>(14);
            for (int i = 0; i < star.Length; i++)
            {
                double angle = (Math.PI * 0.5d + starRotationDegrees * Math.PI / 180d + i * fullTurn / 10d) % fullTurn;
                if (angle < 0d) angle += fullTurn;
                float radius = i % 2 == 0 ? starOuterRadius : starOuterRadius * starInnerRatio;
                star[i] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                angles.Add(angle);
            }

            float halfWidth = panelWidth * 0.5f;
            float halfHeight = panelHeight * 0.5f;
            double cornerAngle = Math.Atan2(halfHeight, halfWidth);
            angles.Add(cornerAngle);
            angles.Add(Math.PI - cornerAngle);
            angles.Add(Math.PI + cornerAngle);
            angles.Add(fullTurn - cornerAngle);
            angles.Sort();
            // Coincident rays share one strip boundary, avoiding zero-area triangles.
            for (int i = angles.Count - 1; i > 0; i--)
                if (angles[i] - angles[i - 1] < 0.00001d) angles.RemoveAt(i);
            if (angles[0] + fullTurn - angles[angles.Count - 1] < 0.00001d)
                angles.RemoveAt(angles.Count - 1);

            var inner = new Vector3[angles.Count];
            var outer = new Vector3[angles.Count];
            for (int i = 0; i < angles.Count; i++)
            {
                var direction = new Vector2((float)Math.Cos(angles[i]), (float)Math.Sin(angles[i]));
                float distance = float.PositiveInfinity;
                for (int edge = 0; edge < star.Length; edge++)
                {
                    Vector2 a = star[edge];
                    Vector2 segment = star[(edge + 1) % star.Length] - a;
                    double denominator = Cross(direction, segment);
                    if (Math.Abs(denominator) < 1e-12d) continue;
                    double alongRay = Cross(a, segment) / denominator;
                    double alongEdge = Cross(a, direction) / denominator;
                    if (alongRay > 0d && alongEdge >= -0.00001d && alongEdge <= 1.00001d)
                        distance = Mathf.Min(distance, (float)alongRay);
                }
                inner[i] = direction * distance;
                float rectangleDistance = Mathf.Min(halfWidth / Mathf.Abs(direction.x), halfHeight / Mathf.Abs(direction.y));
                outer[i] = direction * rectangleDistance;
            }

            var vertices = new List<Vector3>(angles.Count * 16);
            var triangles = new List<int>(angles.Count * 24);
            Vector3 front = Vector3.forward * (panelThickness * 0.5f);
            for (int i = 0; i < angles.Count; i++)
            {
                int next = (i + 1) % angles.Count;
                AddQuad(vertices, triangles, outer[i] + front, outer[next] + front, inner[next] + front, inner[i] + front);
                AddQuad(vertices, triangles, outer[next] - front, outer[i] - front, inner[i] - front, inner[next] - front);
                AddQuad(vertices, triangles, outer[i] - front, outer[next] - front, outer[next] + front, outer[i] + front);
                AddQuad(vertices, triangles, inner[next] - front, inner[i] - front, inner[i] + front, inner[next] + front);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static double Cross(Vector2 a, Vector2 b) => (double)a.x * b.y - (double)a.y * b.x;

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void OnDisable() => ReleasePreview();
        private void OnDestroy() => ReleasePreview();

        private void ReleasePreview()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == previewMesh) filter.sharedMesh = bakedMesh;
            if (previewMesh == null) return;
            if (Application.isPlaying) Destroy(previewMesh);
            else DestroyImmediate(previewMesh);
            previewMesh = null;
        }
    }
}
