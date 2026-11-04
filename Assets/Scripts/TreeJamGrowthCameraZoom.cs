using System;
using System.Collections;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// An entirely visual camera effect: begin slightly closer and slowly zoom out as
    /// the actual wooden tree grows. Never zooms back in when autumn leaves fall.
    /// The maximum zoom is clamped against the shared horizontal extent of the sky
    /// and soil and their combined vertical extent, so the framing stays in the art.
    /// No references to TreeJam's resource, upgrade, or season scripts are needed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(1500)]
    public sealed class TreeJamGrowthCameraZoom : MonoBehaviour
    {
        [Header("Scene references")]
        [Tooltip("Drag 'The Tree' here, not its individual trunk or generated branches.")]
        public Transform treeRoot;
        [Tooltip("Your visible daytime sky image (the sky covering the upper part of the scene).")]
        public SpriteRenderer daySky;
        [Tooltip("Your soil/background image covering the lower part of the scene.")]
        public SpriteRenderer soil;

        [Header("Framing")]
        [Tooltip("How close the camera starts, relative to its existing Orthographic Size. 0.82 = 18% closer.")]
        [Range(0.5f, 1f)] public float startingZoom = 0.82f;
        [Tooltip("How far out the camera can grow relative to its original Orthographic Size.")]
        [Range(1f, 1.3f)] public float matureZoom = 1.10f;
        [Tooltip("How many additional WORLD units of tree width/height are needed for full zoom-out. Larger = more gradual.")]
        [Min(0.1f)] public float treeGrowthForFullZoom = 4f;
        [Tooltip("Seconds for the camera to settle into a larger view. Higher = more subtle.")]
        [Min(0.1f)] public float zoomSmoothTime = 9f;
        [Tooltip("Padding to keep inside the sky/soil graphics, measured in world units.")]
        [Min(0f)] public float edgePadding = 0.16f;

        [Header("Diagnostics (Play Mode)")]
        [SerializeField] private float originalOrthographicSize;
        [SerializeField] private float baselineTreeSize;
        [SerializeField] private float largestTreeSizeSeen;
        [SerializeField] private float currentGrowth01;
        [SerializeField] private float safeMaximumSize;

        private Camera worldCamera;
        private float zoomVelocity;
        private float nextTreeSample;
        private bool initialized;
        private bool warnedMissingBounds;

        private void Awake()
        {
            worldCamera = GetComponent<Camera>();
            if (!worldCamera.orthographic)
            {
                Debug.LogWarning("Tree Jam Growth Camera: This requires an orthographic camera.", this);
                enabled = false;
                return;
            }
            originalOrthographicSize = worldCamera.orthographicSize;
        }

        private IEnumerator Start()
        {
            // Give the procedural bonsai a frame to create its initial branch meshes.
            yield return null;
            yield return null;
            if (treeRoot == null)
            {
                GameObject tree = GameObject.Find("The Tree");
                if (tree != null) treeRoot = tree.transform;
            }
            if (treeRoot == null)
            {
                Debug.LogWarning("Tree Jam Growth Camera: Assign 'The Tree' in Tree Root.", this);
                enabled = false;
                yield break;
            }

            float dimension;
            if (!TryMeasureWood(out dimension))
            {
                Debug.LogWarning("Tree Jam Growth Camera: Could not find any rendered tree wood. " +
                                 "Make sure the procedural bonsai is active.", this);
                enabled = false;
                yield break;
            }
            baselineTreeSize = dimension;
            largestTreeSizeSeen = dimension;
            initialized = true;
            safeMaximumSize = GetSafeMaximumSize();
            float startSize = Mathf.Min(originalOrthographicSize * startingZoom, safeMaximumSize);
            worldCamera.orthographicSize = Mathf.Max(0.05f, startSize);
        }

        private void LateUpdate()
        {
            if (!initialized || worldCamera == null) return;

            if (Time.unscaledTime >= nextTreeSample)
            {
                nextTreeSample = Time.unscaledTime + 0.3f;
                float size;
                if (TryMeasureWood(out size))
                    largestTreeSizeSeen = Mathf.Max(largestTreeSizeSeen, size);
            }

            currentGrowth01 = Mathf.Clamp01((largestTreeSizeSeen - baselineTreeSize) /
                                            Mathf.Max(0.1f, treeGrowthForFullZoom));
            // SmoothStep makes zoom barely move at first and slow again at maturity.
            float progress = currentGrowth01 * currentGrowth01 * (3f - 2f * currentGrowth01);
            safeMaximumSize = GetSafeMaximumSize();
            float minimumSize = originalOrthographicSize * startingZoom;
            float maximumSize = originalOrthographicSize * matureZoom;
            float goal = Mathf.Min(Mathf.Lerp(minimumSize, maximumSize, progress), safeMaximumSize);
            goal = Mathf.Max(0.05f, goal);
            worldCamera.orthographicSize = Mathf.SmoothDamp(
                worldCamera.orthographicSize, goal, ref zoomVelocity,
                Mathf.Max(0.1f, zoomSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);
            // Never reveal space outside the assigned artwork, even on a sudden resize.
            worldCamera.orthographicSize = Mathf.Min(worldCamera.orthographicSize, safeMaximumSize);
        }

        /// <summary>
        /// Measure wood only; leaves falling or growing cannot make the camera zoom.
        /// Lines are used by TreeJam's procedural bonsai. Meshes are also supported.
        /// Visible trunk/root sprites are accepted for other tree-art implementations.
        /// </summary>
        private bool TryMeasureWood(out float longestDimension)
        {
            longestDimension = 0f;
            if (treeRoot == null) return false;
            Renderer[] all = treeRoot.GetComponentsInChildren<Renderer>(true);
            Bounds combined = new Bounds();
            bool haveBounds = false;
            foreach (Renderer renderer in all)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (renderer is SpriteRenderer)
                {
                    // Exclude foliage, which disappears in autumn, and decorative art.
                    string n = renderer.gameObject.name;
                    if (n.IndexOf("leaf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("foliage", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                }
                else if (!(renderer is LineRenderer) && !(renderer is MeshRenderer))
                {
                    continue;
                }
                Bounds b = renderer.bounds;
                if (b.size.sqrMagnitude <= 0.000001f) continue;
                if (!haveBounds) { combined = b; haveBounds = true; }
                else combined.Encapsulate(b);
            }
            if (!haveBounds) return false;
            longestDimension = Mathf.Max(combined.size.x, combined.size.y);
            return true;
        }

        private float GetSafeMaximumSize()
        {
            // Existing camera framing is the fallback if the art references are empty.
            // For this particular scene, the SKY is above the SOIL: use the intersection
            // of their horizontal spans and the union of their vertical spans.
            if (daySky == null || soil == null || daySky.sprite == null || soil.sprite == null)
            {
                if (!warnedMissingBounds)
                {
                    Debug.LogWarning("Tree Jam Growth Camera: Assign BOTH Day Sky and Soil " +
                                     "to safely zoom farther out than the original camera view. " +
                                     "Until then, the original framing is the maximum.", this);
                    warnedMissingBounds = true;
                }
                return originalOrthographicSize;
            }

            Bounds sky = daySky.bounds;
            Bounds ground = soil.bounds;
            float leftEdge = Mathf.Max(sky.min.x, ground.min.x) + edgePadding;
            float rightEdge = Mathf.Min(sky.max.x, ground.max.x) - edgePadding;
            float bottomEdge = Mathf.Min(sky.min.y, ground.min.y) + edgePadding;
            float topEdge = Mathf.Max(sky.max.y, ground.max.y) - edgePadding;

            Vector3 pos = worldCamera.transform.position;
            float aspect = Mathf.Max(.01f, worldCamera.aspect);
            float safeSize = Mathf.Min(
                (pos.x - leftEdge) / aspect,
                (rightEdge - pos.x) / aspect,
                pos.y - bottomEdge,
                topEdge - pos.y);

            if (safeSize <= 0.05f)
            {
                if (!warnedMissingBounds)
                {
                    Debug.LogWarning("Tree Jam Growth Camera: The sky/soil bounds do not " +
                                     "contain the camera center. Falling back to original size. " +
                                     "Please check the SpriteRenderer assignments.", this);
                    warnedMissingBounds = true;
                }
                return originalOrthographicSize;
            }
            return safeSize;
        }

        private void OnDrawGizmosSelected()
        {
            if (daySky == null || soil == null || daySky.sprite == null || soil.sprite == null)
                return;
            Bounds sky = daySky.bounds, ground = soil.bounds;
            float x0 = Mathf.Max(sky.min.x, ground.min.x) + edgePadding;
            float x1 = Mathf.Min(sky.max.x, ground.max.x) - edgePadding;
            float y0 = Mathf.Min(sky.min.y, ground.min.y) + edgePadding;
            float y1 = Mathf.Max(sky.max.y, ground.max.y) - edgePadding;
            if (x1 <= x0 || y1 <= y0) return;
            Gizmos.color = new Color(0.24f, 0.95f, 0.65f, 0.9f);
            Vector3 center = new Vector3((x0+x1)*.5f, (y0+y1)*.5f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(x1-x0, y1-y0, 0f));
        }
    }
}
