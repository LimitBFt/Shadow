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
        [SerializeField] private Image darknessMask;
        [SerializeField] private Material maskMaterialTemplate;

        [Header("Runtime Debug")]
        [SerializeField] private Vector2 currentViewportCenter;
        [SerializeField] private Vector2 currentViewportRadius;

        private Material runtimeMaterial;

        private void Awake()
        {
            if (darknessMask == null || maskMaterialTemplate == null)
            {
                Debug.LogError(
                    $"{nameof(LightGameplayMaskController)} requires a Darkness Mask Image " +
                    "and Mask Material Template.",
                    this);
                enabled = false;
                return;
            }

            runtimeMaterial = new Material(maskMaterialTemplate)
            {
                name = $"{maskMaterialTemplate.name} (Runtime)"
            };
            darknessMask.material = runtimeMaterial;
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
            if (runtimeMaterial == null
                || gameplayArea == null
                || !gameplayArea.TryGetGameplayCenterWorldPosition(out Vector3 centerWorldPosition))
            {
                return;
            }

            UnityEngine.Camera gameplayCamera = gameplayArea.GameplayCamera;
            if (gameplayCamera == null)
            {
                return;
            }

            Vector2 visualRadius = gameplayArea.VisualRadius;
            Vector3 centerViewport = gameplayCamera.WorldToViewportPoint(centerWorldPosition);
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
            runtimeMaterial.SetVector(CenterId, currentViewportCenter);
            runtimeMaterial.SetVector(RadiusId, currentViewportRadius);
            runtimeMaterial.SetFloat(FeatherId, normalizedFeather);
            runtimeMaterial.SetFloat(OutsideDarknessId, gameplayArea.OutsideDarkness);
        }
    }
}
