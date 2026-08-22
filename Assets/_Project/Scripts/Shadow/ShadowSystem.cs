using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    public class ShadowSystem : MonoBehaviour
    {
        private const string RuntimeShadowPrefix = "GeneratedShadow_";
        private const string PreviewShadowPrefix = "PreviewShadow_";

        [SerializeField] private Transform lightSource;
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private List<ShadowCaster> shadowCasters = new List<ShadowCaster>();
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private float surfaceOffset = 0.002f;
        [SerializeField, Min(0.001f)] private float thickness = 0.1f;
        [SerializeField] private float hullEpsilon = 0.0001f;
        [SerializeField, Min(0f)] private float movementThreshold = 0.001f;
        [SerializeField] private bool autoRebuildWhenLightMoves = true;

        private readonly Dictionary<ShadowCaster, GeneratedShadow> generatedShadows =
            new Dictionary<ShadowCaster, GeneratedShadow>();

        private readonly List<GeneratedShadow> previewShadows = new List<GeneratedShadow>();

        private Vector3 lastLightPosition;
        private bool initialized;

        private void Start()
        {
            InitializeShadows();
        }

        private void FixedUpdate()
        {
            if (!initialized || !autoRebuildWhenLightMoves || lightSource == null)
            {
                return;
            }

            Vector3 lightPosition = lightSource.position;
            float threshold = Mathf.Max(0f, movementThreshold);
            if ((lightPosition - lastLightPosition).sqrMagnitude <= threshold * threshold)
            {
                return;
            }

            foreach (KeyValuePair<ShadowCaster, GeneratedShadow> pair in generatedShadows)
            {
                if (pair.Value != null)
                {
                    pair.Value.Rebuild();
                }
            }

            lastLightPosition = lightPosition;
        }

        private void InitializeShadows()
        {
            ClearPreviewShadowsInternal();
            ClearGeneratedShadows();
            initialized = false;

            if (lightSource == null || projectionPlane == null)
            {
                Debug.LogWarning("ShadowSystem requires a LightSource and ProjectionPlane.", this);
                return;
            }

            lastLightPosition = lightSource.position;

            if (shadowCasters == null)
            {
                Debug.LogWarning("ShadowSystem has no ShadowCaster list.", this);
                return;
            }

            for (int i = 0; i < shadowCasters.Count; i++)
            {
                ShadowCaster caster = shadowCasters[i];
                if (!TryValidateCaster(caster, i))
                {
                    continue;
                }

                GeneratedShadow generatedShadow = CreateGeneratedShadow(caster, RuntimeShadowPrefix, i);
                generatedShadow.Initialize(
                    caster,
                    lightSource,
                    projectionPlane,
                    shadowMaterial,
                    surfaceOffset,
                    thickness,
                    hullEpsilon);

                generatedShadows.Add(caster, generatedShadow);
                generatedShadow.Rebuild();
            }

            initialized = true;
        }

        [ContextMenu("Rebuild Preview Shadows")]
        private void RebuildPreviewShadows()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Preview shadows are intended for Edit Mode. Runtime shadows are managed by ShadowSystem during Play Mode.", this);
                return;
            }

            ClearPreviewShadowsInternal();

            if (lightSource == null || projectionPlane == null)
            {
                Debug.LogWarning("ShadowSystem requires a LightSource and ProjectionPlane to rebuild preview shadows.", this);
                return;
            }

            if (shadowCasters == null)
            {
                Debug.LogWarning("ShadowSystem has no ShadowCaster list.", this);
                return;
            }

            for (int i = 0; i < shadowCasters.Count; i++)
            {
                ShadowCaster caster = shadowCasters[i];
                if (!TryValidatePreviewCaster(caster, i))
                {
                    continue;
                }

                GeneratedShadow previewShadow = CreateGeneratedShadow(caster, PreviewShadowPrefix, i);
                previewShadow.Initialize(
                    caster,
                    lightSource,
                    projectionPlane,
                    shadowMaterial,
                    surfaceOffset,
                    thickness,
                    hullEpsilon);

                previewShadows.Add(previewShadow);
                previewShadow.Rebuild();
            }
        }

        [ContextMenu("Clear Preview Shadows")]
        private void ClearPreviewShadows()
        {
            ClearPreviewShadowsInternal();
        }

        private bool TryValidateCaster(ShadowCaster caster, int index)
        {
            if (caster == null)
            {
                Debug.LogWarning("ShadowSystem skipped a null ShadowCaster at index " + index + ".", this);
                return false;
            }

            if (generatedShadows.ContainsKey(caster))
            {
                Debug.LogWarning("ShadowSystem skipped duplicate ShadowCaster " + caster.name + ".", caster);
                return false;
            }

            if (!caster.IsValid)
            {
                Debug.LogWarning("ShadowSystem skipped invalid ShadowCaster " + caster.name + ".", caster);
                return false;
            }

            return true;
        }

        private bool TryValidatePreviewCaster(ShadowCaster caster, int index)
        {
            if (caster == null)
            {
                Debug.LogWarning("ShadowSystem skipped a null preview ShadowCaster at index " + index + ".", this);
                return false;
            }

            if (IsDuplicatePreviewCaster(caster, index))
            {
                Debug.LogWarning("ShadowSystem skipped duplicate preview ShadowCaster " + caster.name + ".", caster);
                return false;
            }

            if (!caster.IsValid)
            {
                Debug.LogWarning("ShadowSystem skipped invalid preview ShadowCaster " + caster.name + ".", caster);
                return false;
            }

            return true;
        }

        private bool IsDuplicatePreviewCaster(ShadowCaster caster, int currentIndex)
        {
            for (int i = 0; i < currentIndex; i++)
            {
                if (shadowCasters[i] == caster)
                {
                    return true;
                }
            }

            return false;
        }

        private GeneratedShadow CreateGeneratedShadow(ShadowCaster caster, string namePrefix, int casterIndex)
        {
            GameObject shadowObject = new GameObject(BuildShadowName(namePrefix, caster.name, casterIndex));
            Transform shadowTransform = shadowObject.transform;
            shadowTransform.SetParent(projectionPlane, false);
            shadowTransform.localPosition = Vector3.zero;
            shadowTransform.localRotation = Quaternion.identity;
            shadowTransform.localScale = Vector3.one;

            shadowObject.AddComponent<MeshFilter>();
            shadowObject.AddComponent<MeshRenderer>();
            shadowObject.AddComponent<MeshCollider>();
            return shadowObject.AddComponent<GeneratedShadow>();
        }

        private string BuildShadowName(string namePrefix, string casterName, int casterIndex)
        {
            int sameNameCount = 0;
            for (int i = 0; i < casterIndex; i++)
            {
                ShadowCaster previousCaster = shadowCasters[i];
                if (previousCaster != null && previousCaster.name == casterName)
                {
                    sameNameCount++;
                }
            }

            if (sameNameCount == 0)
            {
                return namePrefix + casterName;
            }

            return namePrefix + casterName + sameNameCount;
        }

        private void ClearGeneratedShadows()
        {
            foreach (KeyValuePair<ShadowCaster, GeneratedShadow> pair in generatedShadows)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                GameObject shadowObject = pair.Value.gameObject;
                if (Application.isPlaying)
                {
                    Destroy(shadowObject);
                }
                else
                {
                    DestroyImmediate(shadowObject);
                }
            }

            generatedShadows.Clear();
        }

        private void ClearPreviewShadowsInternal()
        {
            for (int i = previewShadows.Count - 1; i >= 0; i--)
            {
                DestroyGeneratedShadowObject(previewShadows[i]);
            }

            previewShadows.Clear();
            if (projectionPlane == null)
            {
                return;
            }

            for (int i = projectionPlane.childCount - 1; i >= 0; i--)
            {
                Transform child = projectionPlane.GetChild(i);
                if (child == null || !child.name.StartsWith(PreviewShadowPrefix))
                {
                    continue;
                }

                GeneratedShadow previewShadow = child.GetComponent<GeneratedShadow>();
                if (previewShadow != null)
                {
                    DestroyGeneratedShadowObject(previewShadow);
                }
            }
        }

        private void DestroyGeneratedShadowObject(GeneratedShadow generatedShadow)
        {
            if (generatedShadow == null)
            {
                return;
            }

            GameObject shadowObject = generatedShadow.gameObject;
            if (Application.isPlaying)
            {
                Destroy(shadowObject);
            }
            else
            {
                DestroyImmediate(shadowObject);
            }
        }

        private void OnDestroy()
        {
            ClearPreviewShadowsInternal();
            ClearGeneratedShadows();
        }
    }
}
