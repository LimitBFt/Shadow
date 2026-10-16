using UnityEngine;

namespace ShadowPath.Camera
{
    public class OccludableObject : MonoBehaviour
    {
        private static readonly int OcclusionAmountId = Shader.PropertyToID("_OcclusionAmount");

        [SerializeField] private Renderer[] renderers;

        private MaterialPropertyBlock propertyBlock;
        private float targetOcclusionAmount;
        private float occlusionAmount;

        public float OcclusionAmount => occlusionAmount;

        private void Awake()
        {
            CacheRenderers();
            SetOcclusionAmount(0f);
        }

        public void SetOccluded(bool isOccluded)
        {
            targetOcclusionAmount = isOccluded ? 1f : 0f;
        }

        public void SetOcclusionAmount(float amount)
        {
            occlusionAmount = Mathf.Clamp01(amount);
            ApplyPropertyBlock();
        }

        internal void StepOcclusion(float deltaTime, float fadeInSpeed, float fadeOutSpeed)
        {
            float speed = targetOcclusionAmount > occlusionAmount
                ? fadeInSpeed
                : fadeOutSpeed;
            float nextAmount = Mathf.MoveTowards(
                occlusionAmount,
                targetOcclusionAmount,
                Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime));

            if (!Mathf.Approximately(nextAmount, occlusionAmount))
            {
                SetOcclusionAmount(nextAmount);
            }
        }

        private void CacheRenderers()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private void ApplyPropertyBlock()
        {
            CacheRenderers();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer cachedRenderer = renderers[i];
                if (cachedRenderer == null)
                {
                    continue;
                }

                cachedRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(OcclusionAmountId, occlusionAmount);
                cachedRenderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        private void OnDisable()
        {
            targetOcclusionAmount = 0f;
            SetOcclusionAmount(0f);
        }
    }
}
