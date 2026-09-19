using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowPath.Interaction
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    public class RotatableObject : MonoBehaviour
    {
        [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;
        [SerializeField, Min(1f)] private float stepAngle = 45f;
        [SerializeField, Min(0.01f)] private float rotationDuration = 0.25f;
        [SerializeField] private bool enableDebugInput = true;

        private Quaternion initialLocalRotation;
        private int rotationStepIndex;
        private Coroutine rotationCoroutine;

        public bool IsRotating { get; private set; }

        private void Awake()
        {
            initialLocalRotation = transform.localRotation;
        }

        private void Update()
        {
            HandleDebugInput();
        }

        public void RotatePositive()
        {
            TryRotate(1);
        }

        public void RotateNegative()
        {
            TryRotate(-1);
        }

        private void TryRotate(int stepDirection)
        {
            if (IsRotating)
            {
                return;
            }

            rotationStepIndex += stepDirection;
            Quaternion targetRotation = CalculateTargetRotation();
            rotationCoroutine = StartCoroutine(RotateToTarget(targetRotation));
        }

        private Quaternion CalculateTargetRotation()
        {
            float targetAngle = rotationStepIndex * stepAngle;
            return initialLocalRotation * Quaternion.AngleAxis(targetAngle, GetLocalRotationAxis());
        }

        private IEnumerator RotateToTarget(Quaternion targetRotation)
        {
            IsRotating = true;

            Quaternion startRotation = transform.localRotation;
            float elapsedTime = 0f;

            while (elapsedTime < rotationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / rotationDuration);
                transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            transform.localRotation = targetRotation;
            IsRotating = false;
            rotationCoroutine = null;
        }

        private Vector3 GetLocalRotationAxis()
        {
            switch (rotationAxis)
            {
                case RotationAxis.X:
                    return Vector3.right;
                case RotationAxis.Y:
                    return Vector3.up;
                default:
                    return Vector3.forward;
            }
        }

        private void HandleDebugInput()
        {
            if (!enableDebugInput)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.jKey.wasPressedThisFrame)
            {
                RotateNegative();
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                RotatePositive();
            }
        }

        private void OnDisable()
        {
            if (rotationCoroutine != null)
            {
                StopCoroutine(rotationCoroutine);
                rotationCoroutine = null;
            }

            IsRotating = false;
        }
    }
}
