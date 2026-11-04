using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// The *visible soil sprite* is the source of truth for ground height.
    /// Samples the actual PolygonCollider2D outline in world space. The collider
    /// can be edited in Sprite Editor > Custom Physics Shape, if needed.
    /// No interpolation between invented terrain control points.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamSoilSurface : MonoBehaviour
    {
        [Tooltip("PolygonCollider2D on the painted SOIL sprite (not the grass or mountains).")]
        public PolygonCollider2D soilOutline;
        [Tooltip("Height offset above the actual collider boundary, if needed to align the grass edge.")]
        public float surfaceOffsetY = 0f;
        [Tooltip("Extra limit for out-of-range callers. Usually leave unchecked.")]
        public bool allowEdgeClamping = false;

        public Bounds WorldBounds => soilOutline != null ? soilOutline.bounds : new Bounds(Vector3.zero, Vector3.zero);

        /// <summary>Returns the highest boundary segment intersected by a vertical line at x.</summary>
        public bool TryGetHeight(float worldX, out float y)
        {
            y = 0f;
            if (soilOutline == null || soilOutline.pathCount == 0) return false;
            Bounds b = soilOutline.bounds;
            if (worldX < b.min.x || worldX > b.max.x)
            {
                if (!allowEdgeClamping) return false;
                worldX = Mathf.Clamp(worldX, b.min.x + .0001f, b.max.x - .0001f);
            }
            bool found = false;
            float highest = float.NegativeInfinity;
            Transform t = soilOutline.transform;
            Vector3 off = soilOutline.offset;
            for (int pathIndex = 0; pathIndex < soilOutline.pathCount; pathIndex++)
            {
                Vector2[] path = soilOutline.GetPath(pathIndex);
                if (path.Length < 2) continue;
                for (int i = 0; i < path.Length; i++)
                {
                    Vector3 a = t.TransformPoint((Vector3)path[i] + off);
                    Vector3 c = t.TransformPoint((Vector3)path[(i + 1) % path.Length] + off);
                    float dx = c.x - a.x;
                    if (Mathf.Abs(dx) < .00001f) continue;
                    if (worldX < Mathf.Min(a.x, c.x) || worldX > Mathf.Max(a.x, c.x)) continue;
                    float lerp = (worldX - a.x) / dx;
                    float candidate = Mathf.Lerp(a.y, c.y, lerp);
                    if (!found || candidate > highest) { highest = candidate; found = true; }
                }
            }
            if (found) y = highest + surfaceOffsetY;
            return found;
        }

        public float HeightOr(float worldX, float fallback)
        {
            return TryGetHeight(worldX, out float result) ? result : fallback;
        }

        [ContextMenu("DEBUG - Check height at the tree")]
        private void DebugHeight()
        {
            if (TryGetHeight(transform.position.x, out float y))
                Debug.Log("Tree Jam ground height at x=" + transform.position.x.ToString("0.00") + " is y=" + y.ToString("0.00"), this);
            else Debug.LogWarning("Tree Jam: ground outline does not cover this x. Check PolygonCollider2D on the Soil sprite.", this);
        }
    }
}
