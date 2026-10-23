using System.Collections.Generic;
using UnityEngine;

namespace ShadowPath.Shadow
{
    public class ShadowSystem : MonoBehaviour
    {
        private const string RuntimeShadowPrefix = "GeneratedShadow_";
        private const string PreviewShadowPrefix = "PreviewShadow_";

        [SerializeField] private Light sourceLight;
        [SerializeField] private Transform lightSource;
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private List<ShadowCaster> shadowCasters = new List<ShadowCaster>();
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private float surfaceOffset = 0.002f;
        [SerializeField, Min(0.001f)] private float thickness = 0.1f;
        [SerializeField] private float hullEpsilon = 0.0001f;
        [SerializeField] private bool showGeneratedShadowVisual = true;
        [SerializeField, Min(0f)] private float movementThreshold = 0.001f;
        [SerializeField, Min(0f)] private float casterPositionThreshold = 0.001f;
        [SerializeField, Min(0f)] private float casterRotationThreshold = 0.1f;
        [SerializeField, Min(0f)] private float casterScaleThreshold = 0.001f;
        [SerializeField] private bool autoRebuildWhenLightMoves = true;
        [SerializeField] private bool autoDetectShadowCasters = true;
        [SerializeField] private LayerMask shadowCasterMask = ~0;
        [SerializeField, Min(8)] private int detectionBufferSize = 64;
        [SerializeField, Min(0.02f)] private float detectionInterval = 0.1f;

        private readonly Dictionary<ShadowCaster, GeneratedShadow> generatedShadows =
            new Dictionary<ShadowCaster, GeneratedShadow>();

        private readonly Dictionary<ShadowCaster, TransformSnapshot> casterTransformSnapshots =
            new Dictionary<ShadowCaster, TransformSnapshot>();

        private readonly HashSet<ShadowCaster> detectedShadowCasters = new HashSet<ShadowCaster>();
        private readonly List<ShadowCaster> castersLeavingRange = new List<ShadowCaster>();
        private readonly List<GeneratedShadow> previewShadows = new List<GeneratedShadow>();

        private Collider[] detectionBuffer;
        private Vector3 lastLightPosition;
        private float detectionTimer;
        private bool initialized;
        private bool warnedInvalidSourceLight;
        private bool warnedDetectionBufferFull;

        private void OnValidate()
        {
            ApplyGeneratedShadowVisualVisibility();
        }

        private void Start()
        {
            InitializeShadows();
        }

        private void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            Transform runtimeLightTransform = GetRuntimeLightTransform();
            if (autoRebuildWhenLightMoves && runtimeLightTransform != null && HasLightSourceMoved(runtimeLightTransform))
            {
                RebuildAllGeneratedShadows();
                lastLightPosition = runtimeLightTransform.position;
            }
            else
            {
                RebuildMovedCasterShadows();
            }

            if (autoDetectShadowCasters)
            {
                detectionTimer += Time.fixedDeltaTime;
                if (detectionTimer >= Mathf.Max(0.02f, detectionInterval))
                {
                    detectionTimer = 0f;
                    DetectShadowCastersInRange();
                }
            }
        }

        private bool HasLightSourceMoved(Transform runtimeLightTransform)
        {
            Vector3 lightPosition = runtimeLightTransform.position;
            float threshold = Mathf.Max(0f, movementThreshold);
            return (lightPosition - lastLightPosition).sqrMagnitude > threshold * threshold;
        }

        private void RebuildAllGeneratedShadows()
        {
            foreach (KeyValuePair<ShadowCaster, GeneratedShadow> pair in generatedShadows)
            {
                if (pair.Value != null)
                {
                    pair.Value.Rebuild();
                }

                if (pair.Key != null)
                {
                    StoreCasterTransformSnapshot(pair.Key);
                }
            }
        }

        private void RebuildMovedCasterShadows()
        {
            foreach (KeyValuePair<ShadowCaster, GeneratedShadow> pair in generatedShadows)
            {
                ShadowCaster caster = pair.Key;
                GeneratedShadow generatedShadow = pair.Value;
                if (caster == null || generatedShadow == null || !HasCasterTransformChanged(caster))
                {
                    continue;
                }

                generatedShadow.Rebuild();
                StoreCasterTransformSnapshot(caster);
            }
        }

        private void InitializeShadows()
        {
            ClearPreviewShadowsInternal();
            ClearGeneratedShadows();
            initialized = false;
            detectionTimer = 0f;

            if (projectionPlane == null)
            {
                Debug.LogWarning("ShadowSystem requires a ProjectionPlane.", this);
                return;
            }

            Transform runtimeLightTransform = GetRuntimeLightTransform();
            if (runtimeLightTransform == null)
            {
                return;
            }

            lastLightPosition = runtimeLightTransform.position;

            if (autoDetectShadowCasters)
            {
                initialized = true;
                DetectShadowCastersInRange();
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
                TryAddGeneratedShadow(caster, i, runtimeLightTransform);
            }

            initialized = true;
        }

        private Transform GetRuntimeLightTransform()
        {
            if (!autoDetectShadowCasters)
            {
                if (lightSource == null)
                {
                    Debug.LogWarning("ShadowSystem requires a LightSource Transform in manual ShadowCaster mode.", this);
                    return null;
                }

                return lightSource;
            }

            if (sourceLight == null)
            {
                WarnInvalidSourceLight("ShadowSystem requires a Point or Spot sourceLight for automatic ShadowCaster detection.");
                return null;
            }

            if (sourceLight.type != LightType.Point && sourceLight.type != LightType.Spot)
            {
                WarnInvalidSourceLight("ShadowSystem automatic ShadowCaster detection only supports Point or Spot Light.");
                return null;
            }

            warnedInvalidSourceLight = false;
            return lightSource != null ? lightSource : sourceLight.transform;
        }

        private void WarnInvalidSourceLight(string message)
        {
            if (warnedInvalidSourceLight)
            {
                return;
            }

            Debug.LogWarning(message, this);
            warnedInvalidSourceLight = true;
        }

        private void DetectShadowCastersInRange()
        {
            if (!TryCollectShadowCastersInRange())
            {
                return;
            }

            int casterIndex = 0;
            foreach (ShadowCaster caster in detectedShadowCasters)
            {
                if (!generatedShadows.ContainsKey(caster))
                {
                    TryAddGeneratedShadow(caster, casterIndex, GetRuntimeLightTransform());
                }

                casterIndex++;
            }

            RemoveCastersOutsideRange();
        }

        private bool TryCollectShadowCastersInRange()
        {
            if (sourceLight == null || (sourceLight.type != LightType.Point && sourceLight.type != LightType.Spot))
            {
                return false;
            }

            EnsureDetectionBuffer();
            detectedShadowCasters.Clear();
            int hitCount = Physics.OverlapSphereNonAlloc(
                GetRuntimeLightTransform().position,
                sourceLight.range,
                detectionBuffer,
                shadowCasterMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount == detectionBuffer.Length && !warnedDetectionBufferFull)
            {
                Debug.LogWarning("ShadowSystem ShadowCaster detection buffer is full. Increase Detection Buffer Size.", this);
                warnedDetectionBufferFull = true;
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider detectedCollider = detectionBuffer[i];
                if (detectedCollider == null)
                {
                    continue;
                }

                ShadowCaster caster = detectedCollider.GetComponentInParent<ShadowCaster>();
                if (caster != null)
                {
                    detectedShadowCasters.Add(caster);
                }
            }

            return true;
        }

        private void EnsureDetectionBuffer()
        {
            int safeBufferSize = Mathf.Max(8, detectionBufferSize);
            if (detectionBuffer != null && detectionBuffer.Length == safeBufferSize)
            {
                return;
            }

            detectionBuffer = new Collider[safeBufferSize];
            warnedDetectionBufferFull = false;
        }

        private void RemoveCastersOutsideRange()
        {
            castersLeavingRange.Clear();
            foreach (KeyValuePair<ShadowCaster, GeneratedShadow> pair in generatedShadows)
            {
                if (pair.Key != null && !detectedShadowCasters.Contains(pair.Key))
                {
                    castersLeavingRange.Add(pair.Key);
                }
            }

            for (int i = 0; i < castersLeavingRange.Count; i++)
            {
                RemoveGeneratedShadow(castersLeavingRange[i]);
            }
        }

        private bool TryAddGeneratedShadow(ShadowCaster caster, int casterIndex, Transform runtimeLightTransform)
        {
            if (!TryValidateCaster(caster, casterIndex))
            {
                return false;
            }

            GeneratedShadow generatedShadow = CreateGeneratedShadow(caster, RuntimeShadowPrefix, casterIndex);
            generatedShadow.Initialize(
                caster,
                runtimeLightTransform,
                projectionPlane,
                shadowMaterial,
                surfaceOffset,
                thickness,
                hullEpsilon);
            generatedShadow.SetVisualVisible(showGeneratedShadowVisual);

            generatedShadows.Add(caster, generatedShadow);
            generatedShadow.Rebuild();
            StoreCasterTransformSnapshot(caster);
            return true;
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

            Transform previewLightTransform = GetPreviewLightTransform();
            if (previewLightTransform == null || projectionPlane == null)
            {
                Debug.LogWarning("ShadowSystem requires a LightSource and ProjectionPlane to rebuild preview shadows.", this);
                return;
            }

            if (autoDetectShadowCasters)
            {
                if (!TryCollectShadowCastersInRange())
                {
                    Debug.LogWarning("ShadowSystem requires a Point Light sourceLight to rebuild automatic preview shadows.", this);
                    return;
                }

                int casterIndex = 0;
                foreach (ShadowCaster caster in detectedShadowCasters)
                {
                    TryCreatePreviewShadow(caster, casterIndex, previewLightTransform, false);
                    casterIndex++;
                }

                return;
            }

            if (shadowCasters == null)
            {
                Debug.LogWarning("ShadowSystem has no ShadowCaster list.", this);
                return;
            }

            for (int i = 0; i < shadowCasters.Count; i++)
            {
                TryCreatePreviewShadow(shadowCasters[i], i, previewLightTransform, true);
            }
        }

        private Transform GetPreviewLightTransform()
        {
            if (lightSource != null)
            {
                return lightSource;
            }

            if (sourceLight != null && (sourceLight.type == LightType.Point || sourceLight.type == LightType.Spot))
            {
                return sourceLight.transform;
            }

            return null;
        }

        private void TryCreatePreviewShadow(
            ShadowCaster caster,
            int casterIndex,
            Transform previewLightTransform,
            bool checkManualListDuplicates)
        {
            if (!TryValidatePreviewCaster(caster, casterIndex, checkManualListDuplicates))
            {
                return;
            }

            GeneratedShadow previewShadow = CreateGeneratedShadow(caster, PreviewShadowPrefix, casterIndex);
            previewShadow.Initialize(
                caster,
                previewLightTransform,
                projectionPlane,
                shadowMaterial,
                surfaceOffset,
                thickness,
                hullEpsilon);
            previewShadow.SetVisualVisible(showGeneratedShadowVisual);

            previewShadows.Add(previewShadow);
            previewShadow.Rebuild();
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

        private bool TryValidatePreviewCaster(ShadowCaster caster, int index, bool checkManualListDuplicates)
        {
            if (caster == null)
            {
                Debug.LogWarning("ShadowSystem skipped a null preview ShadowCaster at index " + index + ".", this);
                return false;
            }

            if (checkManualListDuplicates && IsDuplicatePreviewCaster(caster, index))
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
            int sameNameCount = namePrefix == RuntimeShadowPrefix
                ? CountRuntimeCastersWithName(casterName)
                : CountPreviewShadowsWithName(casterName);

            if (sameNameCount == 0)
            {
                return namePrefix + casterName;
            }

            return namePrefix + casterName + sameNameCount;
        }

        private int CountRuntimeCastersWithName(string casterName)
        {
            int sameNameCount = 0;
            foreach (KeyValuePair<ShadowCaster, GeneratedShadow> pair in generatedShadows)
            {
                if (pair.Key != null && pair.Key.name == casterName)
                {
                    sameNameCount++;
                }
            }

            return sameNameCount;
        }

        private int CountPreviewShadowsWithName(string casterName)
        {
            int sameNameCount = 0;
            for (int i = 0; i < previewShadows.Count; i++)
            {
                GeneratedShadow previewShadow = previewShadows[i];
                if (previewShadow != null && IsMatchingPreviewShadowName(previewShadow.name, casterName))
                {
                    sameNameCount++;
                }
            }

            return sameNameCount;
        }

        private bool IsMatchingPreviewShadowName(string shadowName, string casterName)
        {
            string baseName = PreviewShadowPrefix + casterName;
            if (shadowName == baseName)
            {
                return true;
            }

            if (!shadowName.StartsWith(baseName))
            {
                return false;
            }

            for (int i = baseName.Length; i < shadowName.Length; i++)
            {
                if (!char.IsDigit(shadowName[i]))
                {
                    return false;
                }
            }

            return shadowName.Length > baseName.Length;
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
            casterTransformSnapshots.Clear();
        }

        private void RemoveGeneratedShadow(ShadowCaster caster)
        {
            if (caster == null || !generatedShadows.TryGetValue(caster, out GeneratedShadow generatedShadow))
            {
                return;
            }

            generatedShadows.Remove(caster);
            casterTransformSnapshots.Remove(caster);
            DestroyGeneratedShadowObject(generatedShadow);
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

        private void ApplyGeneratedShadowVisualVisibility()
        {
            foreach (KeyValuePair<ShadowCaster, GeneratedShadow> pair in generatedShadows)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetVisualVisible(showGeneratedShadowVisual);
                }
            }

            for (int i = 0; i < previewShadows.Count; i++)
            {
                GeneratedShadow previewShadow = previewShadows[i];
                if (previewShadow != null)
                {
                    previewShadow.SetVisualVisible(showGeneratedShadowVisual);
                }
            }
        }

        private void OnDestroy()
        {
            ClearPreviewShadowsInternal();
            ClearGeneratedShadows();
        }

        private bool HasCasterTransformChanged(ShadowCaster caster)
        {
            TransformSnapshot currentSnapshot = TransformSnapshot.Capture(caster.transform);
            if (!casterTransformSnapshots.TryGetValue(caster, out TransformSnapshot previousSnapshot))
            {
                return true;
            }

            float positionThreshold = Mathf.Max(0f, casterPositionThreshold);
            if ((currentSnapshot.Position - previousSnapshot.Position).sqrMagnitude > positionThreshold * positionThreshold)
            {
                return true;
            }

            if (Quaternion.Angle(currentSnapshot.Rotation, previousSnapshot.Rotation) > Mathf.Max(0f, casterRotationThreshold))
            {
                return true;
            }

            float scaleThreshold = Mathf.Max(0f, casterScaleThreshold);
            return (currentSnapshot.Scale - previousSnapshot.Scale).sqrMagnitude > scaleThreshold * scaleThreshold;
        }

        private void StoreCasterTransformSnapshot(ShadowCaster caster)
        {
            casterTransformSnapshots[caster] = TransformSnapshot.Capture(caster.transform);
        }

        private struct TransformSnapshot
        {
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }

            private TransformSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public static TransformSnapshot Capture(Transform source)
            {
                return new TransformSnapshot(
                    source.position,
                    source.rotation,
                    source.lossyScale);
            }
        }
    }
}
