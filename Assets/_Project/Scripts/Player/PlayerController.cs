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
        [SerializeField, Min(1f)] private float fallGravityMultiplier = 2.2f;
        [SerializeField, Min(1f)] private float lowJumpGravityMultiplier = 2.5f;
        [SerializeField, Min(0.01f)] private float maxFallSpeed = 18f;
        [SerializeField] private float groundCheckDistance = 0.08f;
        [SerializeField] private LightGameplayArea lightGameplayArea;
        [SerializeField] private Vector3 pendingExternalDisplacement;
        [SerializeField] private Vector3 consumedExternalDisplacement;

        private Rigidbody body;
        private CapsuleCollider capsuleCollider;
        private float moveInput;
        private bool jumpRequested;
        private bool jumpHeld;

        internal LightGameplayArea GameplayArea => lightGameplayArea;

        internal void ResetTransientMovement()
        {
            moveInput = 0f;
            jumpRequested = jumpHeld = false;
            pendingExternalDisplacement = consumedExternalDisplacement = Vector3.zero;
        }

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
            jumpHeld = GameplayInput.Jump?.IsPressed() ?? false;

            if (ReadJumpPressed())
            {
                jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            bool grounded = IsGrounded();
            // The Rigidbody moves in world XY, so use the in-plane part of ProjectionPlane Up.
            Vector3 planeUp = lightGameplayArea != null ? lightGameplayArea.ProjectionPlaneUp : Vector3.up;
            planeUp.z = 0f;
            planeUp = planeUp.sqrMagnitude > 0.000001f ? planeUp.normalized : Vector3.up;

            Vector3 velocity = body.linearVelocity;
            velocity.x = moveInput * moveSpeed;
            velocity.z = 0f;
            bool jumping = jumpRequested && grounded;
            float upSpeed = Vector3.Dot(velocity, planeUp);
            if (jumping)
            {
                velocity += planeUp * (jumpForce - upSpeed);
                upSpeed = jumpForce;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 gravityStep = body.useGravity ? Physics.gravity * dt : Vector3.zero;
            gravityStep.z = 0f;
            velocity += gravityStep;

            if (!grounded || jumping)
            {
                float gravityUpStep = Vector3.Dot(gravityStep, planeUp);
                float nextUpSpeed = upSpeed + gravityUpStep;
                float gravityMultiplier = nextUpSpeed <= 0f
                    ? fallGravityMultiplier
                    : (jumpHeld ? 1f : lowJumpGravityMultiplier);
                float adjustedUpSpeed = Mathf.Max(-maxFallSpeed,
                    upSpeed + gravityUpStep * gravityMultiplier);
                velocity += planeUp * (adjustedUpSpeed - nextUpSpeed);
            }

            Vector3 externalDisplacement = ConsumeExternalDisplacement() - body.position;
            if (lightGameplayArea != null)
            {
                lightGameplayArea.ConstrainPlayerMovement(body.position, externalDisplacement,
                    velocity, dt, out externalDisplacement, out velocity);
            }

            // Unity still applies its built-in gravity step after FixedUpdate.
            velocity -= gravityStep;
            velocity.z = 0f;
            body.linearVelocity = velocity;
            if (externalDisplacement.sqrMagnitude > 0.0000000001f)
            {
                body.MovePosition(body.position + externalDisplacement);
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
