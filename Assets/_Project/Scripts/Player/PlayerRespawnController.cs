using ShadowPath.Interaction;
using ShadowPath.Input;
using ShadowPath.Lighting;
using ShadowPath.Shadow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowPath.Player
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(Rigidbody))]
    public class PlayerRespawnController : MonoBehaviour
    {
        [SerializeField] private LightGameplayArea lightGameplayArea;

        [Header("Checkpoint Snapshot")]
        [SerializeField] private RotatableObject[] checkpointObjects = System.Array.Empty<RotatableObject>();
        [SerializeField] private ShadowSystem[] shadowSystems = System.Array.Empty<ShadowSystem>();
        [Tooltip("Optional fixed safe pose, sampled when the checkpoint is saved. Must be valid in the saved environment.")]
        [SerializeField] private Transform fixedSafeRespawnPoint;
        [SerializeField] private LayerMask respawnCollisionMask = ~0;
        [SerializeField, Min(0.01f)] private float supportProbeDistance = 0.15f;

        [Header("Checkpoint Spawn Validation")]
        [SerializeField, Min(0f)] private float maxGroundSnapDistance = 0.1f;
        [SerializeField, Min(0f)] private float spawnSurfaceClearance = 0.01f;
        [SerializeField, Range(0f, 89f)] private float maxWalkableSlopeAngle = 60f;

        [Header("Level Lower Bound")]
        [SerializeField] private bool useLevelLowerBound;
        [SerializeField] private Transform projectionPlane;
        [Tooltip("Respawn below this height relative to the Projection Plane origin along its Up axis.")]
        [SerializeField] private float minimumPlaneHeight = -10f;

        [Header("Runtime Debug")]
        [SerializeField] private bool runtimeDebug;

        private Rigidbody body;
        private PlayerController playerController;
        private PlayerInteractionController playerInteraction;
        private ShadowRideController shadowRide;
        private Vector3 checkpointPosition;
        private Quaternion checkpointRotation;
        private float initialZ;
        private bool warnedInvalidRespawn;
        private CheckpointSnapshot checkpointSnapshot;
        private CapsuleCollider capsule;
        private bool hasSafeFallback;
        private Vector3 safeFallbackPosition;
        private Quaternion safeFallbackRotation;
        private bool isRestoring;
        private bool recoveryHeld;
        private bool restorePlayerEnabled;
        private bool restoreBodyKinematic;

        private void Awake()
        {
            GameplayInput.TryInitialize(this);
            body = GetComponent<Rigidbody>();
            playerController = GetComponent<PlayerController>();
            capsule = GetComponent<CapsuleCollider>();
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

        private void Start()
        {
            // Default-order ShadowSystem/Light Start methods finish before this component.
            if (checkpointSnapshot == null) CaptureCheckpoint(checkpointPosition, checkpointRotation);
        }

        private void LateUpdate()
        {
            InputAction resetAction = GameplayInput.ResetToCheckpoint;
            bool resetPressed = resetAction?.WasPressedThisFrame() == true;
            // Physical buttons are diagnostic only. The action remains the sole reset trigger.
            if (!resetPressed && (!runtimeDebug || !RawResetWasPressed())) return;

            LogRuntime($"Reset Input Detected. Action pressed={resetPressed}, "
                + $"action={resetAction?.actionMap?.name}/{resetAction?.name}, enabled={resetAction?.enabled}.");
            if (!resetPressed)
            {
                string reason = resetAction == null
                    ? "Gameplay/ResetToCheckpoint was not found in the active project Input Actions."
                    : (!resetAction.enabled
                        ? "Gameplay/ResetToCheckpoint is disabled."
                        : "The physical press did not reach the action. Check binding masks, devices and Input System Update Mode.");
                LogRuntime($"Reset Blocked Reason. {reason}");
                return;
            }

            // Resolve tutorial and interaction input before accepting a normal-play reset.
            string blockedReason = GetResetBlockedReason();
            if (blockedReason != null)
            {
                LogRuntime($"Reset Blocked Reason. {blockedReason}");
                return;
            }

            Respawn();
        }

        private string GetResetBlockedReason()
        {
            string reason = GameplayInput.GetGameplayCommandBlockReason();
            if (reason != null) return reason;
            if (playerInteraction != null && playerInteraction.IsInteracting)
                return "Player interaction is Selecting or Rotating.";
            if (isRestoring) return "Checkpoint restoration is already running.";
            if (playerController == null || (!recoveryHeld && !playerController.isActiveAndEnabled))
                return "PlayerController is missing, disabled or inactive.";
            if (GameplayInput.Interact?.WasPressedThisFrame() == true)
                return "Interact was pressed in the same frame.";
            if (GameplayInput.Cancel?.WasPressedThisFrame() == true)
                return "Cancel was pressed in the same frame.";
            return null;
        }

        private static bool RawResetWasPressed()
        {
            if (Keyboard.current?.rKey.wasPressedThisFrame == true) return true;
            foreach (Gamepad gamepad in Gamepad.all)
                if (gamepad.selectButton.wasPressedThisFrame) return true;
            return false;
        }

        private void OnEnable()
        {
            LogRuntime($"Reset Listener Enabled on {name}. "
                + $"Action available={GameplayInput.ResetToCheckpoint != null}, "
                + $"action enabled={GameplayInput.ResetToCheckpoint?.enabled}, "
                + $"Input System Update Mode={InputSystem.settings.updateMode}.");
        }

        private void OnDisable() => LogRuntime("Reset Listener Disabled.");

        private void LogRuntime(string message)
        {
            if (runtimeDebug) Debug.Log($"[PlayerRespawnController] {message}", this);
        }

        private void FixedUpdate()
        {
            if (!useLevelLowerBound || projectionPlane == null) return;
            float height = Vector3.Dot(body.position - projectionPlane.position, projectionPlane.up);
            if (height < minimumPlaneHeight) Respawn();
        }

        public void SetCheckpoint(Transform respawnPoint)
        {
            TrySetManualCheckpoint(respawnPoint);
        }

        private bool TrySetManualCheckpoint(Transform respawnPoint)
        {
            if (respawnPoint == null || body == null) return false;
            Vector3 position = respawnPoint.position;
            if ((body.constraints & RigidbodyConstraints.FreezePositionZ) != 0) position.z = initialZ;
            Physics.SyncTransforms();
            if (!TryValidateSpawn(position, respawnPoint.rotation, lightGameplayArea, out string reason))
            {
                Debug.LogWarning($"Manual checkpoint was not saved. {reason} Previous checkpoint retained.", this);
                return false;
            }
            return CaptureCheckpoint(position, respawnPoint.rotation);
        }

        public bool TrySetCheckpointFromPlayer(Transform manualRespawnPoint = null)
        {
            // The captured pose belongs to the Player Root, just like the pose restored by Respawn.
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            Physics.SyncTransforms();
            if (TryResolvePlayerSpawn(position, rotation, out Vector3 safePosition, out string reason))
                return CaptureCheckpoint(safePosition, rotation);

            Debug.LogWarning($"PlayerPosition checkpoint was not saved. {reason} "
                + (manualRespawnPoint != null ? "Trying Manual Respawn Point." : "Previous checkpoint retained."), this);
            return manualRespawnPoint != null && TrySetManualCheckpoint(manualRespawnPoint);
        }

        private bool TryResolvePlayerSpawn(Vector3 position, Quaternion rotation, out Vector3 safePosition, out string reason)
        {
            safePosition = position;
            if (!TryGetSpawnUp(lightGameplayArea, out Vector3 up, out reason)) return false;
            if (body == null || capsule == null || !capsule.enabled || capsule.isTrigger)
            {
                reason = "Player requires an initialized Rigidbody and solid CapsuleCollider.";
                return false;
            }
            if ((body.constraints & RigidbodyConstraints.FreezePositionZ) != 0) position.z = initialZ;
            GetCapsuleAtPose(position, rotation, out Vector3 point1, out Vector3 point2, out float radius);
            float maximumSnap = Mathf.Max(0f, maxGroundSnapDistance);
            const float skin = 0.01f;
            float lift = maximumSnap + skin;
            float minimumUpDot = Mathf.Cos(Mathf.Clamp(maxWalkableSlopeAngle, 0f, 89f) * Mathf.Deg2Rad);
            RaycastHit[] hits = Physics.CapsuleCastAll(point1 + up * lift, point2 + up * lift, radius, -up,
                maximumSnap * 2f + skin, respawnCollisionMask, QueryTriggerInteraction.Ignore);
            float bestCorrection = float.PositiveInfinity;
            reason = "No walkable support was found within Max Ground Snap Distance.";
            foreach (RaycastHit hit in hits)
            {
                float upDot = Vector3.Dot(hit.normal, up);
                if (IsPlayerCollider(hit.collider) || hit.distance <= 0.0001f || upDot < minimumUpDot) continue;
                Vector3 supportVelocity = hit.rigidbody != null ? hit.rigidbody.GetPointVelocity(hit.point) : Vector3.zero;
                // Geometry-driven ShadowRide can carry a grounded body without a platform Rigidbody.
                GeneratedShadow supportShadow = hit.collider.GetComponentInParent<GeneratedShadow>();
                bool touchingRideSupport = supportShadow != null && supportShadow.HasValidGeometry
                    && shadowRide != null && shadowRide.IsAttached && supportShadow == shadowRide.AttachedShadow
                    && Mathf.Abs(lift - hit.distance) <= skin;
                if (!touchingRideSupport && Mathf.Abs(Vector3.Dot(body.linearVelocity - supportVelocity, hit.normal)) > 0.5f)
                {
                    reason = "Player is leaving or falling toward the support surface, rather than grounded.";
                    continue;
                }
                float correction = lift - hit.distance + Mathf.Max(0f, spawnSurfaceClearance) / upDot;
                if (Mathf.Abs(correction) > maximumSnap + 0.0001f || Mathf.Abs(correction) >= bestCorrection) continue;
                Vector3 candidate = position + up * correction;
                if (!TryValidateSpawn(candidate, rotation, lightGameplayArea, out string candidateReason))
                {
                    reason = candidateReason;
                    continue;
                }
                safePosition = candidate;
                bestCorrection = Mathf.Abs(correction);
            }
            return !float.IsPositiveInfinity(bestCorrection);
        }

        private bool CaptureCheckpoint(Vector3 position, Quaternion rotation)
        {
            if (isRestoring || recoveryHeld)
            {
                ReportRespawnFailure("Cannot replace the checkpoint during restoration or recovery hold.");
                return false;
            }
            if ((body.constraints & RigidbodyConstraints.FreezePositionZ) != 0) position.z = initialZ;
            if (!CheckpointSnapshot.TryCapture(position, rotation, lightGameplayArea, checkpointObjects, shadowSystems,
                out CheckpointSnapshot snapshot, out string reason))
            {
                ReportRespawnFailure($"Snapshot capture failed. {reason}");
                return false;
            }
            checkpointSnapshot = snapshot;
            checkpointPosition = position;
            checkpointRotation = rotation;
            hasSafeFallback = fixedSafeRespawnPoint != null;
            if (hasSafeFallback)
            {
                safeFallbackPosition = fixedSafeRespawnPoint.position;
                safeFallbackRotation = fixedSafeRespawnPoint.rotation;
                if ((body.constraints & RigidbodyConstraints.FreezePositionZ) != 0)
                    safeFallbackPosition.z = initialZ;
            }
            warnedInvalidRespawn = false;
            LogRuntime($"Checkpoint Snapshot Saved. Position={checkpointPosition}, objects={checkpointObjects?.Length ?? 0}.");
            return true;
        }

        public void Respawn()
        {
            LogRuntime($"Respawn Invoked. Checkpoint position={checkpointPosition}.");
            if (body == null || playerController == null)
            {
                ReportRespawnFailure("Rigidbody or PlayerController is missing; Awake must initialize this component.");
                return;
            }
            if (isRestoring) return;
            if (checkpointSnapshot == null)
            {
                ReportRespawnFailure("No checkpoint snapshot is available. Configure the environment before Play "
                    + "or complete a checkpoint after fixing its references.");
                return;
            }
            if (!checkpointSnapshot.TryCaptureCurrentEnvironment(body.position, body.rotation,
                out CheckpointSnapshot rollback, out string failureReason))
            {
                ReportRespawnFailure($"Cannot prepare environment rollback. {failureReason}");
                return;
            }

            isRestoring = true;
            if (!recoveryHeld)
            {
                if (playerInteraction != null) playerInteraction.ExitInteractionMode();
                restorePlayerEnabled = playerController.enabled;
                restoreBodyKinematic = body.isKinematic;
            }
            recoveryHeld = true;
            playerController.enabled = false;
            playerController.ResetTransientMovement();
            if (shadowRide != null) shadowRide.ResetForRespawn();
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            try
            {
                if (!checkpointSnapshot.TryRestoreEnvironment(out failureReason)
                    || !TryFindSafeSpawn(out Vector3 position, out Quaternion rotation, out failureReason))
                {
                    CancelRestore(rollback, failureReason);
                    return;
                }
                CompleteRestore(position, rotation);
                warnedInvalidRespawn = false;
                LogRuntime($"Respawn Completed. Snapshot and colliders restored, Player position={body.position}.");
            }
            catch (System.Exception exception)
            {
                CancelRestore(rollback, $"Restoration threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                isRestoring = false;
            }
        }

        private void CancelRestore(CheckpointSnapshot rollback, string reason)
        {
            if (rollback.TryRestoreEnvironment(out string rollbackReason))
            {
                CompleteRestore(rollback.PlayerPosition, rollback.PlayerRotation);
                ReportRespawnFailure($"{reason} Environment rolled back; Player was not sent to the checkpoint.");
            }
            else
            {
                // Keep ownership of the physical hold if even the original geometry cannot be rebuilt.
                ReportRespawnFailure($"{reason} Rollback failed: {rollbackReason}. "
                    + "Player remains held safely; fix the references/geometry and press Reset again.");
            }
        }

        private void CompleteRestore(Vector3 position, Quaternion rotation)
        {
            if (playerInteraction != null) playerInteraction.ResetForRespawn(position, rotation);
            playerController.ResetTransientMovement();
            if (shadowRide != null) shadowRide.ResetForRespawn();
            body.position = position;
            body.rotation = rotation;
            Physics.SyncTransforms();
            body.isKinematic = restoreBodyKinematic;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            playerController.enabled = restorePlayerEnabled;
            recoveryHeld = false;
            GameplayInput.BlockGameplayCommandsForFrame();
        }

        private bool TryFindSafeSpawn(out Vector3 position, out Quaternion rotation, out string reason)
        {
            position = checkpointSnapshot.PlayerPosition;
            rotation = checkpointSnapshot.PlayerRotation;
            if (TryValidateSpawn(position, rotation, checkpointSnapshot.Area, out reason)) return true;
            string checkpointReason = reason;
            if (hasSafeFallback && TryValidateSpawn(safeFallbackPosition, safeFallbackRotation, checkpointSnapshot.Area, out reason))
            {
                position = safeFallbackPosition;
                rotation = safeFallbackRotation;
                LogRuntime($"Checkpoint pose rejected ({checkpointReason}); using the configured fixed safe pose.");
                return true;
            }
            reason = $"Checkpoint pose rejected: {checkpointReason}. "
                + (hasSafeFallback ? $"Fixed safe pose rejected: {reason}" : "No fixed safe pose was configured.");
            return false;
        }

        private bool TryValidateSpawn(Vector3 position, Quaternion rotation, LightGameplayArea area, out string reason)
        {
            reason = null;
            Transform plane = area != null ? area.ProjectionPlane : null;
            if (plane == null || !area.TryGetGameplayCircle(out Vector2 center, out float radius)
                || !ProjectionPlaneCoordinates.TryWorldToPlane2D(position, plane.position, plane.right, plane.up, out Vector2 sample)
                || !CircleGameplayConstraint.Contains(sample.x, sample.y, center.x, center.y, radius))
            {
                reason = "Player pose is outside the saved Light's Gameplay Circle.";
                return false;
            }
            if (capsule == null || !capsule.enabled || capsule.isTrigger)
            {
                reason = "Player requires an enabled, solid CapsuleCollider.";
                return false;
            }

            GetCapsuleAtPose(position, rotation, out Vector3 point1, out Vector3 point2, out float capsuleRadius);
            Collider[] overlaps = Physics.OverlapCapsule(point1, point2, capsuleRadius,
                respawnCollisionMask, QueryTriggerInteraction.Ignore);
            foreach (Collider other in overlaps)
            {
                if (IsPlayerCollider(other)) continue;
                if (!Physics.ComputePenetration(capsule, position, rotation, other,
                    other.transform.position, other.transform.rotation, out _, out float distance)
                    || distance <= 0.005f) continue;
                reason = $"Player capsule overlaps {other.name}.";
                return false;
            }

            if (!TryGetSpawnUp(area, out Vector3 up, out reason)) return false;
            const float skin = 0.01f;
            RaycastHit[] hits = Physics.CapsuleCastAll(point1 + up * skin, point2 + up * skin, capsuleRadius,
                -up, supportProbeDistance + skin, respawnCollisionMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
                if (!IsPlayerCollider(hit.collider) && hit.distance > 0.0001f
                    && Vector3.Dot(hit.normal, up) >= Mathf.Cos(Mathf.Clamp(maxWalkableSlopeAngle, 0f, 89f) * Mathf.Deg2Rad)) return true;
            reason = "Player capsule has no valid support beneath it in the restored environment.";
            return false;
        }

        private void GetCapsuleAtPose(Vector3 position, Quaternion rotation, out Vector3 point1, out Vector3 point2, out float radius)
        {
            playerController.GetCapsuleCastPoints(out point1, out point2, out radius);
            Quaternion delta = rotation * Quaternion.Inverse(capsule.transform.rotation);
            point1 = position + delta * (point1 - capsule.transform.position);
            point2 = position + delta * (point2 - capsule.transform.position);
        }

        private static bool TryGetSpawnUp(LightGameplayArea area, out Vector3 up, out string reason)
        {
            up = area != null && area.ProjectionPlane != null ? area.ProjectionPlaneUp : Vector3.zero;
            up.z = 0f;
            reason = null;
            if (up.sqrMagnitude < 0.000001f)
            {
                reason = "A Projection Plane with a usable Up axis in the Player's world XY plane is required.";
                return false;
            }
            up.Normalize();
            return true;
        }

        private bool IsPlayerCollider(Collider other) =>
            other.attachedRigidbody == body || other.transform.IsChildOf(transform);

        private void ReportRespawnFailure(string reason)
        {
            LogRuntime($"Respawn Failed Reason. {reason}");
            if (!warnedInvalidRespawn) Debug.LogError($"Respawn failed. {reason}", this);
            warnedInvalidRespawn = true;
        }
    }
}
