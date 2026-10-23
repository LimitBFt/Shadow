using UnityEngine;

namespace ShadowPath.Camera
{
    public class CameraFollowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, InspectorName("Player")] private Transform target;
        [SerializeField] private Transform projectionPlane;

        [Header("Composition")]
        [SerializeField, InspectorName("Plane Horizontal Offset")] private float planeRightOffset;
        [SerializeField, InspectorName("Plane Vertical Offset")] private float planeUpOffset = 3f;
        [SerializeField, InspectorName("Normal Offset")] private float planeNormalOffset = 8f;
        [SerializeField] private float aimRightOffset;
        [SerializeField] private float aimUpOffset = 1f;

        [Header("Position Follow")]
        [SerializeField, Min(0f)] private float horizontalSmoothTime = 0.45f;
        [SerializeField, Min(0f)] private float verticalSmoothTime = 0.75f;
        [SerializeField, Min(0f)] private float maxFollowSpeed = 8f;

        [Header("Dead Zone (Plane Units, Half Extents)")]
        [SerializeField, Min(0f)] private float horizontalDeadZone = 0.6f;
        [SerializeField, Min(0f)] private float verticalDeadZone = 0.35f;

        [Header("Look Ahead")]
        [SerializeField] private bool enableLookAhead = true;
        [SerializeField, Min(0f)] private float lookAheadDistance = 1f;
        [SerializeField, Min(0f)] private float lookAheadSmoothTime = 0.65f;
        [SerializeField, Min(0f)] private float lookAheadMaxSpeed = 3f;
        [SerializeField, Min(0f)] private float lookAheadVelocityThreshold = 0.05f;

        [Header("Rotation")]
        [SerializeField, Min(0f)] private float rotationSmoothTime = 0.65f;

        [Header("Catch Up")]
        [SerializeField, Min(0f)] private float catchUpDistance = 4f;
        [SerializeField, Min(1f)] private float catchUpMultiplier = 1.8f;

        [Header("Debug")]
        [SerializeField] private Vector2 currentTargetPlanePosition;
        [SerializeField] private Vector2 currentCameraPlanePosition;
        [SerializeField] private float currentLookAhead;
        [SerializeField] private float currentFollowError;

        private float rightVelocity;
        private float upVelocity;
        private float normalVelocity;
        private float lookAheadVelocity;
        private Vector3 previousTargetPosition;
        private Vector2 deadZoneAnchor;
        private Vector3 filteredTargetPosition;
        private bool horizontalFollowActive;
        private float currentCatchUpGain = 1f;
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

            UpdateDeadZone(planeRight, planeUp, planeNormal);
            UpdateLookAhead(planeRight);

            Vector3 desiredPosition = CalculateDesiredPosition(
                planeRight,
                planeUp,
                planeNormal,
                currentLookAhead);
            currentFollowError = Vector3.Distance(transform.position, desiredPosition);
            currentCatchUpGain = CameraFollowMath.CatchUpGain(currentFollowError, catchUpDistance, catchUpMultiplier);
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
            RefreshDebug(desiredPosition, planeRight, planeUp);
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
            Vector3 relativeTarget = target.position - projectionPlane.position;
            deadZoneAnchor = new Vector2(Vector3.Dot(relativeTarget, planeRight), Vector3.Dot(relativeTarget, planeUp));
            filteredTargetPosition = target.position;
            horizontalFollowActive = false;

            transform.position = CalculateDesiredPosition(planeRight, planeUp, planeNormal, 0f);
            Vector3 aimPoint = CalculateAimPoint(planeRight, planeUp, 0f);
            transform.rotation = CalculateDesiredRotation(transform.position, aimPoint, planeUp);

            previousTargetPosition = target.position;
            initialized = true;
            currentFollowError = 0f;
            RefreshDebug(transform.position, planeRight, planeUp);
        }

        private void UpdateDeadZone(Vector3 planeRight, Vector3 planeUp, Vector3 planeNormal)
        {
            Vector3 relativeTarget = target.position - projectionPlane.position;
            float previousAnchorX = deadZoneAnchor.x;
            deadZoneAnchor.x = CameraFollowMath.AdvanceDeadZone(deadZoneAnchor.x,
                Vector3.Dot(relativeTarget, planeRight), horizontalDeadZone);
            deadZoneAnchor.y = CameraFollowMath.AdvanceDeadZone(deadZoneAnchor.y,
                Vector3.Dot(relativeTarget, planeUp), verticalDeadZone);
            horizontalFollowActive = Mathf.Abs(deadZoneAnchor.x - previousAnchorX) > 0.000001f;
            filteredTargetPosition = projectionPlane.position + planeRight * deadZoneAnchor.x
                + planeUp * deadZoneAnchor.y + planeNormal * Vector3.Dot(relativeTarget, planeNormal);
        }

        private void RefreshDebug(Vector3 desiredPosition, Vector3 planeRight, Vector3 planeUp)
        {
            Vector3 desired = desiredPosition - projectionPlane.position;
            Vector3 current = transform.position - projectionPlane.position;
            currentTargetPlanePosition = new Vector2(Vector3.Dot(desired, planeRight), Vector3.Dot(desired, planeUp));
            currentCameraPlanePosition = new Vector2(Vector3.Dot(current, planeRight), Vector3.Dot(current, planeUp));
            currentFollowError = Vector3.Distance(desiredPosition, transform.position);
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
            float desiredLookAhead = CameraFollowMath.LookAheadTarget(horizontalSpeed,
                enableLookAhead && (horizontalFollowActive || horizontalDeadZone <= 0f),
                lookAheadDistance, lookAheadVelocityThreshold);

            currentLookAhead = Mathf.SmoothDamp(
                currentLookAhead,
                desiredLookAhead,
                ref lookAheadVelocity,
                Mathf.Max(0.0001f, lookAheadSmoothTime),
                Mathf.Max(0f, lookAheadMaxSpeed),
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
            if (deltaTime <= 0f) return currentPosition;
            float speed = Mathf.Max(0f, maxFollowSpeed) * currentCatchUpGain;
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
                Mathf.Max(0.0001f, horizontalSmoothTime / currentCatchUpGain),
                speed,
                deltaTime);
            float smoothedUp = Mathf.SmoothDamp(
                currentUp,
                desiredUp,
                ref upVelocity,
                Mathf.Max(0.0001f, verticalSmoothTime / currentCatchUpGain),
                speed,
                deltaTime);
            float smoothedNormal = Mathf.SmoothDamp(
                currentNormal,
                desiredNormal,
                ref normalVelocity,
                Mathf.Max(0.0001f, horizontalSmoothTime / currentCatchUpGain),
                speed,
                deltaTime);

            Vector3 smoothed = planeRight * smoothedRight
                + planeUp * smoothedUp
                + planeNormal * smoothedNormal;
            Vector3 change = smoothed - currentPosition;
            float maximumStep = speed * deltaTime;
            if (change.magnitude > maximumStep && change.sqrMagnitude > 0f)
            {
                float scale = maximumStep / change.magnitude;
                rightVelocity *= scale;
                upVelocity *= scale;
                normalVelocity *= scale;
                return currentPosition + change * scale;
            }
            return smoothed;
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
            Vector3 followTarget = initialized ? filteredTargetPosition : target.position;
            return followTarget
                + planeRight * (planeRightOffset + lookAhead)
                + planeUp * planeUpOffset
                + planeNormal * planeNormalOffset;
        }

        private Vector3 CalculateAimPoint(Vector3 planeRight, Vector3 planeUp, float lookAhead)
        {
            Vector3 followTarget = initialized ? filteredTargetPosition : target.position;
            return followTarget
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
            maxFollowSpeed = Mathf.Max(0f, maxFollowSpeed);
            horizontalDeadZone = Mathf.Max(0f, horizontalDeadZone);
            verticalDeadZone = Mathf.Max(0f, verticalDeadZone);
            lookAheadDistance = Mathf.Max(0f, lookAheadDistance);
            lookAheadSmoothTime = Mathf.Max(0f, lookAheadSmoothTime);
            lookAheadMaxSpeed = Mathf.Max(0f, lookAheadMaxSpeed);
            lookAheadVelocityThreshold = Mathf.Max(0f, lookAheadVelocityThreshold);
            rotationSmoothTime = Mathf.Max(0f, rotationSmoothTime);
            catchUpDistance = Mathf.Max(0f, catchUpDistance);
            catchUpMultiplier = Mathf.Max(1f, catchUpMultiplier);
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
