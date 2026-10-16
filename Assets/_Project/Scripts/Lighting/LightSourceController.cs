using ShadowPath.Input;
using UnityEngine;

namespace ShadowPath.Lighting
{
    public class LightSourceController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float gamepadLightMoveSpeed = 2.5f;
        [SerializeField] private bool clampMovement = true;
        [SerializeField] private Vector2 minPosition = new Vector2(-5f, -3f);
        [SerializeField] private Vector2 maxPosition = new Vector2(5f, 3f);

        private float initialZ;

        private void Awake()
        {
            GameplayInput.TryInitialize(this);
            initialZ = transform.position.z;
        }

        private void Update()
        {
            Vector2 input = ReadMoveInput();
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            if (input.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector3 position = transform.position;
            float activeMoveSpeed = GameplayInput.IsLightMoveFromGamepad()
                ? gamepadLightMoveSpeed
                : moveSpeed;
            activeMoveSpeed = Mathf.Max(0f, activeMoveSpeed);
            position.x += input.x * activeMoveSpeed * Time.deltaTime;
            position.y += input.y * activeMoveSpeed * Time.deltaTime;

            if (clampMovement)
            {
                position.x = Mathf.Clamp(position.x, minPosition.x, maxPosition.x);
                position.y = Mathf.Clamp(position.y, minPosition.y, maxPosition.y);
            }

            position.z = initialZ;
            transform.position = position;
        }

        private static Vector2 ReadMoveInput()
        {
            return GameplayInput.LightMove == null
                ? Vector2.zero
                : GameplayInput.LightMove.ReadValue<Vector2>();
        }

        private void OnDrawGizmosSelected()
        {
            if (!clampMovement)
            {
                return;
            }

            float z = Application.isPlaying ? initialZ : transform.position.z;
            Vector3 bottomLeft = new Vector3(minPosition.x, minPosition.y, z);
            Vector3 bottomRight = new Vector3(maxPosition.x, minPosition.y, z);
            Vector3 topRight = new Vector3(maxPosition.x, maxPosition.y, z);
            Vector3 topLeft = new Vector3(minPosition.x, maxPosition.y, z);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
        }
    }
}
