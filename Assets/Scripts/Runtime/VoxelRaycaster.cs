using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// Grid traversal raycast (Amanatides &amp; Woo DDA) straight through
    /// <see cref="WorldData"/> — no colliders involved. Visits exactly the cells
    /// the ray passes through, in order; the first solid one is the hit, and the
    /// cell visited just before it is where a placed block belongs.
    /// </summary>
    public static class VoxelRaycaster
    {
        public static bool Raycast(WorldData data, Vector3 origin, Vector3 direction, float maxDistance,
            out Vector3Int hitCell, out Vector3Int adjacentCell)
        {
            direction = direction.normalized;
            Vector3Int cell = Vector3Int.FloorToInt(origin);
            hitCell = cell;
            adjacentCell = cell;
            if (data.IsSolid(cell.x, cell.y, cell.z))
                return true;

            var step = new Vector3Int(System.Math.Sign(direction.x), System.Math.Sign(direction.y), System.Math.Sign(direction.z));
            // t = distance along the ray to the next boundary crossing, per axis.
            var tMax = new Vector3(
                BoundaryT(origin.x, direction.x, cell.x),
                BoundaryT(origin.y, direction.y, cell.y),
                BoundaryT(origin.z, direction.z, cell.z));
            var tDelta = new Vector3(InverseAbs(direction.x), InverseAbs(direction.y), InverseAbs(direction.z));

            float t = 0f;
            while (t <= maxDistance)
            {
                Vector3Int previous = cell;
                if (tMax.x <= tMax.y && tMax.x <= tMax.z)
                {
                    t = tMax.x;
                    cell.x += step.x;
                    tMax.x += tDelta.x;
                }
                else if (tMax.y <= tMax.z)
                {
                    t = tMax.y;
                    cell.y += step.y;
                    tMax.y += tDelta.y;
                }
                else
                {
                    t = tMax.z;
                    cell.z += step.z;
                    tMax.z += tDelta.z;
                }

                if (t > maxDistance)
                    break;

                if (data.IsSolid(cell.x, cell.y, cell.z))
                {
                    hitCell = cell;
                    adjacentCell = previous;
                    return true;
                }
            }

            return false;
        }

        private static float BoundaryT(float origin, float direction, int cell) =>
            direction > 0f ? (cell + 1 - origin) / direction :
            direction < 0f ? (cell - origin) / direction :
            float.PositiveInfinity;

        private static float InverseAbs(float v) =>
            v == 0f ? float.PositiveInfinity : Mathf.Abs(1f / v);
    }
}
