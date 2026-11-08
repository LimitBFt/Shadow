using ShadowPath.Interaction;
using ShadowPath.Lighting;
using UnityEngine;

namespace ShadowPath.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(Rigidbody))]
    public class PlayerRespawnController : MonoBehaviour
    {
        [SerializeField] private LightGameplayArea lightGameplayArea;

        [Header("Level Lower Bound")]
        [SerializeField] private bool useLevelLowerBound;
        [SerializeField] private Transform projectionPlane;
        [Tooltip("Respawn below this height relative to the Projection Plane origin along its Up axis.")]
        [SerializeField] private float minimumPlaneHeight = -10f;

        private Rigidbody body;
        private PlayerController playerController;
        private PlayerInteractionController playerInteraction;
        private ShadowRideController shadowRide;
        private Vector3 checkpointPosition;
        private Quaternion checkpointRotation;
        private float initialZ;
        private bool warnedInvalidRespawn;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            playerController = GetComponent<PlayerController>();
            playerInteraction = GetComponent<PlayerInteractionController>();
            shadowRide = GetComponent<ShadowRideController>();
            if (lightGameplayArea == null) lightGameplayArea = playerController.GameplayArea;
            if (projectionPlane == null && lightGameplayArea != null)
                projectionPlane = lightGameplayArea.ProjectionPlane;
            checkpointPosition = body.position;
            checkpointRotation = body.rotation;
            initialZ = checkpointPosition.z;
            if (useLevelLowerBound && projectionPlane == null)
                Debug.LogWarning("Assign a Projection Plane to enable the level lower bound.", this);
        }

        private void FixedUpdate()
        {
            if (!useLevelLowerBound || projectionPlane == null) return;
            float height = Vector3.Dot(body.position - projectionPlane.position, projectionPlane.up);
            if (height < minimumPlaneHeight) Respawn();
        }

        public void SetCheckpoint(Transform respawnPoint)
        {
            if (respawnPoint == null) return;
            checkpointPosition = respawnPoint.position;
            checkpointRotation = respawnPoint.rotation;
            warnedInvalidRespawn = false;
        }

        public void Respawn()
        {
            Vector3 position = checkpointPosition;
            if ((body.constraints & RigidbodyConstraints.FreezePositionZ) != 0)
                position.z = initialZ;

            // Restore the pair before teleporting, using the existing circle/level bounds math.
            // Invalid checkpoint layouts leave both objects untouched.
            if (lightGameplayArea != null)
            {
                LightSourceController lightController = lightGameplayArea.LightController;
                if (!lightGameplayArea.TryGetGameplayCircle(out _, out float radius)
                    || lightController == null || !lightController.TryRestoreForRespawn(position, radius))
                {
                    if (!warnedInvalidRespawn)
                        Debug.LogError("Respawn requires valid Gameplay Circle/Light references and a checkpoint "
                            + "whose circle intersects the Level Light Bounds.", this);
                    warnedInvalidRespawn = true;
                    return;
                }
            }

            if (playerInteraction != null) playerInteraction.ResetForRespawn(position, checkpointRotation);
            playerController.ResetTransientMovement();
            if (shadowRide != null) shadowRide.ResetForRespawn();
            body.position = position;
            body.rotation = checkpointRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            warnedInvalidRespawn = false;
        }
    }
}
