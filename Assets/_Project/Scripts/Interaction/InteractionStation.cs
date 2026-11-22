using System.Collections.Generic;
using ShadowPath.Player;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ShadowPath.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class InteractionStation : MonoBehaviour
    {
        [SerializeField] private Transform controlAreaAnchor;
        [SerializeField] private Vector3 controlAreaSize = new Vector3(10f, 6f, 2f);
        [SerializeField] private List<RotatableObject> controlledObjects = new List<RotatableObject>();

        [Header("Interaction Camera")]
        [SerializeField] private bool useInteractionCamera = true;
        [SerializeField] private Transform cameraFocusPoint;
        [SerializeField] private float interactionCameraNormalOffset = -18f;
        [SerializeField, Min(0.01f)] private float cameraTransitionSmoothTime = 0.6f;

        [Header("Auto Frame")]
        [SerializeField] private bool autoFrameControlledObjects;
        [Tooltip("Viewport inset on each edge, as a fraction of the full viewport.")]
        [SerializeField, Range(0f, 0.45f)] private float framePadding = 0.15f;
        [SerializeField, Min(0.01f)] private float autoFrameSmoothTime = 0.6f;
        [Tooltip("Auto Frame only. Negative offsets extend along the negative ProjectionPlane normal.")]
        [SerializeField] private float minInteractionNormalOffset = -20f;
        [SerializeField] private float maxInteractionNormalOffset = -10f;

        private readonly List<Renderer> cameraRenderers = new List<Renderer>(16);
        private readonly List<Collider> cameraColliders = new List<Collider>(8);

        public IReadOnlyList<RotatableObject> ControlledObjects => controlledObjects;
        public bool UseInteractionCamera => useInteractionCamera;
        public float InteractionCameraNormalOffset => interactionCameraNormalOffset;
        public float CameraTransitionSmoothTime => Mathf.Max(0.01f, cameraTransitionSmoothTime);
        public bool AutoFrameControlledObjects => autoFrameControlledObjects;
        public float FramePadding => Mathf.Clamp(framePadding, 0f, 0.45f);
        public float AutoFrameSmoothTime => Mathf.Max(0.01f, autoFrameSmoothTime);
        public Vector2 InteractionNormalOffsetRange => new Vector2(minInteractionNormalOffset, maxInteractionNormalOffset);

        public void CollectCameraFramePoints(List<Vector3> points)
        {
            points.Clear();
            if (controlledObjects == null) return;
            foreach (RotatableObject controlledObject in controlledObjects)
            {
                if (controlledObject == null || !controlledObject.isActiveAndEnabled) continue;
                int startCount = points.Count;
                cameraRenderers.Clear();
                controlledObject.GetComponentsInChildren(false, cameraRenderers);
                foreach (Renderer objectRenderer in cameraRenderers)
                    if (objectRenderer != null && objectRenderer.enabled) AddBoundsCorners(objectRenderer.bounds, points);
                if (points.Count != startCount) continue;
                cameraColliders.Clear();
                controlledObject.GetComponentsInChildren(false, cameraColliders);
                foreach (Collider objectCollider in cameraColliders)
                    if (objectCollider != null && objectCollider.enabled)
                        AddBoundsCorners(objectCollider.bounds, points);
            }
        }

        private static void AddBoundsCorners(Bounds bounds, List<Vector3> points)
        {
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            for (int corner = 0; corner < 8; corner++)
                points.Add(new Vector3((corner & 1) == 0 ? minimum.x : maximum.x,
                    (corner & 2) == 0 ? minimum.y : maximum.y,
                    (corner & 4) == 0 ? minimum.z : maximum.z));
        }

        public Vector3 GetCameraFocusPosition()
        {
            if (cameraFocusPoint != null) return cameraFocusPoint.position;
            bool hasBounds = false;
            Bounds combinedBounds = default;
            if (controlledObjects != null)
            {
                foreach (RotatableObject controlledObject in controlledObjects)
                {
                    if (controlledObject == null || !controlledObject.isActiveAndEnabled) continue;
                    cameraRenderers.Clear();
                    controlledObject.GetComponentsInChildren(false, cameraRenderers);
                    bool hasRenderer = false;
                    foreach (Renderer objectRenderer in cameraRenderers)
                    {
                        if (objectRenderer == null || !objectRenderer.enabled) continue;
                        Bounds bounds = objectRenderer.bounds;
                        if (hasBounds) combinedBounds.Encapsulate(bounds);
                        else combinedBounds = bounds;
                        hasBounds = true;
                        hasRenderer = true;
                    }
                    if (hasRenderer) continue;
                    if (hasBounds) combinedBounds.Encapsulate(controlledObject.transform.position);
                    else combinedBounds = new Bounds(controlledObject.transform.position, Vector3.zero);
                    hasBounds = true;
                }
            }
            return hasBounds ? combinedBounds.center : transform.position;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Collect Controlled Objects")]
        private void AutoCollectControlledObjects()
        {
            Undo.RecordObject(this, "Auto Collect Controlled Objects");
            controlledObjects.Clear();

            RotatableObject[] rotatableObjects = Object.FindObjectsByType<RotatableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < rotatableObjects.Length; i++)
            {
                RotatableObject rotatableObject = rotatableObjects[i];
                if (rotatableObject == null || !rotatableObject.isActiveAndEnabled)
                {
                    continue;
                }

                if (!IsWorldPointInsideControlArea(rotatableObject.transform.position))
                {
                    continue;
                }

                if (controlledObjects.Contains(rotatableObject))
                {
                    continue;
                }

                controlledObjects.Add(rotatableObject);
            }

            controlledObjects.Sort(CompareRotatableObjectsByWorldXY);
            MarkStationDirty();
        }

        [ContextMenu("Clear Controlled Objects")]
        private void ClearControlledObjects()
        {
            Undo.RecordObject(this, "Clear Controlled Objects");
            controlledObjects.Clear();
            MarkStationDirty();
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Transform anchor = GetControlAreaAnchor();
            Vector3 size = GetPositiveControlAreaSize();

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            Gizmos.matrix = Matrix4x4.TRS(anchor.position, anchor.rotation, anchor.lossyScale);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, size);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetPlayerInteractionController(other, out PlayerInteractionController playerInteractionController))
            {
                return;
            }

            playerInteractionController.EnterStationRange(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetPlayerInteractionController(other, out PlayerInteractionController playerInteractionController))
            {
                return;
            }

            playerInteractionController.ExitStationRange(this);
        }

        private static bool TryGetPlayerInteractionController(
            Collider other,
            out PlayerInteractionController playerInteractionController)
        {
            playerInteractionController = null;

            if (other == null)
            {
                return false;
            }

            if (other.GetComponentInParent<PlayerController>() == null)
            {
                return false;
            }

            playerInteractionController = other.GetComponentInParent<PlayerInteractionController>();
            return playerInteractionController != null;
        }

        private Transform GetControlAreaAnchor()
        {
            return controlAreaAnchor != null ? controlAreaAnchor : transform;
        }

        private Vector3 GetPositiveControlAreaSize()
        {
            return new Vector3(
                Mathf.Abs(controlAreaSize.x),
                Mathf.Abs(controlAreaSize.y),
                Mathf.Abs(controlAreaSize.z));
        }

        private bool IsWorldPointInsideControlArea(Vector3 worldPoint)
        {
            Transform anchor = GetControlAreaAnchor();
            Vector3 localPoint = anchor.InverseTransformPoint(worldPoint);
            Vector3 halfSize = GetPositiveControlAreaSize() * 0.5f;

            return Mathf.Abs(localPoint.x) <= halfSize.x
                && Mathf.Abs(localPoint.y) <= halfSize.y
                && Mathf.Abs(localPoint.z) <= halfSize.z;
        }

#if UNITY_EDITOR
        private static int CompareRotatableObjectsByWorldXY(RotatableObject a, RotatableObject b)
        {
            if (a == b)
            {
                return 0;
            }

            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            Vector3 aPosition = a.transform.position;
            Vector3 bPosition = b.transform.position;
            int xComparison = aPosition.x.CompareTo(bPosition.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            int yComparison = aPosition.y.CompareTo(bPosition.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return string.CompareOrdinal(a.name, b.name);
        }

        private void MarkStationDirty()
        {
            EditorUtility.SetDirty(this);
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}
