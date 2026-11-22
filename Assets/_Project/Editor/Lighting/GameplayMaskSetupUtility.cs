using System;
using System.IO;
using ShadowPath.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowPath.Editor.Lighting
{
    [InitializeOnLoad]
    public static class GameplayMaskSetupUtility
    {
        private const string ScenePath = "Assets/_Project/Scenes/ShadowPrototype.unity";
        private const string MaterialPath = "Assets/_Project/Materials/MAT_LightGameplayMask.mat";
        private const string ValidationKey = "ShadowPath.GameplayMask.Validation";
        private const string OutputKey = "ShadowPath.GameplayMask.Output";
        private static double captureTime;
        private static bool captureRequested;

        static GameplayMaskSetupUtility()
        {
            if (SessionState.GetBool(ValidationKey, false))
            {
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }
        }

        [MenuItem("Tools/ShadowPath/Lighting/Setup Visual Gameplay Mask")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before configuring the mask.");

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty)
                    throw new InvalidOperationException("Save the current scene before opening ShadowPrototype.");
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            LightGameplayArea area = FindInScene<LightGameplayArea>(scene);
            if (area == null || area.GameplayCamera == null)
                throw new InvalidOperationException("The scene needs a LightGameplayArea with Gameplay Camera assigned.");

            Shader shader = Shader.Find("ShadowPath/UI/Light Gameplay Mask");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Gameplay mask shader is missing or has compile errors.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MAT_LightGameplayMask" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            Undo.RecordObject(material, "Configure gameplay mask material");
            material.shader = shader;
            material.SetColor("_Color", Color.black);
            material.SetFloat("_OutsideDarkness", 1f);
            material.SetFloat("_Feather", 0.05f / 2.7f);
            EditorUtility.SetDirty(material);

            GameObject canvasObject = null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.name == "GameplayMaskCanvas") canvasObject = candidate;
            if (canvasObject == null)
            {
                canvasObject = new GameObject("GameplayMaskCanvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create gameplay mask canvas");
            }
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null) canvas = Undo.AddComponent<Canvas>(canvasObject);
            Undo.RecordObject(canvas, "Configure gameplay mask canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingLayerID = 0;
            canvas.sortingOrder = 10;
            canvas.targetDisplay = area.GameplayCamera.targetDisplay;
            canvas.enabled = true;
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.SetActive(true);
            canvasObject.transform.localScale = Vector3.one;

            Transform child = canvasObject.transform.Find("DarknessMask");
            if (child == null)
            {
                GameObject mask = new GameObject("DarknessMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(mask, "Create darkness mask");
                mask.transform.SetParent(canvasObject.transform, false);
                child = mask.transform;
            }
            child.gameObject.SetActive(true);
            child.gameObject.layer = canvasObject.layer;
            RectTransform rect = (RectTransform)child;
            Undo.RecordObject(rect, "Stretch darkness mask");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            Image image = child.GetComponent<Image>();
            if (image == null) image = Undo.AddComponent<Image>(child.gameObject);
            Undo.RecordObject(image, "Assign darkness mask material");
            image.color = Color.white;
            image.raycastTarget = false;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.material = material;
            image.enabled = true;

            LightGameplayMaskController controller = canvasObject.GetComponent<LightGameplayMaskController>();
            if (controller == null) controller = Undo.AddComponent<LightGameplayMaskController>(canvasObject);
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("gameplayArea").objectReferenceValue = area;
            serialized.FindProperty("gameplayCamera").objectReferenceValue = area.GameplayCamera;
            serialized.FindProperty("darknessMask").objectReferenceValue = image;
            serialized.FindProperty("maskMaterialTemplate").objectReferenceValue = material;
            serialized.ApplyModifiedProperties();
            controller.enabled = true;

            // Existing overlay HUD must render after the world-darkening layer.
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Canvas hud in root.GetComponentsInChildren<Canvas>(true))
            {
                if (hud == canvas || !hud.isRootCanvas || hud.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                if (hud.sortingOrder <= canvas.sortingOrder)
                {
                    Undo.RecordObject(hud, "Keep HUD above gameplay mask");
                    hud.sortingOrder = 20;
                    EditorUtility.SetDirty(hud);
                }
            }

            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the gameplay mask scene.");
            Selection.activeGameObject = canvasObject;
            Debug.Log("Visual Gameplay Mask saved: Overlay canvas order 10, HUD order >= 20; full-stretch white Image, " +
                "MAT_LightGameplayMask and all controller references assigned. Visual parameters unchanged.", canvasObject);
        }

        // Command-line entry point for a normal, graphical Unity Editor (no batchmode).
        public static void SetupAndValidate()
        {
            Setup();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-maskValidationDir") SessionState.SetString(OutputKey, args[i + 1]);
            string output = SessionState.GetString(OutputKey, "");
            if (string.IsNullOrEmpty(output))
                throw new InvalidOperationException("Provide -maskValidationDir for the validation report and Game View capture.");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "validation.txt"), "Scene setup saved. Entering normal Editor Play Mode.\n");
            SessionState.SetBool(ValidationKey, true);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                captureTime = EditorApplication.timeSinceStartup + 4.0;
                captureRequested = false;
                EditorApplication.update -= CaptureValidation;
                EditorApplication.update += CaptureValidation;
                Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameView != null) EditorWindow.GetWindow(gameView).Show();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= CaptureValidation;
                SessionState.SetBool(ValidationKey, false);
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            }
        }

        private static void CaptureValidation()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < captureTime) return;
            string output = SessionState.GetString(OutputKey, "");
            string screenshot = Path.Combine(output, "game-view.png");
            if (!captureRequested)
            {
                LightGameplayMaskController controller = FindInScene<LightGameplayMaskController>(SceneManager.GetActiveScene());
                var serialized = new SerializedObject(controller);
                string report = $"Viewport Center: {controller.ViewportCenter}\nViewport Radius: {controller.ViewportRadius}\n";
                foreach (string field in new[] { "maskMaterialAssigned", "cameraAssigned", "gameplayAreaAssigned" })
                    report += $"{field}: {serialized.FindProperty(field).boolValue}\n";
                report += $"Shader compile errors: {ShaderUtil.ShaderHasError(Shader.Find("ShadowPath/UI/Light Gameplay Mask"))}\n";
                File.AppendAllText(Path.Combine(output, "validation.txt"), report);
                ScreenCapture.CaptureScreenshot(screenshot);
                captureRequested = true;
                captureTime = EditorApplication.timeSinceStartup + 2.0;
                return;
            }
            if (!File.Exists(screenshot)) return;
            File.AppendAllText(Path.Combine(output, "validation.txt"), "Game View screenshot captured. Leaving Play Mode.\n");
            EditorApplication.update -= CaptureValidation;
            EditorApplication.isPlaying = false;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }
    }
}
