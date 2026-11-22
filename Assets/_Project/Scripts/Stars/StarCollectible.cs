using ShadowPath.Player;
using UnityEngine;
using UnityEngine.Events;

namespace ShadowPath.Stars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public class StarCollectible : MonoBehaviour
    {
        public enum StarType { Small, Large }

        [SerializeField] private StarType starType;
        [SerializeField, Min(0)] private int scoreValue = 100;
        [SerializeField] private bool locked;
        [SerializeField, Tooltip("Uses the unique manager in this scene when left empty.")]
        private StarCollectionManager collectionManager;
        [SerializeField, Tooltip("A SpriteRenderer or MeshRenderer on this object or a child. Defaults to the renderer on this object.")]
        private Renderer starVisual;
        [SerializeField, Tooltip("Optional child GameObject shown after pickup.")]
        private GameObject collectedVisual;
        [SerializeField] private UnityEvent pickupFeedback = new UnityEvent();

        private SphereCollider pickupTrigger;
        private GameObject lockedVisual;
        private GameObject unlockedVisual;
        private bool collectWhileOverlapping;

        public StarType Type => starType;
        public int ScoreValue => Mathf.Max(0, scoreValue);
        public bool IsCollected { get; private set; }
        public bool IsLocked => locked;

        private void Reset() => GetComponent<SphereCollider>().isTrigger = true;

        private void Awake()
        {
            pickupTrigger = GetComponent<SphereCollider>();
            pickupTrigger.isTrigger = true;

            if (starVisual == null || (starVisual.gameObject != gameObject
                && !starVisual.transform.IsChildOf(transform)))
            {
                starVisual = GetComponent<Renderer>();
            }
            if (collectedVisual != null && (collectedVisual == gameObject
                || !collectedVisual.transform.IsChildOf(transform)))
            {
                collectedVisual = null;
            }
            if (collectedVisual != null) collectedVisual.SetActive(false);
        }

        public void SetLocked(bool locked)
        {
            if (IsCollected || this.locked == locked) return;
            this.locked = locked;
            collectWhileOverlapping = !locked;
            RefreshAlignmentVisuals();
        }

        internal bool TrySetAlignmentVisuals(GameObject lockedVisual, GameObject unlockedVisual)
        {
            if (!IsIndependentVisualChild(lockedVisual) || !IsIndependentVisualChild(unlockedVisual)
                || VisualsOverlap(lockedVisual, unlockedVisual)) return false;

            this.lockedVisual = lockedVisual;
            this.unlockedVisual = unlockedVisual;
            RefreshAlignmentVisuals();
            return true;
        }

        private bool IsIndependentVisualChild(GameObject visual) =>
            visual == null || (visual != gameObject && visual.transform.IsChildOf(transform)
                && !VisualsOverlap(visual, collectedVisual));

        private static bool VisualsOverlap(GameObject first, GameObject second) =>
            first != null && second != null
            && (first.transform.IsChildOf(second.transform) || second.transform.IsChildOf(first.transform));

        private void RefreshAlignmentVisuals()
        {
            if (lockedVisual != null) lockedVisual.SetActive(!IsCollected && IsLocked);
            if (unlockedVisual != null) unlockedVisual.SetActive(!IsCollected && !IsLocked);
        }

        private void OnTriggerEnter(Collider other) => TryPickup(other);

        private void OnTriggerStay(Collider other)
        {
            if (collectWhileOverlapping) TryPickup(other);
        }

        private void TryPickup(Collider other)
        {
            if (!isActiveAndEnabled || IsLocked || IsCollected) return;
            if (!(other is CapsuleCollider) || other.isTrigger
                || !other.TryGetComponent<PlayerController>(out _)) return;

            if (collectionManager == null)
                collectionManager = StarCollectionManager.FindUniqueInScene(gameObject.scene);
            if (collectionManager == null || !collectionManager.TryRecordPickup(this)) return;

            IsCollected = true;
            pickupTrigger.enabled = false;
            if (starVisual != null) starVisual.enabled = false;
            RefreshAlignmentVisuals();
            if (collectedVisual != null) collectedVisual.SetActive(true);
            pickupFeedback?.Invoke();
        }
    }
}
