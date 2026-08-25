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

        private VoxelPlayerController player;
        private Renderer faceHighlight;
        private Renderer blockHighlight;
        private GameObject placementWireframe;
        private Color highlightBaseColor;
        private Vector3Int currentTarget;
        private bool hasTarget;
        private float miningProgress;
        private float deniedFlashTimer;

        private static readonly Color DeniedColor = new(1f, 0.15f, 0.15f, 0.5f);
        private const float DeniedFlashDuration = 1f;

        private void Awake()
        {
            if (world == null)
                world = FindFirstObjectByType<World>();
            player = GetComponentInParent<VoxelPlayerController>();

            faceHighlight = CreateHighlight(PrimitiveType.Quad, "FaceHighlight");
            blockHighlight = CreateHighlight(PrimitiveType.Cube, "BlockHighlight");
            highlightBaseColor = faceHighlight.material.color;
            placementWireframe = CreatePlacementWireframe();
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

            BlockDefinitionBase definition =
                world.Settings.BlockForId(world.Data[hitCell.x, hitCell.y, hitCell.z].BlockTypeId);

            deniedFlashTimer -= Time.deltaTime;

            if (mouse.leftButton.isPressed && !world.CanMine(hitCell))
            {
                placementWireframe.SetActive(false);
                ShowUnmineableHighlight(hitCell);
            }
            else if (mouse.leftButton.isPressed && definition != null)
            {
                placementWireframe.SetActive(false);
                ShowMiningHighlight(hitCell, definition);
            }
            else
            {
                miningProgress = 0f;
                ShowHoverHighlight(hitCell, faceNormal);
                ShowPlacementWireframe(placeCell);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (placeCell.y >= world.Data.sizeY)
                    deniedFlashTimer = DeniedFlashDuration;
                else if (!PlacementOverlapsPlayer(placeCell))
                    world.TryPlace(placeCell);
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

        private void ShowMiningHighlight(Vector3Int hitCell, BlockDefinitionBase definition)
        {
            miningProgress += Time.deltaTime;
            if (miningProgress >= definition.MineDuration && world.TryMine(hitCell))
                miningProgress = 0f;

            Color color = highlightBaseColor;
            color.a = Mathf.Lerp(highlightBaseColor.a, 0.85f, Mathf.Clamp01(miningProgress / definition.MineDuration));
            blockHighlight.material.color = color;
            blockHighlight.transform.position = hitCell + Vector3.one * 0.5f;
            blockHighlight.gameObject.SetActive(true);
            faceHighlight.gameObject.SetActive(false);
        }

        // Held on an unbreakable block (the world floor): pulse the block red.
        private void ShowUnmineableHighlight(Vector3Int hitCell)
        {
            miningProgress = 0f;
            Color color = DeniedColor;
            color.a = Mathf.Lerp(0.2f, 0.6f, Mathf.PingPong(Time.time * 4f, 1f));
            blockHighlight.material.color = color;
            blockHighlight.transform.position = hitCell + Vector3.one * 0.5f;
            blockHighlight.gameObject.SetActive(true);
            faceHighlight.gameObject.SetActive(false);
        }

        // While hovering nothing is highlighted; the face only appears as the red
        // flash of a denied placement (against the world ceiling).
        private void ShowHoverHighlight(Vector3Int hitCell, Vector3Int faceNormal)
        {
            blockHighlight.gameObject.SetActive(false);
            bool flashing = deniedFlashTimer > 0f;
            faceHighlight.gameObject.SetActive(flashing);
            if (!flashing)
                return;

            faceHighlight.material.color =
                Color.Lerp(highlightBaseColor, DeniedColor, deniedFlashTimer / DeniedFlashDuration);
            faceHighlight.transform.position = hitCell + Vector3.one * 0.5f + (Vector3)faceNormal * 0.505f;
            faceHighlight.transform.rotation = Quaternion.LookRotation(-(Vector3)faceNormal);
        }

        // Dot crosshair marking the screen center while the cursor is locked.
        private void OnGUI()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;
            const float size = 4f;
            GUI.DrawTexture(
                new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size),
                Texture2D.whiteTexture);
        }

        // Outlines the cell a right click would fill; visible only when the
        // placement would actually succeed.
        private void ShowPlacementWireframe(Vector3Int placeCell)
        {
            bool canPlace = world.CanPlace(placeCell) && !PlacementOverlapsPlayer(placeCell);
            placementWireframe.SetActive(canPlace);
            if (canPlace)
                placementWireframe.transform.position = placeCell + Vector3.one * 0.5f;
        }

        private GameObject CreatePlacementWireframe()
        {
            Vector3[] corners =
            {
                new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, 0.5f),
                new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f),
            };
            int[] edges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };

            var mesh = new Mesh { name = "PlacementWireframe" };
            mesh.vertices = corners;
            mesh.SetIndices(edges, MeshTopology.Lines, 0);

            var wireframe = new GameObject("PlacementWireframe");
            wireframe.AddComponent<MeshFilter>().sharedMesh = mesh;
            var wireframeRenderer = wireframe.AddComponent<MeshRenderer>();
            if (highlightMaterial != null)
                wireframeRenderer.material = highlightMaterial;
            wireframeRenderer.material.color = Color.black;
            // Slightly shrunk so the edges do not z-fight with neighboring block faces.
            wireframe.transform.localScale = Vector3.one * 0.995f;
            wireframe.SetActive(false);
            return wireframe;
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
            if (placementWireframe != null)
                placementWireframe.SetActive(false);
        }
    }
}
