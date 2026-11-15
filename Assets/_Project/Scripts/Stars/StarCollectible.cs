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
            if (!IsCollected) this.locked = locked;
        }

        private void OnTriggerEnter(Collider other)
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
            if (collectedVisual != null) collectedVisual.SetActive(true);
            pickupFeedback?.Invoke();
        }
    }
}
