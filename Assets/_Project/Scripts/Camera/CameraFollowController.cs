using UnityEngine;

namespace ShadowPath.Camera
{
    public class CameraFollowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform projectionPlane;

        [Header("Position In Projection Plane Space")]
        [SerializeField] private float planeRightOffset;
        [SerializeField] private float planeUpOffset = 3f;
        [SerializeField] private float planeNormalOffset = 8f;

        [Header("Follow Smoothing")]
        [SerializeField, Min(0f)] private float horizontalSmoothTime = 0.18f;
        [SerializeField, Min(0f)] private float verticalSmoothTime = 0.4f;

        [Header("Look Ahead")]
        [SerializeField, Min(0f)] private float lookAheadDistance = 1f;
        [SerializeField, Min(0f)] private float lookAheadSmoothTime = 0.2f;
        [SerializeField, Min(0f)] private float lookAheadVelocityThreshold = 0.05f;

        [Header("Aim")]
        [SerializeField] private float aimRightOffset;
        [SerializeField] private float aimUpOffset = 1f;
        [SerializeField, Min(0f)] private float rotationSmoothTime = 0.15f;

        private float rightVelocity;
        private float upVelocity;
        private float normalVelocity;
        private float lookAheadVelocity;
        private float currentLookAhead;
        private Vector3 previousTargetPosition;
        private bool initialized;

        private void OnEnable()
        {
            initialized = false;
        }

        private void Start()
        {
            TrySnapToTarget();
        }

        private void LateUpdate()
        {
            if (!HasValidReferences())
            {
                initialized = false;
                return;
            }

            if (!initialized)
            {
                TrySnapToTarget();
                return;
            }

            Vector3 planeRight = projectionPlane.right;
            Vector3 planeUp = projectionPlane.up;
            Vector3 planeNormal = projectionPlane.forward;

            UpdateLookAhead(planeRight);

            Vector3 desiredPosition = CalculateDesiredPosition(
                planeRight,
                planeUp,
                planeNormal,
                currentLookAhead);
            transform.position = SmoothPosition(
                transform.position,
                desiredPosition,
                planeRight,
                planeUp,
                planeNormal);

            Vector3 aimPoint = CalculateAimPoint(planeRight, planeUp, currentLookAhead);
            Quaternion desiredRotation = CalculateDesiredRotation(transform.position, aimPoint, planeUp);
            transform.rotation = SmoothRotation(transform.rotation, desiredRotation);

            previousTargetPosition = target.position;
        }

        private void TrySnapToTarget()
        {
            if (!HasValidReferences())
            {
                return;
            }

            Vector3 planeRight = projectionPlane.right;
            Vector3 planeUp = projectionPlane.up;
            Vector3 planeNormal = projectionPlane.forward;

            currentLookAhead = 0f;
            lookAheadVelocity = 0f;
            rightVelocity = 0f;
            upVelocity = 0f;
            normalVelocity = 0f;

            transform.position = CalculateDesiredPosition(planeRight, planeUp, planeNormal, 0f);
            Vector3 aimPoint = CalculateAimPoint(planeRight, planeUp, 0f);
            transform.rotation = CalculateDesiredRotation(transform.position, aimPoint, planeUp);

            previousTargetPosition = target.position;
            initialized = true;
        }

        private void UpdateLookAhead(Vector3 planeRight)
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 targetDelta = target.position - previousTargetPosition;
            float horizontalSpeed = Vector3.Dot(targetDelta, planeRight) / deltaTime;
            float desiredLookAhead = Mathf.Abs(horizontalSpeed) > lookAheadVelocityThreshold
                ? Mathf.Sign(horizontalSpeed) * lookAheadDistance
                : 0f;

            currentLookAhead = Mathf.SmoothDamp(
                currentLookAhead,
                desiredLookAhead,
                ref lookAheadVelocity,
                lookAheadSmoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private Vector3 SmoothPosition(
            Vector3 currentPosition,
            Vector3 desiredPosition,
            Vector3 planeRight,
            Vector3 planeUp,
            Vector3 planeNormal)
        {
            float deltaTime = Time.deltaTime;
            float currentRight = Vector3.Dot(currentPosition, planeRight);
            float currentUp = Vector3.Dot(currentPosition, planeUp);
            float currentNormal = Vector3.Dot(currentPosition, planeNormal);
            float desiredRight = Vector3.Dot(desiredPosition, planeRight);
            float desiredUp = Vector3.Dot(desiredPosition, planeUp);
            float desiredNormal = Vector3.Dot(desiredPosition, planeNormal);

            float smoothedRight = Mathf.SmoothDamp(
                currentRight,
                desiredRight,
                ref rightVelocity,
                horizontalSmoothTime,
                Mathf.Infinity,
                deltaTime);
            float smoothedUp = Mathf.SmoothDamp(
                currentUp,
                desiredUp,
                ref upVelocity,
                verticalSmoothTime,
                Mathf.Infinity,
                deltaTime);
            float smoothedNormal = Mathf.SmoothDamp(
                currentNormal,
                desiredNormal,
                ref normalVelocity,
                horizontalSmoothTime,
                Mathf.Infinity,
                deltaTime);

            return planeRight * smoothedRight
                + planeUp * smoothedUp
                + planeNormal * smoothedNormal;
        }

        private Quaternion SmoothRotation(Quaternion currentRotation, Quaternion desiredRotation)
        {
            if (rotationSmoothTime <= 0f)
            {
                return desiredRotation;
            }

            float interpolation = 1f - Mathf.Exp(-Time.deltaTime / rotationSmoothTime);
            return Quaternion.Slerp(currentRotation, desiredRotation, interpolation);
        }

        private Vector3 CalculateDesiredPosition(
            Vector3 planeRight,
            Vector3 planeUp,
            Vector3 planeNormal,
            float lookAhead)
        {
            return target.position
                + planeRight * (planeRightOffset + lookAhead)
                + planeUp * planeUpOffset
                + planeNormal * planeNormalOffset;
        }

        private Vector3 CalculateAimPoint(Vector3 planeRight, Vector3 planeUp, float lookAhead)
        {
            return target.position
                + planeRight * (aimRightOffset + lookAhead)
                + planeUp * aimUpOffset;
        }

        private Quaternion CalculateDesiredRotation(
            Vector3 cameraPosition,
            Vector3 aimPoint,
            Vector3 planeUp)
        {
            Vector3 lookDirection = aimPoint - cameraPosition;
            if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return transform.rotation;
            }

            Vector3 cameraUp = planeUp;
            if (Vector3.Cross(lookDirection.normalized, cameraUp).sqrMagnitude <= Mathf.Epsilon)
            {
                cameraUp = projectionPlane.forward;
            }

            return Quaternion.LookRotation(lookDirection, cameraUp);
        }

        private bool HasValidReferences()
        {
            return target != null && projectionPlane != null;
        }

        private void OnValidate()
        {
            horizontalSmoothTime = Mathf.Max(0f, horizontalSmoothTime);
            verticalSmoothTime = Mathf.Max(0f, verticalSmoothTime);
            lookAheadDistance = Mathf.Max(0f, lookAheadDistance);
            lookAheadSmoothTime = Mathf.Max(0f, lookAheadSmoothTime);
            lookAheadVelocityThreshold = Mathf.Max(0f, lookAheadVelocityThreshold);
            rotationSmoothTime = Mathf.Max(0f, rotationSmoothTime);
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasValidReferences())
            {
                return;
            }

            Vector3 planeRight = projectionPlane.right;
            Vector3 planeUp = projectionPlane.up;
            Vector3 planeNormal = projectionPlane.forward;
            float previewLookAhead = Application.isPlaying ? currentLookAhead : 0f;
            Vector3 aimPoint = CalculateAimPoint(planeRight, planeUp, previewLookAhead);
            Vector3 desiredPosition = CalculateDesiredPosition(
                planeRight,
                planeUp,
                planeNormal,
                previewLookAhead);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.15f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(aimPoint, 0.15f);
            Gizmos.DrawLine(target.position, aimPoint);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(desiredPosition, 0.2f);
            Gizmos.DrawLine(desiredPosition, aimPoint);
        }
    }
}
