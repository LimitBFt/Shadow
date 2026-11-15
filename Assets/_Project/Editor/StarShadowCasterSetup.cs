using System.Collections.Generic;
using ShadowPath.Shadow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadowPath.Editor.Stars
{
    public static class StarShadowCasterSetup
    {
        private const string MenuPath = "GameObject/ShadowPath/Create Star Shadow Caster";
        private const string MeshAssetPath = "Assets/_Project/Art/StarShadowCaster.asset";
        private const int OutlinePointCount = 10;
        private const float OuterRadius = 1f;
        private const float InnerRadius = 0.45f;
        private const float Thickness = 0.1f;

        [MenuItem(MenuPath, false, 10)]
        private static void CreateStarShadowCaster(MenuCommand command)
        {
            if (!CanCreateStarShadowCaster()) return;

            GameObject selected = command.context as GameObject ?? Selection.activeGameObject;
            if (selected != null && (!selected.scene.IsValid() || !selected.scene.isLoaded))
                selected = null;

            Scene scene = selected != null ? selected.scene : SceneManager.GetActiveScene();
            Transform plane = FindProjectionPlane(scene);
            Vector3 position = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot : (plane != null ? plane.position : Vector3.zero);
            if (selected != null && selected.TryGetComponent<ShadowCaster>(out _))
                position = selected.transform.position;
            Quaternion rotation = plane != null ? plane.rotation : Quaternion.identity;

            Vector3[] frontOutline = CreateFrontOutline();
            Mesh mesh = CreateMesh(frontOutline);
            string path = AssetDatabase.GenerateUniqueAssetPath(MeshAssetPath);
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssetIfDirty(mesh);

            var root = new GameObject("StarShadowCaster", typeof(MeshFilter), typeof(MeshRenderer), typeof(ShadowCaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(position, rotation);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterial = GraphicsSettings.currentRenderPipeline?.defaultMaterial
                ?? AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            for (int i = 0; i < OutlinePointCount; i++)
            {
                var point = new GameObject($"OutlinePoint_{i:D2}").transform;
                point.SetParent(root.transform, false);
                point.localPosition = frontOutline[i];
            }

            Undo.RegisterCreatedObjectUndo(root, "Create Star Shadow Caster");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log($"Created StarShadowCaster with Mesh Asset at {path}.", root);
            if (plane == null)
                Debug.LogWarning("No unique Shadow System Projection Plane found in this scene. StarShadowCaster uses world XY.", root);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanCreateStarShadowCaster() =>
            !EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null;

        private static Transform FindProjectionPlane(Scene scene)
        {
            Transform result = null;
            foreach (ShadowSystem system in Object.FindObjectsByType<ShadowSystem>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (system.gameObject.scene != scene || !system.isActiveAndEnabled) continue;
                var settings = new SerializedObject(system);
                var plane = settings.FindProperty("projectionPlane")?.objectReferenceValue as Transform;
                if (plane == null) continue;
                if (result != null && result != plane) return null;
                result = plane;
            }
            return result;
        }

        private static Vector3[] CreateFrontOutline()
        {
            var outline = new Vector3[OutlinePointCount];
            float angleStep = 2f * Mathf.PI / OutlinePointCount;
            for (int i = 0; i < OutlinePointCount; i++)
            {
                float radius = i % 2 == 0 ? OuterRadius : InnerRadius;
                float angle = i * angleStep;
                outline[i] = new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, Thickness * 0.5f);
            }
            return outline;
        }

        private static Mesh CreateMesh(Vector3[] frontOutline)
        {
            var vertices = new List<Vector3>(6 * OutlinePointCount + 2);
            var triangles = new List<int>(12 * OutlinePointCount);
            Vector3 depth = Vector3.back * Thickness;

            vertices.AddRange(frontOutline);
            foreach (Vector3 point in frontOutline) vertices.Add(point + depth);

            int frontCenter = vertices.Count;
            vertices.Add(Vector3.forward * (Thickness * 0.5f));
            int backCenter = vertices.Count;
            vertices.Add(vertices[frontCenter] + depth);

            for (int i = 0; i < OutlinePointCount; i++)
            {
                int next = (i + 1) % OutlinePointCount;
                int back = i + OutlinePointCount;
                int backNext = next + OutlinePointCount;
                int side = vertices.Count;

                // Separate side vertices keep the cap and wall normals sharp.
                vertices.Add(vertices[i]);
                vertices.Add(vertices[next]);
                vertices.Add(vertices[backNext]);
                vertices.Add(vertices[back]);

                // The outline runs clockwise as seen from the local +Z side.
                triangles.AddRange(new[]
                {
                    frontCenter, next, i,
                    backCenter, back, backNext,
                    side, side + 1, side + 2,
                    side, side + 2, side + 3
                });
            }

            var mesh = new Mesh { name = "StarShadowCaster" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
