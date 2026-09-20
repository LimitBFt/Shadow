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

        public IReadOnlyList<RotatableObject> ControlledObjects => controlledObjects;

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
