using System;
using System.IO;
using ShadowPath.Lighting;
using ShadowPath.Shadow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShadowPath.Editor.Lighting
{
    [InitializeOnLoad]
    public static class NaturalLightingSetupUtility
    {
        private const string ScenePath = "Assets/_Project/Scenes/ShadowPrototype.unity";
        private const string PendingKey = "ShadowPath.NaturalLighting.Validate";
        private const string OutputKey = "ShadowPath.NaturalLighting.Output";
        private static double nextCaptureTime;
        private static int stage;
        private static Vector3 originalLightPosition;
        private static Light sourceLight;

        static NaturalLightingSetupUtility()
        {
            if (SessionState.GetBool(PendingKey, false))
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/ShadowPath/Lighting/Apply Natural Lighting Preview")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying lighting.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the current scene first.");
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            LightGameplayArea area = FindInScene<LightGameplayArea>(scene);
            Light light = FindInScene<LightSourceController>(scene)?.GetComponentInChildren<Light>();
            if (area == null || light == null || light.type != LightType.Point)
                throw new InvalidOperationException("Expected the existing gameplay Point Light and LightGameplayArea.");

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "GameplayMaskCanvas") continue;
                Undo.RecordObject(root, "Temporarily disable gameplay UI mask");
                root.SetActive(false); // Preserve every Mask component, material and reference for comparison.
                EditorUtility.SetDirty(root);
            }

            Undo.RecordObject(light, "Use natural realtime point lighting");
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.intensity = 220f;
            light.color = new Color(1f, 0.93f, 0.84f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.15f;
            UniversalAdditionalLightData data = light.GetComponent<UniversalAdditionalLightData>();
            if (data != null)
            {
                Undo.RecordObject(data, "Use local shadow bias and high soft shadow filtering");
                data.usePipelineSettings = false;
                data.softShadowQuality = SoftShadowQuality.High;
                EditorUtility.SetDirty(data);
            }
            EditorUtility.SetDirty(light);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.18f, 0.21f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.13f, 0.17f);
            RenderSettings.ambientGroundColor = new Color(0.065f, 0.075f, 0.10f);
            RenderSettings.ambientIntensity = 1f;

            // Six 1024-pixel Point Light shadow faces cannot fit a 2048 atlas without downscaling.
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("The active pipeline must be URP.");
            Undo.RecordObject(pipeline, "Fit point light shadow faces in the PC shadow atlas");
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 4096;
            pipelineSettings.ApplyModifiedProperties();
            EditorUtility.SetDirty(pipeline);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save scene.");
            Debug.Log($"Natural lighting saved: Mask inactive; Point Light intensity {light.intensity}, " +
                $"range unchanged ({light.range}), soft shadows with strength {light.shadowStrength}; " +
                "trilight ambient fill; active URP additional shadow atlas 4096. Gameplay and camera unchanged.", light);
        }

        // For a normal graphical Editor only; also available from the menu below.
        [MenuItem("Tools/ShadowPath/Lighting/Validate Natural Lighting In Play Mode")]
        public static void ApplyAndValidate()
        {
            Apply();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../NaturalLightingValidation"));
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-lightingValidationDir") output = args[i + 1];
            Directory.CreateDirectory(output);
            SessionState.SetString(OutputKey, output);
            File.WriteAllText(Path.Combine(output, "validation.txt"),
                "Saved lighting preview. Validation temporarily moves the Light and disables direct lighting; no Play state is saved.\n");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                sourceLight = FindInScene<LightSourceController>(SceneManager.GetActiveScene()).GetComponentInChildren<Light>();
                originalLightPosition = sourceLight.transform.parent.position;
                stage = 0;
                nextCaptureTime = EditorApplication.timeSinceStartup + 3.0;
                EditorApplication.update -= ValidateFrame;
                EditorApplication.update += ValidateFrame;
                Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameView != null) EditorWindow.GetWindow(gameView).Show();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= ValidateFrame;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                SessionState.SetBool(PendingKey, false);
            }
        }

        private static void ValidateFrame()
        {
            if (!EditorApplication.isPlaying) return;
            if (stage == 1 || stage == 3)
            {
                Vector3 target = stage == 1 ? originalLightPosition + Vector3.right : originalLightPosition;
                sourceLight.transform.parent.position = Vector3.MoveTowards(
                    sourceLight.transform.parent.position, target, 0.01f);
                if (Vector3.Distance(sourceLight.transform.parent.position, target) > 0.0001f) return;
            }
            if (EditorApplication.timeSinceStartup < nextCaptureTime) return;
            string output = SessionState.GetString(OutputKey, "");
            switch (stage)
            {
                case 0:
                    Capture(output, "01-natural-light");
                    break;
                case 1:
                    // Only transient Play state; preserve the saved light position and all controller code.
                    sourceLight.transform.parent.position = originalLightPosition + Vector3.right;
                    break;
                case 2:
                    Capture(output, "02-light-moved");
                    break;
                case 3:
                    sourceLight.transform.parent.position = originalLightPosition;
                    sourceLight.enabled = false;
                    break;
                case 4:
                    Capture(output, "03-ambient-only");
                    break;
                case 5:
                    if (!File.Exists(Path.Combine(output, "03-ambient-only.png"))) return;
                    sourceLight.enabled = true;
                    sourceLight.transform.parent.position = originalLightPosition;
                    File.AppendAllText(Path.Combine(output, "validation.txt"), "Restored transient Light state. Leaving Play Mode without saving runtime state.\n");
                    EditorApplication.update -= ValidateFrame;
                    EditorApplication.isPlaying = false;
                    return;
            }
            stage++;
            nextCaptureTime = EditorApplication.timeSinceStartup + 1.5;
        }

        private static void Capture(string output, string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            LightGameplayArea area = FindInScene<LightGameplayArea>(scene);
            int validShadows = 0;
            int geometryVersionSum = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (GeneratedShadow shadow in root.GetComponentsInChildren<GeneratedShadow>(true))
            {
                if (shadow.HasValidGeometry) validShadows++;
                geometryVersionSum += shadow.GeometryVersion;
            }
            string report = $"{name}: Light world={sourceLight.transform.position:F4}, enabled={sourceLight.enabled}, " +
                $"soft shadows={sourceLight.shadows}, range={sourceLight.range}, valid generated shadow roads={validShadows}, " +
                $"geometry version sum={geometryVersionSum}, camera={area.GameplayCamera.transform.position:F4}\n";
            File.AppendAllText(Path.Combine(output, "validation.txt"), report);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
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
