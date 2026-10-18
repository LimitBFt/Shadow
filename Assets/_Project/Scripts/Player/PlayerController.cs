using ShadowPath.Input;
using ShadowPath.Lighting;
using UnityEngine;

namespace ShadowPath.Player
{
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 7f;
        [SerializeField] private float groundCheckDistance = 0.08f;
        [SerializeField] private LightGameplayArea lightGameplayArea;
        [SerializeField] private Vector3 pendingExternalDisplacement;
        [SerializeField] private Vector3 consumedExternalDisplacement;

        private Rigidbody body;
        private CapsuleCollider capsuleCollider;
        private float moveInput;
        private bool jumpRequested;

        private void Awake()
        {
            GameplayInput.TryInitialize(this);
            body = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            body.constraints |= RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ;
        }

        private void Update()
        {
            moveInput = ReadMoveInput();

            if (ReadJumpPressed())
            {
                jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            bool grounded = IsGrounded();
            Vector3 velocity = body.linearVelocity;
            velocity.x = moveInput * moveSpeed;
            velocity.z = 0f;
            body.linearVelocity = velocity;

            if (jumpRequested && grounded)
            {
                Vector3 jumpVelocity = body.linearVelocity;
                jumpVelocity.y = Mathf.Max(0f, jumpVelocity.y);
                body.linearVelocity = jumpVelocity;
                body.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            }

            velocity = body.linearVelocity;
            Vector3 targetPosition = ConsumeExternalDisplacement();
            bool shouldMovePosition = targetPosition != body.position;
            if (lightGameplayArea != null)
            {
                Vector3 constrainedPosition = lightGameplayArea.ConstrainPlayerWorldPosition(
                    body.position,
                    targetPosition);
                shouldMovePosition |= constrainedPosition != body.position;
                targetPosition = constrainedPosition;
                velocity = lightGameplayArea.ConstrainPlayerVelocity(
                    targetPosition,
                    velocity,
                    Time.fixedDeltaTime);
            }

            velocity.z = 0f;
            body.linearVelocity = velocity;
            if (shouldMovePosition)
            {
                body.MovePosition(targetPosition);
            }

            jumpRequested = false;
        }

        public void AddExternalDisplacement(Vector3 displacement)
        {
            if (!IsFinite(displacement))
            {
                return;
            }

            displacement.z = 0f;
            pendingExternalDisplacement += displacement;
        }

        private Vector3 ConsumeExternalDisplacement()
        {
            consumedExternalDisplacement = pendingExternalDisplacement;
            pendingExternalDisplacement = Vector3.zero;
            return body.position + consumedExternalDisplacement;
        }

        private void OnDisable()
        {
            pendingExternalDisplacement = Vector3.zero;
            consumedExternalDisplacement = Vector3.zero;
        }

        private bool IsGrounded()
        {
            GetCapsuleCastPoints(out Vector3 point1, out Vector3 point2, out float radius);
            return Physics.CapsuleCast(
                point1,
                point2,
                radius,
                Vector3.down,
                groundCheckDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);
        }

        private void GetCapsuleCastPoints(out Vector3 point1, out Vector3 point2, out float radius)
        {
            Transform capsuleTransform = capsuleCollider.transform;
            Vector3 scale = capsuleTransform.lossyScale;
            Vector3 axis = GetCapsuleAxis(capsuleCollider.direction);
            Vector3 worldAxis = capsuleTransform.TransformDirection(axis).normalized;
            Vector3 worldCenter = capsuleTransform.TransformPoint(capsuleCollider.center);
            float axisScale = GetAxisScale(scale, capsuleCollider.direction);
            float radiusScale = GetMaxPerpendicularScale(scale, capsuleCollider.direction);
            radius = Mathf.Max(0.001f, capsuleCollider.radius * radiusScale);
            float height = Mathf.Max(radius * 2f, capsuleCollider.height * axisScale);
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);

            point1 = worldCenter + worldAxis * halfSegment;
            point2 = worldCenter - worldAxis * halfSegment;
        }

        private static Vector3 GetCapsuleAxis(int direction)
        {
            if (direction == 0)
            {
                return Vector3.right;
            }

            if (direction == 2)
            {
                return Vector3.forward;
            }

            return Vector3.up;
        }

        private static float GetAxisScale(Vector3 scale, int direction)
        {
            if (direction == 0)
            {
                return Mathf.Abs(scale.x);
            }

            if (direction == 2)
            {
                return Mathf.Abs(scale.z);
            }

            return Mathf.Abs(scale.y);
        }

        private static float GetMaxPerpendicularScale(Vector3 scale, int direction)
        {
            if (direction == 0)
            {
                return Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            }

            if (direction == 2)
            {
                return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }

            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        private static float ReadMoveInput()
        {
            return GameplayInput.Move == null
                ? 0f
                : Mathf.Clamp(GameplayInput.Move.ReadValue<Vector2>().x, -1f, 1f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool ReadJumpPressed()
        {
            return GameplayInput.Jump != null && GameplayInput.Jump.WasPressedThisFrame();
        }
    }
}
