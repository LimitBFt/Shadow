using ShadowPath.Player;
using ShadowPath.Stars;
using UnityEngine;

namespace ShadowPath.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class GoalTrigger : MonoBehaviour
    {
        [SerializeField] private GoalCompleteUI goalCompleteUI;
        [SerializeField, Tooltip("Uses the unique manager in this scene when left empty.")]
        private StarCollectionManager starCollectionManager;
        [SerializeField] private StarHUD starHUD;
        [Header("Runtime Debug")]
        [SerializeField] private bool completed;

        public bool Completed => completed;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (completed || !isActiveAndEnabled || other == null
                || other.GetComponentInParent<PlayerController>() == null) return;
            if (starCollectionManager == null)
                starCollectionManager = StarCollectionManager.FindUniqueInScene(gameObject.scene);
            if (starCollectionManager != null && !starCollectionManager.HasRequiredLargeStars)
            {
                if (starHUD == null || !starHUD.ShowCollectMoreStars())
                    Debug.LogWarning("COLLECT MORE STARS. Assign a Star HUD with a Goal Hint Label to show this in-game.", this);
                return;
            }
            if (goalCompleteUI == null)
            {
                Debug.LogWarning("Assign the HUD Goal Complete UI to this Goal Trigger.", this);
                return;
            }
            if (goalCompleteUI.ShowCompletion())
            {
                completed = true;
                if (starHUD != null) starHUD.HideGoalHint();
            }
        }
    }
}
