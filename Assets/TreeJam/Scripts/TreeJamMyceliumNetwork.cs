using System.Collections.Generic;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Decorative, gradually growing underground fungal strands, arriving from both
    /// sides and converging at the tree roots. Connection unlocks the simple market.
    /// No upgrade assets or root scripts are modified.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamMyceliumNetwork : MonoBehaviour
    {
        public Camera worldCamera;
        public Transform treeRoot;
        [Header("Timing")]
        [Min(5f)] public float secondsUntilConnected = 85f;
        [Range(0f, 1f)] public float beginAfterYearFraction = 0f;
        [Header("Placement — editable in scene")]
        public float soilCenterY = -2f;
        [Range(0f, 2f)] public float centerRootDepth = .25f;
        [Range(1.5f, 10f)] public float spawnOffsetFromTree = 5f;
        [Range(.01f, .11f)] public float mainThreadWidth = .048f;
        public Color threadColor = new Color(.85f, .92f, .78f, .95f);
        public Color mushroomColor = new Color(.91f, .88f, .73f, .98f);
        [Tooltip("Optional artwork prefab. Without one, simple mushrooms are drawn from curved lines.")]
        public GameObject mushroomPrefab;

        public float Progress01 { get; private set; }
        public bool Connected { get; private set; }

        private readonly List<LineRenderer> strands = new List<LineRenderer>();
        private readonly List<Vector3[]> allPaths = new List<Vector3[]>();
        private readonly List<GameObject> fruitBodies = new List<GameObject>();
        private Material threadMaterial;
        private float elapsed;
        private bool notified;
        private const int Points = 42;

        private void Start()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (treeRoot == null)
            {
                var tree = GameObject.Find("The Tree");
                if (tree != null) treeRoot = tree.transform;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) { enabled = false; Debug.LogWarning("Tree Jam fungi: no unlit shader available", this); return; }
            threadMaterial = new Material(shader) { name = "Runtime pale fungal network" };
            BuildStrands();
            BuildFruits();
            UpdateGraphics();
        }

        [ContextMenu("TEST - Connect network immediately (Play Mode)")]
        private void TestConnect()
        {
            if (Application.isPlaying) elapsed = secondsUntilConnected * (1f + beginAfterYearFraction) + 1f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float delay = secondsUntilConnected * Mathf.Clamp01(beginAfterYearFraction);
            Progress01 = Mathf.Clamp01((elapsed - delay) / Mathf.Max(5f, secondsUntilConnected));
            Connected = Progress01 >= 1f;
            UpdateGraphics();
            if (Connected && !notified)
            {
                notified = true;
                TreeJamHUD.Notify("The fungal network has reached your roots. Trading is now available!");
            }
        }

        private Vector3 P(float x, float y) => new Vector3(x, y, 0f);
        private void BuildStrands()
        {
            float centerX = treeRoot != null ? treeRoot.position.x : 0f;
            float radius = spawnOffsetFromTree;
            if (worldCamera != null) radius = Mathf.Max(radius, worldCamera.orthographicSize * worldCamera.aspect * .88f);
            for (int side = -1; side <= 1; side += 2)
            {
                // A gently meandering primary filament from either side of the soil.
                Vector3[] path = new Vector3[Points];
                float y0 = soilCenterY + (side < 0 ? -.25f : .07f);
                float y1 = soilCenterY + centerRootDepth;
                for (int i = 0; i < Points; i++)
                {
                    float t = i / (float)(Points - 1);
                    float smooth = t * t * (3f - 2f * t);
                    float x = centerX + side * Mathf.Lerp(radius, .23f, smooth);
                    float y = Mathf.Lerp(y0, y1, t) +
                              Mathf.Sin(t * Mathf.PI * 3.2f + (side < 0 ? 0f : 1.1f)) * .20f * Mathf.Sin(Mathf.PI * t);
                    path[i] = P(x, y);
                }
                CreateThread("Main mycelium " + (side < 0 ? "LEFT" : "RIGHT"), path, mainThreadWidth, 0f);
                // Secondary filaments; different growth offsets keep the roots alive-looking.
                for (int branch = 0; branch < 4; branch++)
                {
                    float t0 = .14f + branch * .17f;
                    int attach = Mathf.Clamp(Mathf.RoundToInt(t0 * (Points - 1)), 0, Points - 1);
                    Vector3 basePoint = path[attach];
                    Vector3[] offshoot = new Vector3[16];
                    float direction = branch % 2 == 0 ? -1f : 1f;
                    for (int j = 0; j < offshoot.Length; j++)
                    {
                        float t = j / (float)(offshoot.Length - 1);
                        offshoot[j] = basePoint + P(side * t * .46f, direction * t * (.35f + .07f * branch) + .06f * Mathf.Sin(t * 7f));
                    }
                    CreateThread("Fine hypha " + side + "/" + branch, offshoot, mainThreadWidth * .45f, t0 + .06f);
                }
            }
        }

        private void CreateThread(string label, Vector3[] positions, float width, float progressOffset)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = threadMaterial;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 3;
            lr.sortingOrder = 4; // in front of soil, behind individual leaves
            lr.startColor = lr.endColor = threadColor;
            lr.widthMultiplier = width;
            lr.widthCurve = AnimationCurve.EaseInOut(0f, .55f, 1f, .08f);
            lr.positionCount = 0;
            strands.Add(lr);
            allPaths.Add(positions);
            // Store a harmless per-thread growth delay in the parent name's float? No;
            // the natural stagger instead comes from the thread index in UpdateGraphics.
        }

        private void BuildFruits()
        {
            float centerX = treeRoot != null ? treeRoot.position.x : 0f;
            float[] offsets = { -3.15f, -2.45f, 2.75f, 3.65f };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject go = new GameObject("Fungal fruiting body " + (i + 1));
                go.transform.SetParent(transform, false);
                go.transform.position = P(centerX + offsets[i], soilCenterY + (i % 2 == 0 ? .17f : -.05f));
                if (mushroomPrefab != null)
                {
                    GameObject art = Instantiate(mushroomPrefab, go.transform);
                    art.name = "Editable mushroom prefab instance";
                    art.transform.localPosition = Vector3.zero;
                }
                else
                {
                    var stem = MakeFruitLine(go.transform, "Stem", .045f);
                    stem.positionCount = 2;
                    stem.SetPosition(0, P(0f, 0f));
                    stem.SetPosition(1, P(.02f, .18f));
                    var cap = MakeFruitLine(go.transform, "Cap", .055f);
                    cap.positionCount = 11;
                    for (int p = 0; p < 11; p++)
                    {
                        float t = p / 10f;
                        cap.SetPosition(p, P(-.13f + .26f * t, .185f + .095f * Mathf.Sin(t * Mathf.PI)));
                    }
                }
                go.transform.localScale = Vector3.zero;
                fruitBodies.Add(go);
            }
        }

        private LineRenderer MakeFruitLine(Transform parent, string label, float width)
        {
            var o = new GameObject(label);
            o.transform.SetParent(parent, false);
            var lr = o.AddComponent<LineRenderer>();
            lr.sharedMaterial = threadMaterial;
            lr.useWorldSpace = false;
            lr.startColor = lr.endColor = mushroomColor;
            lr.widthMultiplier = width;
            lr.numCapVertices = 5;
            lr.sortingOrder = 7;
            return lr;
        }

        private void UpdateGraphics()
        {
            for (int i = 0; i < strands.Count; i++)
            {
                // Each offshoot begins only after its parent has reached the
                // attachment point. This prevents stray pieces appearing ahead
                // of the advancing main fungal strand.
                float local = Progress01;
                if (i % 5 != 0)
                {
                    int branchNumber = i % 5 - 1;
                    Vector3[] parent = allPaths[(i / 5) * 5];
                    float attachT = .14f + branchNumber * .17f;
                    int anchorIndex = Mathf.Clamp(Mathf.RoundToInt(attachT * (parent.Length - 1)), 0, parent.Length - 1);
                    float startAt = DistanceFractionToIndex(parent, anchorIndex) + .025f;
                    local = Mathf.Clamp01((Progress01 - startAt) / .16f);
                }
                // Grow continuously along the route. The previous CeilToInt code
                // revealed a whole new waypoint at once, causing visible "hops".
                DrawSmoothGrowingPath(strands[i], allPaths[i], local);
            }
            for (int i = 0; i < fruitBodies.Count; i++)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Progress01 - (.42f + i * .11f)) / .2f));
                fruitBodies[i].transform.localScale = Vector3.one * Mathf.Max(.0001f, t * (i % 2 == 0 ? 1f : .78f));
            }
        }

        private static float DistanceFractionToIndex(Vector3[] path, int index)
        {
            if (path == null || path.Length < 2) return 0f;
            float whole = 0f, portion = 0f;
            for (int j = 1; j < path.Length; j++)
            {
                float length = Vector3.Distance(path[j-1], path[j]);
                whole += length;
                if (j <= index) portion += length;
            }
            return whole > .00001f ? portion / whole : 0f;
        }

        /// <summary>
        /// Renders every completed waypoint plus one interpolated tip. Progress is
        /// measured by arc length, giving the leading tip a constant apparent speed
        /// even where the path bends or its sample points are unevenly spaced.
        /// </summary>
        private static void DrawSmoothGrowingPath(LineRenderer line, Vector3[] path, float progress)
        {
            if (line == null || path == null || path.Length < 2 || progress <= 0f)
            {
                if (line != null) line.positionCount = 0;
                return;
            }

            if (progress >= 1f)
            {
                line.positionCount = path.Length;
                line.SetPositions(path);
                return;
            }

            float totalLength = 0f;
            for (int j = 1; j < path.Length; j++)
                totalLength += Vector3.Distance(path[j - 1], path[j]);

            float remaining = totalLength * Mathf.Clamp01(progress);
            for (int segment = 1; segment < path.Length; segment++)
            {
                float length = Vector3.Distance(path[segment - 1], path[segment]);
                if (remaining <= length || segment == path.Length - 1)
                {
                    line.positionCount = segment + 1;
                    for (int j = 0; j < segment; j++) line.SetPosition(j, path[j]);
                    float t = length > .00001f ? Mathf.Clamp01(remaining / length) : 1f;
                    line.SetPosition(segment, Vector3.Lerp(path[segment - 1], path[segment], t));
                    return;
                }
                remaining -= length;
            }
        }

        private void OnDestroy() { if (threadMaterial != null) Destroy(threadMaterial); }
    }
}
