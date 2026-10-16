using ShadowPath.Shadow;
using UnityEngine;

namespace ShadowPath.Camera
{
    [DefaultExecutionOrder(100)]
    public class CameraOcclusionController : MonoBehaviour
    {
        private const int HitBufferSize = 32;
        private const int TrackedOccluderCapacity = 64;
        private const float MinimumCastDistance = 0.0001f;

        private static readonly int OcclusionCenterId = Shader.PropertyToID("_OcclusionCenter");
        private static readonly int InnerRadiusId = Shader.PropertyToID("_InnerRadius");
        private static readonly int OuterRadiusId = Shader.PropertyToID("_OuterRadius");
        private static readonly int OcclusionAspectId = Shader.PropertyToID("_OcclusionAspect");

        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private LayerMask occluderMask;

        [Header("Occlusion Detection")]
        [SerializeField, Min(0f)] private float sphereRadius = 0.2f;

        [Header("Screen-space Mask")]
        [SerializeField, Min(0f)] private float innerRadius = 0.07f;
        [SerializeField, Min(0f)] private float outerRadius = 0.14f;
        [SerializeField] private Vector2 screenCenterOffset;

        [Header("Transition")]
        [SerializeField, Min(0f)] private float fadeInSpeed = 6f;
        [SerializeField, Min(0f)] private float fadeOutSpeed = 4f;

        [Header("Debug")]
        [SerializeField] private bool drawDebug;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];
        private readonly OccludableObject[] frameOccluders = new OccludableObject[HitBufferSize];
        private readonly OccludableObject[] trackedOccluders = new OccludableObject[TrackedOccluderCapacity];

        private int frameOccluderCount;
        private int trackedOccluderCount;

        private void LateUpdate()
        {
            if (target == null || targetCamera == null)
            {
                ReleaseTrackedOccluders();
                return;
            }

            UpdateShaderGlobals();
            CollectFrameOccluders();
            UpdateTrackedOccluders();
        }

        private void CollectFrameOccluders()
        {
            frameOccluderCount = 0;

            Vector3 castOrigin = targetCamera.transform.position;
            Vector3 castOffset = target.position - castOrigin;
            float castDistance = castOffset.magnitude;
            if (castDistance <= MinimumCastDistance)
            {
                return;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                castOrigin,
                sphereRadius,
                castOffset / castDistance,
                hitBuffer,
                castDistance,
                occluderMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = hitBuffer[i].collider;
                if (ShouldIgnore(hitCollider))
                {
                    continue;
                }

                OccludableObject occluder = hitCollider.GetComponentInParent<OccludableObject>();
                if (occluder == null || Contains(frameOccluders, frameOccluderCount, occluder))
                {
                    continue;
                }

                frameOccluders[frameOccluderCount] = occluder;
                frameOccluderCount++;
            }
        }

        private bool ShouldIgnore(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return true;
            }

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == target
                || hitTransform.IsChildOf(target)
                || target.IsChildOf(hitTransform))
            {
                return true;
            }

            Transform cameraTransform = targetCamera.transform;
            if (hitTransform == cameraTransform
                || hitTransform.IsChildOf(cameraTransform)
                || cameraTransform.IsChildOf(hitTransform))
            {
                return true;
            }

            return hitCollider.GetComponentInParent<GeneratedShadow>() != null;
        }

        private void UpdateTrackedOccluders()
        {
            for (int i = 0; i < frameOccluderCount; i++)
            {
                OccludableObject occluder = frameOccluders[i];
                if (!Contains(trackedOccluders, trackedOccluderCount, occluder)
                    && trackedOccluderCount < trackedOccluders.Length)
                {
                    trackedOccluders[trackedOccluderCount] = occluder;
                    trackedOccluderCount++;
                }
            }

            int trackedIndex = 0;
            while (trackedIndex < trackedOccluderCount)
            {
                OccludableObject occluder = trackedOccluders[trackedIndex];
                if (occluder == null)
                {
                    RemoveTrackedAt(trackedIndex);
                    continue;
                }

                bool isOccluded = Contains(frameOccluders, frameOccluderCount, occluder);
                occluder.SetOccluded(isOccluded);
                occluder.StepOcclusion(Time.deltaTime, fadeInSpeed, fadeOutSpeed);

                if (!isOccluded && occluder.OcclusionAmount <= 0f)
                {
                    RemoveTrackedAt(trackedIndex);
                    continue;
                }

                trackedIndex++;
            }
        }

        private void UpdateShaderGlobals()
        {
            Vector3 viewportPosition = targetCamera.WorldToViewportPoint(target.position);
            Vector2 center = new Vector2(viewportPosition.x, viewportPosition.y) + screenCenterOffset;
            float pixelHeight = Mathf.Max(1f, targetCamera.pixelHeight);
            float aspect = Mathf.Max(0.0001f, targetCamera.pixelWidth / pixelHeight);

            Shader.SetGlobalVector(OcclusionCenterId, new Vector4(center.x, center.y, 0f, 0f));
            Shader.SetGlobalFloat(InnerRadiusId, innerRadius);
            Shader.SetGlobalFloat(OuterRadiusId, Mathf.Max(innerRadius, outerRadius));
            Shader.SetGlobalFloat(OcclusionAspectId, aspect);
        }

        private void ReleaseTrackedOccluders()
        {
            for (int i = 0; i < trackedOccluderCount; i++)
            {
                OccludableObject occluder = trackedOccluders[i];
                if (occluder != null)
                {
                    occluder.SetOccluded(false);
                    occluder.SetOcclusionAmount(0f);
                }

                trackedOccluders[i] = null;
            }

            trackedOccluderCount = 0;
            frameOccluderCount = 0;
        }

        private void RemoveTrackedAt(int index)
        {
            trackedOccluderCount--;
            trackedOccluders[index] = trackedOccluders[trackedOccluderCount];
            trackedOccluders[trackedOccluderCount] = null;
        }

        private static bool Contains(
            OccludableObject[] occluders,
            int count,
            OccludableObject candidate)
        {
            for (int i = 0; i < count; i++)
            {
                if (occluders[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDisable()
        {
            ReleaseTrackedOccluders();
        }

        private void OnValidate()
        {
            sphereRadius = Mathf.Max(0f, sphereRadius);
            innerRadius = Mathf.Max(0f, innerRadius);
            outerRadius = Mathf.Max(innerRadius, outerRadius);
            fadeInSpeed = Mathf.Max(0f, fadeInSpeed);
            fadeOutSpeed = Mathf.Max(0f, fadeOutSpeed);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebug || target == null || targetCamera == null)
            {
                return;
            }

            Vector3 castOrigin = targetCamera.transform.position;
            Vector3 castTarget = target.position;

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(castOrigin, castTarget);
            Gizmos.DrawWireSphere(castOrigin, sphereRadius);
            Gizmos.DrawWireSphere(castTarget, sphereRadius);
        }
    }
}
