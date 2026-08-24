using CustomMinecraft.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CustomMinecraft.Player
{
    /// <summary>
    /// The mine/place gameplay loop, attached to the camera. Targets a block via
    /// the voxel raycast, shows a highlight cube over it, mines with a held left
    /// button (hold duration from the block's definition, progress shown as the
    /// highlight fading in) and places with a right-button press into the cell
    /// adjacent to the hit face.
    /// </summary>
    public sealed class BlockInteractor : MonoBehaviour
    {
        [SerializeField] private World world;
        [SerializeField] private Material highlightMaterial;
        [SerializeField, Min(1f)] private float reach = 6f;

        private WorldRenderer worldRenderer;
        private Renderer highlight;
        private Color highlightBaseColor;
        private Vector3Int currentTarget;
        private bool hasTarget;
        private float miningProgress;

        private void Awake()
        {
            if (world == null)
                world = FindFirstObjectByType<World>();
            worldRenderer = world.GetComponent<WorldRenderer>();
            CreateHighlight();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || world.Data == null || Cursor.lockState != CursorLockMode.Locked)
            {
                ClearTarget();
                return;
            }

            if (!VoxelRaycaster.Raycast(world.Data, transform.position, transform.forward, reach,
                    out Vector3Int hitCell, out Vector3Int placeCell))
            {
                ClearTarget();
                return;
            }

            // Looking at a different block resets mining progress, like Minecraft.
            if (!hasTarget || hitCell != currentTarget)
            {
                currentTarget = hitCell;
                hasTarget = true;
                miningProgress = 0f;
            }

            // The face we entered the block through, e.g. (0, 1, 0) for the top face.
            Vector3Int faceNormal = placeCell - hitCell;
            if (faceNormal == Vector3Int.zero)
            {
                // Camera is inside a block; there is no face to highlight.
                ClearTarget();
                return;
            }

            highlight.gameObject.SetActive(true);
            highlight.transform.position = hitCell + Vector3.one * 0.5f + (Vector3)faceNormal * 0.505f;
            highlight.transform.rotation = Quaternion.LookRotation(-(Vector3)faceNormal);

            BlockDefinition definition =
                world.Settings.BlockForId(world.Data[hitCell.x, hitCell.y, hitCell.z].BlockTypeId);

            if (mouse.leftButton.isPressed && definition != null)
            {
                miningProgress += Time.deltaTime;
                if (miningProgress >= definition.MineDuration && world.TryMine(hitCell))
                {
                    worldRenderer.RebuildChunkAt(hitCell.x, hitCell.z);
                    miningProgress = 0f;
                }
            }
            else
            {
                miningProgress = 0f;
            }

            float fraction = definition == null ? 0f : Mathf.Clamp01(miningProgress / definition.MineDuration);
            Color color = highlightBaseColor;
            color.a = Mathf.Lerp(highlightBaseColor.a, 0.85f, fraction);
            highlight.material.color = color;

            if (mouse.rightButton.wasPressedThisFrame
                && placeCell != Vector3Int.FloorToInt(transform.position)
                && world.TryPlace(placeCell))
            {
                worldRenderer.RebuildChunkAt(placeCell.x, placeCell.z);
            }
        }

        private void CreateHighlight()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FaceHighlight";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.localScale = Vector3.one * 1.01f;

            highlight = quad.GetComponent<Renderer>();
            if (highlightMaterial != null)
                highlight.material = highlightMaterial;
            highlightBaseColor = highlight.material.color;
            quad.SetActive(false);
        }

        private void ClearTarget()
        {
            hasTarget = false;
            miningProgress = 0f;
            if (highlight != null)
                highlight.gameObject.SetActive(false);
        }
    }
}
