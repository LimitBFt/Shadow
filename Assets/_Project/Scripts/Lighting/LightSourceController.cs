using UnityEngine;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace ShadowPath.Lighting
{
    public class LightSourceController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private bool clampMovement = true;
        [SerializeField] private Vector2 minPosition = new Vector2(-5f, -3f);
        [SerializeField] private Vector2 maxPosition = new Vector2(5f, 3f);

        private float initialZ;

        private void Awake()
        {
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
            position.x += input.x * Mathf.Max(0f, moveSpeed) * Time.deltaTime;
            position.y += input.y * Mathf.Max(0f, moveSpeed) * Time.deltaTime;

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
            Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            if (keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }
#else
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                input.x -= 1f;
            }

            if (Input.GetKey(KeyCode.RightArrow))
            {
                input.x += 1f;
            }

            if (Input.GetKey(KeyCode.DownArrow))
            {
                input.y -= 1f;
            }

            if (Input.GetKey(KeyCode.UpArrow))
            {
                input.y += 1f;
            }
#endif

            return input;
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
