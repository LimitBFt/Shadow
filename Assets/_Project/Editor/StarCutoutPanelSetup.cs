using ShadowPath.Shadow;
using ShadowPath.Stars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadowPath.Editor.Stars
{
    public static class StarCutoutPanelSetup
    {
        [MenuItem("GameObject/ShadowPath/Create Star Cutout Panel", false, 10)]
        private static void CreatePanel(MenuCommand command)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            GameObject selected = command.context as GameObject ?? Selection.activeGameObject;
            Scene scene = selected != null && selected.scene.IsValid() ? selected.scene : SceneManager.GetActiveScene();
            if (!TryFindEnvironment(scene, out Transform plane, out Light light))
            {
                Debug.LogWarning("Select a ShadowSystem or use a scene with one active ShadowSystem, assigned Projection Plane and Point/Spot Source Light.");
                return;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("URP Lit shader is required to create this panel.");
                return;
            }
            var wall = new Plane(plane.forward, plane.position);
            if (!wall.Raycast(new Ray(light.transform.position, light.transform.forward), out float distance) || distance <= 0.3f)
            {
                Debug.LogWarning("Aim Source Light forward at the Projection Plane before creating the cutout panel.");
                return;
            }

            string materialPath = AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/Materials/StarCutoutPanel.mat");
            var material = new Material(shader) { name = "Star Cutout Panel" };
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_ZWrite", 1f);
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssetIfDirty(material);

            var root = new GameObject("StarCutoutPanel", typeof(MeshFilter), typeof(MeshRenderer),
                typeof(ShadowCaster), typeof(BoxCollider));
            SceneManager.MoveGameObjectToScene(root, scene);
            int detectionLayer = LayerMask.NameToLayer("Occluder");
            if (detectionLayer >= 0) root.layer = detectionLayer;
            root.transform.SetPositionAndRotation(light.transform.position + light.transform.forward * (distance * 0.5f), plane.rotation);
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            root.AddComponent<StarCutoutPanel>().RebuildMesh();
            Undo.RegisterCreatedObjectUndo(root, "Create Star Cutout Panel");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log($"Created StarCutoutPanel. Material saved to {materialPath}. Include its layer in Shadow Caster Mask and the light's Culling Mask.", root);
        }

        private static bool TryFindEnvironment(Scene scene, out Transform plane, out Light light)
        {
            plane = null;
            light = null;
            ShadowSystem selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<ShadowSystem>() : null;
            ShadowSystem candidate = null;
            foreach (ShadowSystem system in Object.FindObjectsByType<ShadowSystem>(FindObjectsSortMode.None))
            {
                if (system.gameObject.scene != scene || !system.isActiveAndEnabled) continue;
                if (selected != null && system != selected) continue;
                if (candidate != null) return false;
                candidate = system;
            }
            if (candidate == null) return false;
            var settings = new SerializedObject(candidate);
            plane = settings.FindProperty("projectionPlane").objectReferenceValue as Transform;
            light = settings.FindProperty("sourceLight").objectReferenceValue as Light;
            return plane != null && light != null && (light.type == LightType.Spot || light.type == LightType.Point);
        }
    }
}
