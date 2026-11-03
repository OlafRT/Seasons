using System.Collections.Generic;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Listens to the EXISTING fungal upgrade nodes and grows pale organic branches
    /// toward their orb positions. No upgrade data, purchases, or market code changes.
    /// </summary>
    [DefaultExecutionOrder(7000)]
    [DisallowMultipleComponent]
    public sealed class TreeJamFungalUpgradeGrowth : MonoBehaviour
    {
        public TreeJamFungalMushroomSystem mushroomSystem;
        public TreeJamMyceliumNetwork network;
        public TreeJamFungalRoute fungalRoute;
        public TreeJamSoilSurface soil;
        public TreeJamFungalUndergroundGuard groundGuard;

        [Min(0.5f)] public float secondsToGrow = 5.5f;
        [Range(0.035f, 0.15f)] public float branchThickness = 0.075f;
        [Range(0.02f, 0.10f)] public float fineFilamentThickness = 0.045f;
        [Range(0, 5)] public int extraFilamentsPerUpgrade = 3;
        public Color color = new Color(0.91f, 0.96f, 0.83f, 1f);
        [Tooltip("Optional material; otherwise reuse the material from the original mycelium.")]
        public Material strandMaterial;
        public SpriteRenderer grassSprite;

        private sealed class Branch
        {
            public Vector3 endpoint;
            public Vector3[] path;
            public LineRenderer main;
            public LineRenderer[] filaments;
            public Vector3[][] filamentPaths;
            public float began;
            public bool IsFinished(float seconds) => Time.time - began >= seconds;
        }

        private TreeJamFungalUpgradeNode[] nodes;
        private readonly Dictionary<FungalUpgradeKind, Branch> grown = new Dictionary<FungalUpgradeKind, Branch>();
        private Transform visualsRoot;
        private Material ownedMaterial;
        private bool didWarn;

        private void Awake()
        {
            if (mushroomSystem == null) mushroomSystem = GetComponent<TreeJamFungalMushroomSystem>();
            if (network == null) network = FindFirstObjectByType<TreeJamMyceliumNetwork>();
            if (fungalRoute == null) fungalRoute = GetComponent<TreeJamFungalRoute>();
            if (soil == null) soil = FindFirstObjectByType<TreeJamSoilSurface>();
            if (groundGuard == null) groundGuard = GetComponent<TreeJamFungalUndergroundGuard>();
            nodes = GetComponentsInChildren<TreeJamFungalUpgradeNode>(true);
            if (grassSprite == null)
            {
                TreeJamMyceliumPolish polish = FindFirstObjectByType<TreeJamMyceliumPolish>();
                if (polish != null) grassSprite = polish.grassSprite;
            }
        }

        private void Update()
        {
            if (network == null || !network.Connected || soil == null || nodes == null) return;
            foreach (TreeJamFungalUpgradeNode node in nodes)
            {
                if (node == null || !node.purchased || grown.ContainsKey(node.upgrade)) continue;
                Vector3 origin;
                if (node.hasPrerequisite)
                {
                    if (!grown.TryGetValue(node.prerequisite, out Branch parent) || !parent.IsFinished(secondsToGrow)) continue;
                    origin = parent.endpoint;
                }
                else if (!GetRouteAttachment(node.transform.position, out origin)) continue;
                StartBranch(node, origin);
            }
            foreach (Branch branch in grown.Values) Draw(branch);
        }

        private bool GetRouteAttachment(Vector3 target, out Vector3 origin)
        {
            origin = Vector3.zero;
            if (fungalRoute == null) return false;
            float middle = network.treeRoot != null ? network.treeRoot.position.x : 0f;
            List<Vector3> route = fungalRoute.BuildCourierRoute(target.x < middle);
            if (route == null || route.Count == 0) return false;
            // Begin a little closer to the tree than the orb, so a new hypha visibly
            // branches out from the main transport line rather than growing on top of it.
            float attachX = Mathf.Lerp(target.x, middle, 0.28f);
            float best = float.MaxValue;
            foreach (Vector3 pt in route)
            {
                float dist = Mathf.Abs(pt.x - attachX);
                if (dist < best) { best = dist; origin = pt; }
            }
            return true;
        }

        private Vector3 UnderSoil(Vector3 p, float width)
        {
            if (groundGuard != null) return groundGuard.ClampUnderSoil(p, width);
            if (soil != null && soil.soilOutline != null)
            {
                Bounds b = soil.WorldBounds;
                float x = Mathf.Clamp(p.x, b.min.x + 0.002f, b.max.x - 0.002f);
                if (soil.TryGetHeight(x, out float y)) p.y = Mathf.Min(p.y, y - 0.16f - width * 0.5f);
            }
            return p;
        }

        private void StartBranch(TreeJamFungalUpgradeNode node, Vector3 origin)
        {
            if (visualsRoot == null)
            {
                Transform found = transform.Find("FUNGAL UPGRADE BRANCHES - runtime");
                if (found == null)
                {
                    GameObject root = new GameObject("FUNGAL UPGRADE BRANCHES - runtime");
                    root.transform.SetParent(transform, false);
                    visualsRoot = root.transform;
                }
                else visualsRoot = found;
            }
            float width = branchThickness;
            Vector3 end = UnderSoil(node.transform.position, width);
            origin = UnderSoil(origin, width);
            Branch branch = new Branch();
            branch.endpoint = end;
            branch.began = Time.time;
            branch.path = Bezier(origin, end, width, (int)node.upgrade);
            branch.main = CreateLine("Fungal growth " + node.displayName, width);
            int n = Mathf.Max(0, extraFilamentsPerUpgrade);
            branch.filaments = new LineRenderer[n];
            branch.filamentPaths = new Vector3[n][];
            for (int i = 0; i < n; i++)
            {
                float t = (i + 1f) / (n + 1f);
                Vector3 p = branch.path[Mathf.Clamp(Mathf.RoundToInt(t * (branch.path.Length - 1)), 0, branch.path.Length - 1)];
                float sign = i % 2 == 0 ? 1f : -1f;
                float lateral = (node.transform.position.x >= origin.x ? 1f : -1f) * (.19f + .055f * (i % 3));
                Vector3 finish = UnderSoil(p + new Vector3(lateral, sign * (.20f + .06f * i), 0), fineFilamentThickness);
                branch.filamentPaths[i] = Bezier(p, finish, fineFilamentThickness, (int)node.upgrade + i + 7, 10);
                branch.filaments[i] = CreateLine("Fungal filament " + node.displayName + " " + i, fineFilamentThickness);
            }
            grown.Add(node.upgrade, branch);
        }

        private Vector3[] Bezier(Vector3 a, Vector3 b, float width, int seed, int count = 22)
        {
            Vector3[] points = new Vector3[count];
            Vector3 v = b - a;
            float bend = (seed % 2 == 0 ? -1f : 1f) * 0.20f;
            Vector3 c1 = a + v * .30f + new Vector3(-v.y * .08f, bend, 0);
            Vector3 c2 = a + v * .73f + new Vector3(v.y * .08f, -bend * .30f, 0);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float s = 1f - t;
                Vector3 p = s*s*s*a + 3f*s*s*t*c1 + 3f*s*t*t*c2 + t*t*t*b;
                points[i] = UnderSoil(p, width);
            }
            return points;
        }

        private LineRenderer CreateLine(string title, float width)
        {
            GameObject go = new GameObject(title);
            go.transform.SetParent(visualsRoot, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 8;
            line.numCornerVertices = 8;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, .95f), new Keyframe(.75f, .82f), new Keyframe(1f, .30f));
            line.startColor = line.endColor = color;
            line.sharedMaterial = GetMaterial();
            line.positionCount = 0;
            if (grassSprite != null)
            {
                line.sortingLayerID = grassSprite.sortingLayerID;
                line.sortingOrder = grassSprite.sortingOrder - 1;
            }
            return line;
        }

        private Material GetMaterial()
        {
            if (strandMaterial != null) return strandMaterial;
            if (network != null)
            {
                foreach (LineRenderer lr in network.GetComponentsInChildren<LineRenderer>(true))
                    if (lr != null && lr.sharedMaterial != null) return lr.sharedMaterial;
            }
            if (ownedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null && !didWarn)
                {
                    Debug.LogWarning("Tree Jam: no unlit shader for new mycelium branches.", this);
                    didWarn = true;
                }
                if (shader != null) ownedMaterial = new Material(shader);
            }
            return ownedMaterial;
        }

        private void Draw(Branch branch)
        {
            float p = Mathf.Clamp01((Time.time - branch.began) / Mathf.Max(0.1f, secondsToGrow));
            DrawPartial(branch.main, branch.path, p);
            for (int i = 0; i < branch.filaments.Length; i++)
            {
                float childP = Mathf.Clamp01((p - (0.35f + i * .12f)) / .38f);
                DrawPartial(branch.filaments[i], branch.filamentPaths[i], childP);
            }
        }

        private static void DrawPartial(LineRenderer line, Vector3[] path, float progress)
        {
            if (line == null || path == null || path.Length < 2 || progress <= 0f)
            {
                if (line != null) line.positionCount = 0;
                return;
            }
            float traveled = Mathf.Clamp01(progress) * (path.Length - 1);
            int segment = Mathf.Min(path.Length - 2, Mathf.FloorToInt(traveled));
            float fract = progress >= 1f ? 1f : traveled - segment;
            line.positionCount = segment + 2;
            for (int i = 0; i <= segment; i++) line.SetPosition(i, path[i]);
            line.SetPosition(segment + 1, Vector3.Lerp(path[segment], path[segment + 1], fract));
        }

        private void OnDestroy()
        {
            if (ownedMaterial != null) Destroy(ownedMaterial);
        }
    }
}
