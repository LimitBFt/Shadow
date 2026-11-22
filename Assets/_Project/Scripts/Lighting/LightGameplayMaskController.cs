using UnityEngine;
using UnityEngine.UI;

namespace ShadowPath.Lighting
{
    [DefaultExecutionOrder(100)]
    public class LightGameplayMaskController : MonoBehaviour
    {
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int OutsideDarknessId = Shader.PropertyToID("_OutsideDarkness");

        [Header("References")]
        [SerializeField] private LightGameplayArea gameplayArea;
        [SerializeField] private UnityEngine.Camera gameplayCamera;
        [SerializeField] private Image darknessMask;
        [SerializeField] private Material maskMaterialTemplate;

        [Header("Runtime Debug")]
        [SerializeField] private Vector2 currentViewportCenter;
        [SerializeField] private Vector2 currentViewportRadius;
        [SerializeField] private bool maskMaterialAssigned;
        [SerializeField] private bool cameraAssigned;
        [SerializeField] private bool gameplayAreaAssigned;

        public Vector2 ViewportCenter => currentViewportCenter;
        public Vector2 ViewportRadius => currentViewportRadius;

        private Material runtimeMaterial;
        private bool warnedBehindCamera;

        private void Awake()
        {
            RefreshReferenceDebug();
            if (!maskMaterialAssigned || !cameraAssigned || !gameplayAreaAssigned)
            {
                Debug.LogError(
                    $"{nameof(LightGameplayMaskController)} requires a Gameplay Area, Gameplay Camera, " +
                    "Darkness Mask Image and Mask Material Template using ShadowPath/UI/Light Gameplay Mask.",
                    this);
                enabled = false;
                return;
            }

            runtimeMaterial = new Material(maskMaterialTemplate)
            {
                name = $"{maskMaterialTemplate.name} (Runtime)"
            };
            darknessMask.material = runtimeMaterial;
            darknessMask.color = Color.white;
            darknessMask.raycastTarget = false;
        }

        private void Start()
        {
            UpdateMask();
        }

        private void LateUpdate()
        {
            UpdateMask();
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void UpdateMask()
        {
            RefreshReferenceDebug();
            if (runtimeMaterial == null
                || gameplayArea == null
                || !gameplayArea.TryGetGameplayCenterWorldPosition(out Vector3 centerWorldPosition))
            {
                return;
            }

            if (gameplayCamera == null)
            {
                return;
            }

            Vector2 visualRadius = gameplayArea.VisualRadius;
            Vector3 centerViewport = gameplayCamera.WorldToViewportPoint(centerWorldPosition);
            if (centerViewport.z <= 0f)
            {
                if (!warnedBehindCamera)
                {
                    Debug.LogWarning("Gameplay Mask center is behind the Gameplay Camera " +
                        "(WorldToViewportPoint Z <= 0). The mask stays dark until the center is in front.", this);
                    warnedBehindCamera = true;
                }

                runtimeMaterial.SetVector(CenterId, new Vector4(-10f, -10f, 0f, 0f));
                runtimeMaterial.SetVector(RadiusId, new Vector4(0.0001f, 0.0001f, 0f, 0f));
                runtimeMaterial.SetFloat(OutsideDarknessId, gameplayArea.OutsideDarkness);
                return;
            }

            warnedBehindCamera = false;
            Vector3 rightViewport = gameplayCamera.WorldToViewportPoint(
                centerWorldPosition + gameplayArea.ProjectionPlaneRight * visualRadius.x);
            Vector3 upViewport = gameplayCamera.WorldToViewportPoint(
                centerWorldPosition + gameplayArea.ProjectionPlaneUp * visualRadius.y);

            currentViewportCenter = new Vector2(centerViewport.x, centerViewport.y);
            currentViewportRadius = new Vector2(
                Mathf.Max(0.0001f, Mathf.Abs(rightViewport.x - centerViewport.x)),
                Mathf.Max(0.0001f, Mathf.Abs(upViewport.y - centerViewport.y)));

            float minimumWorldRadius = Mathf.Min(visualRadius.x, visualRadius.y);
            float normalizedFeather = Mathf.Clamp01(
                gameplayArea.VisualFeather / Mathf.Max(0.0001f, minimumWorldRadius));
            // The Image fills the screen, including when the camera uses a partial viewport.
            Rect cameraRect = gameplayCamera.rect;
            Vector2 screenCenter = cameraRect.position + Vector2.Scale(currentViewportCenter, cameraRect.size);
            Vector2 screenRadius = Vector2.Scale(currentViewportRadius, cameraRect.size);
            runtimeMaterial.SetVector(CenterId, screenCenter);
            runtimeMaterial.SetVector(RadiusId, screenRadius);
            runtimeMaterial.SetFloat(FeatherId, normalizedFeather);
            runtimeMaterial.SetFloat(OutsideDarknessId, gameplayArea.OutsideDarkness);
        }

        private void RefreshReferenceDebug()
        {
            gameplayAreaAssigned = gameplayArea != null;
            if (gameplayCamera == null && gameplayAreaAssigned)
            {
                gameplayCamera = gameplayArea.GameplayCamera;
            }

            cameraAssigned = gameplayCamera != null;
            Material assignedMaterial = runtimeMaterial != null ? runtimeMaterial : maskMaterialTemplate;
            maskMaterialAssigned = darknessMask != null && assignedMaterial != null
                && assignedMaterial.shader != null
                && assignedMaterial.shader.name == "ShadowPath/UI/Light Gameplay Mask";
        }
    }
}
