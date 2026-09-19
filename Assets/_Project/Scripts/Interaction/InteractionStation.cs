using System.Collections.Generic;
using ShadowPath.Player;
using UnityEngine;

namespace ShadowPath.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class InteractionStation : MonoBehaviour
    {
        [SerializeField] private List<RotatableObject> controlledObjects = new List<RotatableObject>();

        public IReadOnlyList<RotatableObject> ControlledObjects => controlledObjects;

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
    }
}

