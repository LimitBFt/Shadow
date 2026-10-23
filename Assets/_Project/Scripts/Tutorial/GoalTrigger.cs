using ShadowPath.Player;
using UnityEngine;

namespace ShadowPath.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class GoalTrigger : MonoBehaviour
    {
        [SerializeField] private GoalCompleteUI goalCompleteUI;
        [Header("Runtime Debug")]
        [SerializeField] private bool completed;

        public bool Completed => completed;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (completed || !isActiveAndEnabled || other == null
                || other.GetComponentInParent<PlayerController>() == null) return;
            if (goalCompleteUI == null)
            {
                Debug.LogWarning("Assign the HUD Goal Complete UI to this Goal Trigger.", this);
                return;
            }
            if (goalCompleteUI.ShowCompletion()) completed = true;
        }
    }
}
