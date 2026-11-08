using UnityEngine;

namespace ShadowPath.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class DeathZone : MonoBehaviour
    {
        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null && player.TryGetComponent(out PlayerRespawnController respawn))
                respawn.Respawn();
        }
    }
}
