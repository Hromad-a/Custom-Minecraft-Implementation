using UnityEngine;
using UnityEngine.InputSystem;

namespace CustomMinecraft.Player
{
    /// <summary>
    /// Free-fly camera: mouse look, WASD, Space/Ctrl up and down, Shift for speed.
    /// Click locks the cursor, Escape releases it. Placeholder until walking
    /// player physics exist; block interaction lives in <see cref="BlockInteractor"/>.
    /// </summary>
    public sealed class FlyCamera : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 10f;
        [SerializeField, Min(1f)] private float fastMultiplier = 3f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.1f;

        private float yaw;
        private float pitch;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
            if (keyboard.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += transform.forward;
            if (keyboard.sKey.isPressed) move -= transform.forward;
            if (keyboard.dKey.isPressed) move += transform.right;
            if (keyboard.aKey.isPressed) move -= transform.right;
            if (keyboard.spaceKey.isPressed) move += Vector3.up;
            if (keyboard.leftCtrlKey.isPressed) move -= Vector3.up;

            float speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? fastMultiplier : 1f);
            transform.position += move.normalized * (speed * Time.deltaTime);
        }
    }
}
