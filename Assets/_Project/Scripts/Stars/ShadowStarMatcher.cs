using ShadowPath.Shadow;
using UnityEngine;

namespace ShadowPath.Stars
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StarCollectible))]
    public class ShadowStarMatcher : MonoBehaviour
    {
        private const int OutlinePointCount = 10;
        private const float DefaultCheckInterval = 0.05f;

        [SerializeField] private StarCollectible targetStar;
        [SerializeField] private ShadowSystem shadowSystem;
        [SerializeField] private ShadowCaster sourceShadowCaster;
        [SerializeField, Tooltip("Ten points under the caster. Start at a named outer tip and alternate outer and inner points.")]
        private Transform[] sourceOutlinePoints = new Transform[OutlinePointCount];
        [SerializeField, Tooltip("Ten points in the same index order as the source points.")]
        private Transform[] targetOutlinePoints = new Transform[OutlinePointCount];
        [SerializeField, Min(0f)] private float matchTolerance = 0.1f;
        [SerializeField, Min(0f)] private float matchHoldDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float checkInterval = DefaultCheckInterval;
        [SerializeField, Tooltip("Phase 2 requires a permanent unlock. Keep this enabled.")]
        private bool unlockPermanently = true;

        [SerializeField] private GameObject lockedVisual;
        [SerializeField] private GameObject unlockedVisual;
        [SerializeField, Tooltip("Optional dedicated line renderer for the projected ten-point star.")]
        private LineRenderer projectedOutline;
        [SerializeField, Tooltip("Visual-only offset along the Projection Plane's forward axis.")]
        private float outlineDepthOffset;

        [Header("Runtime Debug")]
        [SerializeField] private bool runtimeDebug;
        [SerializeField] private string matchStatus;
        [SerializeField, Range(0f, 1f)] private float matchingProgress;

        private readonly Vector3[] projectedPositions = new Vector3[OutlinePointCount];
        private string setupError;
        private double nextCheckTime;
        private double? matchingSince;
        private bool unlocked;

        private void Reset() => targetStar = GetComponent<StarCollectible>();

        private void Awake()
        {
            if (projectedOutline != null)
            {
                projectedOutline.useWorldSpace = true;
                projectedOutline.loop = true;
                projectedOutline.positionCount = OutlinePointCount;
                projectedOutline.enabled = false;
            }

            StarCollectible localStar = GetComponent<StarCollectible>();
            localStar.SetLocked(true);
            if (targetStar == null) targetStar = localStar;
            if (targetStar != localStar || targetStar.Type != StarCollectible.StarType.Large)
            {
                setupError = "Target Star must be the Large Star on this GameObject.";
                return;
            }

            if (!targetStar.TrySetAlignmentVisuals(lockedVisual, unlockedVisual))
                setupError = "Use separate visual children of the Target Star, independent of Collected Visual.";
        }

        private void OnEnable()
        {
            nextCheckTime = 0d;
            matchingSince = null;
        }

        private void OnDisable() => SetProjectedOutlineVisible(false);

        private void LateUpdate()
        {
            if (unlocked || (targetStar != null && targetStar.IsCollected)) return;
            double now = Time.timeAsDouble;
            if (now < nextCheckTime) return;
            nextCheckTime = now + (IsNonnegativeFinite(checkInterval)
                ? Mathf.Max(0.01f, checkInterval) : DefaultCheckInterval);

            string mismatch = GetMismatchReason(out bool hasProjectedOutline);
            SetProjectedOutlineVisible(hasProjectedOutline);
            if (mismatch != null)
            {
                matchingSince = null;
                if (runtimeDebug) SetDebugStatus($"Not Matching — {mismatch}", 0f);
                return;
            }

            matchingSince ??= now;
            double elapsed = now - matchingSince.Value;
            if (elapsed < matchHoldDuration)
            {
                SetDebugStatus("Matching", (float)(elapsed / matchHoldDuration));
                return;
            }

            unlocked = true;
            SetProjectedOutlineVisible(false);
            targetStar.SetLocked(false);
            SetDebugStatus("Unlocked", 1f);
        }

        private string GetMismatchReason(out bool hasProjectedOutline)
        {
            hasProjectedOutline = false;
            if (setupError != null) return setupError;
            if (!unlockPermanently) return "Enable Unlock Permanently for this star.";
            if (targetStar == null || sourceShadowCaster == null || shadowSystem == null)
                return "Assign Target Star, Source Shadow Caster and Shadow System.";
            if (sourceOutlinePoints?.Length != OutlinePointCount || targetOutlinePoints?.Length != OutlinePointCount)
                return "Source and Target Outline Points must each have exactly ten entries.";
            if (!IsNonnegativeFinite(matchTolerance) || !IsNonnegativeFinite(matchHoldDuration)
                || !IsNonnegativeFinite(checkInterval))
            {
                return "Match Tolerance, Match Hold Duration and Check Interval must be finite nonnegative values.";
            }

            if (!shadowSystem.TryGetRuntimeProjection(sourceShadowCaster, out Transform light, out Transform plane))
                return "No valid runtime projection for this caster. Check the Shadow System, light and caster detection.";

            Vector3 lightPosition = light.position;
            Vector3 planeOrigin = plane.position;
            Vector3 planeNormal = plane.forward;
            Vector3 planeRight = plane.right;
            Vector3 planeUp = plane.up;
            Vector3 visualOffset = planeNormal * outlineDepthOffset;
            Transform caster = sourceShadowCaster.transform;
            int firstOutsideTolerance = -1;
            for (int i = 0; i < OutlinePointCount; i++)
            {
                Transform source = sourceOutlinePoints[i];
                Transform target = targetOutlinePoints[i];
                if (source == null || target == null) return $"Missing outline point at index {i}.";
                if (source == caster || !source.IsChildOf(caster))
                    return $"Source point {i} must be a child of the Shadow Caster.";

                for (int previous = 0; previous < i; previous++)
                {
                    if (source == sourceOutlinePoints[previous] || target == targetOutlinePoints[previous])
                        return $"Duplicate outline point at index {i}.";
                }

                if (!ShadowProjectionCalculator.TryProjectPoint(lightPosition, source.position, planeOrigin,
                    planeNormal, out Vector3 projectedPoint, out ShadowProjectionFailureReason projectionFailure))
                {
                    return $"Source point {i} projection failed ({projectionFailure}).";
                }

                if (!ProjectionPlaneCoordinates.TryWorldToPlane2D(projectedPoint, planeOrigin, planeRight,
                        planeUp, out Vector2 projected2D)
                    || !ProjectionPlaneCoordinates.TryWorldToPlane2D(target.position, planeOrigin, planeRight,
                        planeUp, out Vector2 target2D))
                {
                    return $"Could not convert pair {i} to Projection Plane coordinates.";
                }

                float error = Vector2.Distance(projected2D, target2D);
                if (!IsNonnegativeFinite(error)) return $"Outline pair {i} has a non-finite error.";
                if (firstOutsideTolerance < 0 && error > matchTolerance) firstOutsideTolerance = i;
                projectedPositions[i] = projectedPoint + visualOffset;
            }
            hasProjectedOutline = true;
            if (firstOutsideTolerance < 0) return null;
            return runtimeDebug ? $"Outline pair {firstOutsideTolerance} exceeds Match Tolerance."
                : "Outline pair exceeds Match Tolerance.";
        }

        private static bool IsNonnegativeFinite(float value) => value >= 0f && !float.IsInfinity(value);

        private void SetProjectedOutlineVisible(bool visible)
        {
            if (projectedOutline == null) return;
            if (visible) projectedOutline.SetPositions(projectedPositions);
            projectedOutline.enabled = visible;
        }

        private void SetDebugStatus(string status, float progress)
        {
            if (!runtimeDebug) return;
            matchStatus = status;
            matchingProgress = progress;
        }
    }
}
