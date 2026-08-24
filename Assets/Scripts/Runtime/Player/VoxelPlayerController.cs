using UnityEngine;
using UnityEngine.InputSystem;

namespace CustomMinecraft.Player
{
    /// <summary>
    /// Walking player with custom voxel physics — no Unity physics involved.
    /// The body is an axis-aligned box (position = center of the feet) moved
    /// axis by axis against <see cref="WorldData"/>: each axis' displacement is
    /// applied alone and clamped to the first solid cell it would enter, which
    /// gives wall sliding, ground detection, and head bonks with no vector math.
    /// </summary>
    public sealed class VoxelPlayerController : MonoBehaviour
    {
        [SerializeField] private World world;
        [SerializeField] private Transform cameraTransform;

        [Header("Body")]
        [SerializeField, Min(0.1f)] private float width = 0.6f;
        [SerializeField, Min(0.1f)] private float height = 1.8f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 5f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.6f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -25f;
        [SerializeField, Min(1f)] private float maxFallSpeed = 30f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.1f;

        // Keeps clamped positions strictly outside walls despite float rounding.
        private const float SkinEpsilon = 0.001f;

        private Vector3 velocity;
        private bool grounded;
        private float yaw;
        private float pitch;

        /// <summary>The player's collision box in world space.</summary>
        public Bounds WorldBounds =>
            new(transform.position + Vector3.up * (height * 0.5f), new Vector3(width, height, width));

        private void Awake()
        {
            if (world == null)
                world = FindFirstObjectByType<World>();
            if (cameraTransform == null)
                cameraTransform = GetComponentInChildren<Camera>().transform;
            world.Regenerated += Respawn;
        }

        private void Start()
        {
            if (world.Data != null)
                Respawn();
        }

        private void OnDestroy()
        {
            world.Regenerated -= Respawn;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null || world.Data == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
            if (keyboard.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            UpdateLook(mouse);
            UpdateVelocity(keyboard);
            MoveAxis(0, velocity.x * Time.deltaTime);
            grounded = false;
            MoveAxis(1, velocity.y * Time.deltaTime);
            MoveAxis(2, velocity.z * Time.deltaTime);

            // Fell out of the world (walked off the edge): start over on the surface.
            if (transform.position.y < -10f)
                Respawn();
        }

        private void UpdateLook(Mouse mouse)
        {
            Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateVelocity(Keyboard keyboard)
        {
            Vector3 input = Vector3.zero;
            if (keyboard.wKey.isPressed) input += transform.forward;
            if (keyboard.sKey.isPressed) input -= transform.forward;
            if (keyboard.dKey.isPressed) input += transform.right;
            if (keyboard.aKey.isPressed) input -= transform.right;
            input.y = 0f;
            float speed = walkSpeed * (keyboard.leftShiftKey.isPressed ? sprintMultiplier : 1f);
            input = input.normalized * speed;
            velocity.x = input.x;
            velocity.z = input.z;

            if (grounded && keyboard.spaceKey.isPressed)
                velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);

            velocity.y = Mathf.Max(velocity.y + gravity * Time.deltaTime, -maxFallSpeed);
        }

        private void MoveAxis(int axis, float delta)
        {
            if (delta == 0f)
                return;

            // One frame can never move more than a block per axis, so the clamp
            // below always catches the first wall even at terrible framerates.
            delta = Mathf.Clamp(delta, -0.9f, 0.9f);

            Vector3 position = transform.position;
            position[axis] += delta;

            if (BoxOverlapsSolid(position))
            {
                if (delta > 0f)
                {
                    // Leading face penetrated the cell starting at floor(max); rest against it.
                    float boundary = Mathf.Floor(BoxMax(position)[axis]);
                    position[axis] = boundary - (BoxMax(position)[axis] - position[axis]) - SkinEpsilon;
                }
                else
                {
                    // Leading face penetrated the cell ending at floor(min) + 1.
                    float boundary = Mathf.Floor(BoxMin(position)[axis]) + 1f;
                    position[axis] = boundary - (BoxMin(position)[axis] - position[axis]) + SkinEpsilon;
                    if (axis == 1)
                        grounded = true;
                }
                velocity[axis] = 0f;
            }

            transform.position = position;
        }

        private Vector3 BoxMin(Vector3 position) =>
            position + new Vector3(-width * 0.5f, 0f, -width * 0.5f);

        private Vector3 BoxMax(Vector3 position) =>
            position + new Vector3(width * 0.5f, height, width * 0.5f);

        private bool BoxOverlapsSolid(Vector3 position)
        {
            Vector3Int min = Vector3Int.FloorToInt(BoxMin(position));
            Vector3Int max = Vector3Int.FloorToInt(BoxMax(position) - Vector3.one * SkinEpsilon);
            WorldData data = world.Data;

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int z = min.z; z <= max.z; z++)
                    {
                        // Above and below the world is open air; beyond the sides
                        // counts as solid, walling the world in.
                        if (y < 0 || y >= data.sizeY)
                            continue;
                        bool outsideSides = x < 0 || x >= data.sizeX || z < 0 || z >= data.sizeZ;
                        if (outsideSides || data.IsSolid(x, y, z))
                            return true;
                    }
                }
            }
            return false;
        }

        private void Respawn()
        {
            transform.position = new Vector3(
                world.Data.sizeX * 0.5f,
                world.Data.sizeY + 1f,
                world.Data.sizeZ * 0.5f);
            velocity = Vector3.zero;
            grounded = false;
        }
    }
}
