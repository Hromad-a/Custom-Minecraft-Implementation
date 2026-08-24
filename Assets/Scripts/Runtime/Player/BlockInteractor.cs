using CustomMinecraft.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CustomMinecraft.Player
{
    /// <summary>
    /// The mine/place gameplay loop, attached to the camera. Targets a block via
    /// the voxel raycast. While hovering, the targeted face is highlighted with a
    /// quad; while mining (left button held) the whole block is highlighted, its
    /// alpha rising with mining progress. Right button places into the cell
    /// adjacent to the targeted face.
    /// </summary>
    public sealed class BlockInteractor : MonoBehaviour
    {
        [SerializeField] private World world;
        [SerializeField] private Material highlightMaterial;
        [SerializeField, Min(1f)] private float reach = 6f;

        private WorldRenderer worldRenderer;
        private VoxelPlayerController player;
        private Renderer faceHighlight;
        private Renderer blockHighlight;
        private Color highlightBaseColor;
        private Vector3Int currentTarget;
        private bool hasTarget;
        private float miningProgress;

        private void Awake()
        {
            if (world == null)
                world = FindFirstObjectByType<World>();
            worldRenderer = world.GetComponent<WorldRenderer>();
            player = GetComponentInParent<VoxelPlayerController>();

            faceHighlight = CreateHighlight(PrimitiveType.Quad, "FaceHighlight");
            blockHighlight = CreateHighlight(PrimitiveType.Cube, "BlockHighlight");
            highlightBaseColor = faceHighlight.material.color;
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

            // The face we entered the block through, e.g. (0, 1, 0) for the top face.
            Vector3Int faceNormal = placeCell - hitCell;
            if (faceNormal == Vector3Int.zero)
            {
                // Camera is inside a block; there is no face to highlight.
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

            BlockDefinition definition =
                world.Settings.BlockForId(world.Data[hitCell.x, hitCell.y, hitCell.z].BlockTypeId);

            if (mouse.leftButton.isPressed && definition != null)
            {
                ShowMiningHighlight(hitCell, definition);
            }
            else
            {
                miningProgress = 0f;
                ShowHoverHighlight(hitCell, faceNormal);
            }

            if (mouse.rightButton.wasPressedThisFrame
                && !PlacementOverlapsPlayer(placeCell)
                && world.TryPlace(placeCell))
            {
                worldRenderer.RebuildChunkAt(placeCell.x, placeCell.z);
            }
        }

        private bool PlacementOverlapsPlayer(Vector3Int cell)
        {
            if (player == null)
            {
                // Fly camera setup: no body, just avoid the camera's own cell.
                return cell == Vector3Int.FloorToInt(transform.position);
            }
            var cellBounds = new Bounds(cell + Vector3.one * 0.5f, Vector3.one);
            return player.WorldBounds.Intersects(cellBounds);
        }

        private void ShowMiningHighlight(Vector3Int hitCell, BlockDefinition definition)
        {
            miningProgress += Time.deltaTime;
            if (miningProgress >= definition.MineDuration && world.TryMine(hitCell))
            {
                worldRenderer.RebuildChunkAt(hitCell.x, hitCell.z);
                miningProgress = 0f;
            }

            Color color = highlightBaseColor;
            color.a = Mathf.Lerp(highlightBaseColor.a, 0.85f, Mathf.Clamp01(miningProgress / definition.MineDuration));
            blockHighlight.material.color = color;
            blockHighlight.transform.position = hitCell + Vector3.one * 0.5f;
            blockHighlight.gameObject.SetActive(true);
            faceHighlight.gameObject.SetActive(false);
        }

        private void ShowHoverHighlight(Vector3Int hitCell, Vector3Int faceNormal)
        {
            faceHighlight.transform.position = hitCell + Vector3.one * 0.5f + (Vector3)faceNormal * 0.505f;
            faceHighlight.transform.rotation = Quaternion.LookRotation(-(Vector3)faceNormal);
            faceHighlight.gameObject.SetActive(true);
            blockHighlight.gameObject.SetActive(false);
        }

        private Renderer CreateHighlight(PrimitiveType primitive, string objectName)
        {
            GameObject highlightObject = GameObject.CreatePrimitive(primitive);
            highlightObject.name = objectName;
            Destroy(highlightObject.GetComponent<Collider>());
            // Slightly inflated so it does not z-fight with the block's own faces.
            highlightObject.transform.localScale = Vector3.one * 1.01f;

            var highlightRenderer = highlightObject.GetComponent<Renderer>();
            if (highlightMaterial != null)
                highlightRenderer.material = highlightMaterial;
            highlightObject.SetActive(false);
            return highlightRenderer;
        }

        private void ClearTarget()
        {
            hasTarget = false;
            miningProgress = 0f;
            if (faceHighlight != null)
                faceHighlight.gameObject.SetActive(false);
            if (blockHighlight != null)
                blockHighlight.gameObject.SetActive(false);
        }
    }
}
