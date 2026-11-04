using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Procedural bonsai with smooth connected branch junctions and individual leaves.
    /// Compatible with the original TreeGameState, UpgradeOrb, and BonsaiGrowthLink.
    /// Seasonal methods are available, but NO seasons are automatically started.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrganicBonsaiTree : MonoBehaviour
    {
        [Header("Art / style")]
        public Color barkColor = new Color(.35f, .235f, .18f);
        public Color rootColor = new Color(.43f, .29f, .23f);
        public Color leafColor = new Color(.24f, .70f, .24f);
        public Color leafHighlight = new Color(.48f, .87f, .33f);
        public Sprite customLeafSprite;
        [Tooltip("Use an unlit 2D Sprite material here so the leaves are visible regardless of Global Light 2D settings. If empty, one is created at runtime.")]
        public Material leafMaterial;
        public Material woodMaterial;
        [Range(.5f, 2.2f)] public float leafSize = 1.15f;
        [Range(.5f, 1.8f)] public float trunkThickness = 1f;

        [Header("Foliage - distinct, visible bonsai pads")]
        [Range(6, 32)] public int leavesPerCluster = 19;
        [Range(.12f, .55f)] public float foliagePadRadius = .36f;
        [Range(.16f, .5f)] public float individualLeafSize = .33f;
        [Range(.4f, 1.5f)] public float foliageDensity = 1f;
        [Header("Foliage progression")]
        [Tooltip("Only this many INDIVIDUAL leaves are created at first, taken from TreeGameState.LeafCount. " +
                 "Upgrades with AddLeaves grow additional leaves. If no game state exists, use this fallback.")]
        [Min(0)] public int fallbackStartingLeaves = 5;
        [Min(.1f)] public float newLeafGrowthSeconds = 3.5f;
        [Min(0f)] public float staggerBetweenNewLeaves = .18f;

        [Header("Branch junctions")]
        [Range(.6f, .95f)] public float maximumTipAttachment = .84f;
        [Range(.5f, 1.5f)] public float forkThickness = 1.05f;
        [Range(.1f, .7f)] public float forkBlendLength = .35f;
        [Min(.1f)] public float maximumAttachDistance = .47f;

        [Header("Growth timing - unchanged from previous version")]
        [Min(.2f)] public float secondsToGrowNewBranch = 7f;
        [Min(20f)] public float secondsPerYear = 180f;
        [Range(0f, .10f)] public float growthPerYear = .028f;
        [Range(0f, 1f)] public float leafAmount = 1f;
        public bool advanceAgeAutomatically = true;

        [Header("Future seasons - prepared but not auto-enabled")]
        public Color autumnLeafColor = new Color(.91f, .53f, .18f);
        [Tooltip("Fallback flat surface used for falling leaf previews. Later supply soil surface height from terrain.")]
        public float soilSurfaceWorldY = -1f;
        [Min(1f)] public float autumnFallDuration = 22f;
        [Min(.5f)] public float springRegrowthDuration = 7f;

        [Header("References")]
        public TreeGameState gameState;

        /// <summary>Raised for EACH leaf after it lands on the soil.</summary>
        public event Action<BonsaiLeaf> LeafLanded;
        /// <summary>Raised for EACH leaf after it has finished decaying.</summary>
        public event Action<BonsaiLeaf> LeafDecomposed;

        private const float GroundY = -1f;
        private const int CurveSegments = 30;
        private const string VisualRootName = "PROCEDURAL BONSAI (runtime)";
        private const string LitterRootName = "BONSAI LEAF LITTER (runtime)";

        private Transform generated;
        private Transform litterRoot;
        private Material runtimeMaterial;
        private Material runtimeLeafMaterial;
        private Texture2D generatedLeafTexture;
        private Sprite generatedLeafSprite;
        private float ageYears;
        private float springBloom = 1f;
        private bool built;
        private bool autumnInProgress;
        private Coroutine autumnCoroutine;
        private Coroutine springCoroutine;
        private Color currentFoliageTint;

        private sealed class Fork
        {
            public float at;
            public float childWidth;
        }

        private sealed class LeafSlot
        {
            public Transform attachedParent;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 fullScale;
            public Color tint;
            public float threshold;
            public BonsaiLeaf current;
            public bool occupied;
            public int rankInCluster;
            public int clusterIndex;
            public float budFraction = 1f;
            public float budWait;
        }

        private sealed class Limb
        {
            public Vector2 a, b, c, d;
            public float width;
            public float progress;
            public float elapsed;
            public bool expanding;
            public bool root;
            public float parentAttachT = .48f;
            public LineRenderer line;
            public readonly List<Fork> forks = new List<Fork>();
            public readonly List<Limb> offshoots = new List<Limb>();
            public readonly List<LeafSlot> leaves = new List<LeafSlot>();
        }

        private int nextLeafClusterIndex;
        private readonly List<Limb> limbs = new List<Limb>();
        private readonly List<BonsaiLeaf> fallenLeaves = new List<BonsaiLeaf>();
        private readonly HashSet<UpgradeDefinition> renderedUpgrades = new HashSet<UpgradeDefinition>();
        private readonly Dictionary<UpgradeDefinition, Limb> upgradeLimbs = new Dictionary<UpgradeDefinition, Limb>();
        private readonly List<BonsaiGrowthLink> queuedLinks = new List<BonsaiGrowthLink>();

        private void Start()
        {
            if (gameState == null) gameState = FindFirstObjectByType<TreeGameState>();
            Build();
            if (gameState != null) gameState.UpgradePurchased += HandlePurchase;

            BonsaiGrowthLink[] links = FindObjectsByType<BonsaiGrowthLink>(FindObjectsSortMode.None);
            // Restoring upgrades: prerequisites before their children, so fork attachment is stable.
            Array.Sort(links, (left, right) => PrerequisiteCount(GetUpgrade(left)).CompareTo(PrerequisiteCount(GetUpgrade(right))));
            foreach (BonsaiGrowthLink link in links)
            {
                if (link.gameObject.scene != gameObject.scene) continue;
                UpgradeDefinition upgrade = GetUpgrade(link);
                if (upgrade != null && gameState != null && gameState.IsPurchased(upgrade))
                    AddUpgradeLimb(link, true);
            }
            // Unlike the earlier renderer, don't automatically create every possible
            // leaf. We start with exactly the LeafCount used for photosynthesis.
            SyncLeafBudget(null, instant: true);
        }

        private static int PrerequisiteCount(UpgradeDefinition definition)
        {
            return definition != null && definition.prerequisites != null ? definition.prerequisites.Length : 0;
        }

        private void OnDestroy()
        {
            if (gameState != null) gameState.UpgradePurchased -= HandlePurchase;
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
            if (runtimeLeafMaterial != null) Destroy(runtimeLeafMaterial);
            if (generatedLeafSprite != null) Destroy(generatedLeafSprite);
            if (generatedLeafTexture != null) Destroy(generatedLeafTexture);
        }

        private void Update()
        {
            if (!built || generated == null) return;
            if (advanceAgeAutomatically)
                ageYears += Time.deltaTime / Mathf.Max(20f, secondsPerYear);

            float scale = 1f + Mathf.Min(.6f, ageYears * growthPerYear);
            generated.localScale = Vector3.one * scale;

            foreach (Limb limb in limbs)
            {
                if (!limb.expanding) continue;
                limb.elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(limb.elapsed / Mathf.Max(.2f, secondsToGrowNewBranch));
                limb.progress = Mathf.SmoothStep(0f, 1f, t);
                DrawLimb(limb);
                if (t >= 1f) limb.expanding = false;
            }
            // Orbs become purchasable as soon as their prerequisite is purchased, but
            // their wood must not appear in empty air before the parent has grown.
            for (int i = queuedLinks.Count - 1; i >= 0; i--)
            {
                BonsaiGrowthLink link = queuedLinks[i];
                if (link == null) { queuedLinks.RemoveAt(i); continue; }
                if (!IsParentReady(link)) continue;
                queuedLinks.RemoveAt(i);
                Limb added = AddUpgradeLimb(link, false);
                SyncLeafBudget(added != null && !added.root ? added : null, false);
            }
            foreach (Limb limb in limbs)
            foreach (LeafSlot leaf in limb.leaves)
            {
                if (leaf.current == null) continue;
                // A bud should not spend its growth timer before its branch
                // reaches the point where that leaf is attached.
                if (limb.progress >= leaf.threshold)
                {
                    if (leaf.budWait > 0f)
                        leaf.budWait = Mathf.Max(0f, leaf.budWait - Time.deltaTime);
                    else
                        leaf.budFraction = Mathf.MoveTowards(leaf.budFraction, 1f,
                            Time.deltaTime / Mathf.Max(.1f, newLeafGrowthSeconds));
                }
                leaf.current.SetGrowth(limb.progress,
                    leafAmount * springBloom * leaf.budFraction);
            }
        }

        public void SetAgeInYears(float years)
        {
            advanceAgeAutomatically = false;
            ageYears = Mathf.Max(0f, years);
        }

        /// <summary>
        /// Quick foliage preview. For a real autumn, call BeginAutumnLeafFall instead;
        /// SetLeafAmount(0) alone only shrinks leaves and doesn't produce litter.
        /// </summary>
        public void SetLeafAmount(float amount) => leafAmount = Mathf.Clamp01(amount);

        public void SetFoliageTint(Color tint)
        {
            currentFoliageTint = tint;
            foreach (Limb limb in limbs)
            foreach (LeafSlot slot in limb.leaves)
                if (slot.current != null) slot.current.SetTint(tint);
        }

        /// <summary>
        /// Seasonal entry point for future SeasonManager. Releases individual leaf
        /// objects across the supplied interval, rather than dropping a whole canopy.
        /// </summary>
        public void BeginAutumnLeafFall(float duration = -1f)
        {
            if (!built || autumnInProgress) return;
            if (springCoroutine != null) { StopCoroutine(springCoroutine); springCoroutine = null; }
            springBloom = 1f;
            autumnInProgress = true;
            SetFoliageTint(autumnLeafColor);
            autumnCoroutine = StartCoroutine(ReleaseLeaves(duration > 0f ? duration : autumnFallDuration));
        }

        private IEnumerator ReleaseLeaves(float duration)
        {
            List<LeafSlot> toDrop = new List<LeafSlot>();
            foreach (Limb limb in limbs)
            foreach (LeafSlot slot in limb.leaves)
                if (slot.current != null && limb.progress > slot.threshold + .1f)
                    toDrop.Add(slot);

            // Shuffle so the fall is distributed over the entire crown, not per branch.
            for (int i = toDrop.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                LeafSlot swap = toDrop[i];
                toDrop[i] = toDrop[randomIndex];
                toDrop[randomIndex] = swap;
            }

            float step = Mathf.Max(0f, duration) / Mathf.Max(1, toDrop.Count);
            foreach (LeafSlot slot in toDrop)
            {
                if (slot.current != null)
                {
                    BonsaiLeaf leaf = slot.current;
                    slot.current = null; // next spring makes a new leaf; old one remains as litter.
                    fallenLeaves.Add(leaf);
                    leaf.Landed += OnIndividualLeafLanded;
                    leaf.Decomposed += OnIndividualLeafDecomposed;
                    leaf.Fall(litterRoot, soilSurfaceWorldY, UnityEngine.Random.Range(3f, 6f));
                }
                if (step > 0f) yield return new WaitForSeconds(step);
            }
            autumnInProgress = false;
            autumnCoroutine = null;
        }

        /// <summary>
        /// Seasonal entry point: grow NEW leaves on the same branches. Fallen
        /// leaves remain on the soil, ready for the future decomposition cycle.
        /// </summary>
        public void BeginSpringLeafRegrowth(float duration = -1f)
        {
            if (!built) return;
            if (autumnCoroutine != null) { StopCoroutine(autumnCoroutine); autumnCoroutine = null; }
            autumnInProgress = false;
            if (springCoroutine != null) StopCoroutine(springCoroutine);
            currentFoliageTint = leafColor;
            leafAmount = 1f;
            springBloom = 0f;
            foreach (Limb limb in limbs)
            foreach (LeafSlot slot in limb.leaves)
            {
                // Dormant slots never contained a leaf. Don't generate hundreds
                // of new leaves each spring; only regrow leaves already earned.
                if (!slot.occupied) continue;
                if (slot.current == null) CreateLeaf(slot);
                slot.budFraction = 1f;
                slot.budWait = 0f;
                slot.current.SetTint(slot.tint);
            }
            springCoroutine = StartCoroutine(Bloom(duration > 0f ? duration : springRegrowthDuration));
        }

        private IEnumerator Bloom(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                springBloom = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(.2f, duration)));
                yield return null;
            }
            springBloom = 1f;
            springCoroutine = null;
        }

        private void OnIndividualLeafLanded(BonsaiLeaf leaf) => LeafLanded?.Invoke(leaf);
        private void OnIndividualLeafDecomposed(BonsaiLeaf leaf)
        {
            fallenLeaves.Remove(leaf);
            LeafDecomposed?.Invoke(leaf);
        }

        /// <summary>For later nutrient logic: request decay of all landed leaves.</summary>
        public void DecomposeLandedLeaves(float duration = 25f)
        {
            foreach (BonsaiLeaf leaf in fallenLeaves)
                if (leaf != null && leaf.State == BonsaiLeaf.LeafState.Landed)
                    leaf.BeginDecomposition(duration);
        }

        [ContextMenu("DEBUG - Inspect Leaf Renderers (Play Mode)")]
        private void DebugLeafRenderers()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("Bonsai: enter Play Mode, then run this command again.", this);
                return;
            }
            int total = 0, visible = 0;
            string example = "No leaves created";
            foreach (Limb limb in limbs)
            foreach (LeafSlot slot in limb.leaves)
            {
                if (slot.current == null) continue;
                total++;
                SpriteRenderer sr = slot.current.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    if (sr.enabled && sr.gameObject.activeInHierarchy) visible++;
                    if (total == 1)
                        example = "shader=" + (sr.sharedMaterial != null && sr.sharedMaterial.shader != null
                            ? sr.sharedMaterial.shader.name : "NO MATERIAL") +
                            ", color=" + sr.color + ", bounds=" + sr.bounds.size +
                            ", sprite=" + (sr.sprite != null ? sr.sprite.name : "NONE") +
                            ", sorting=" + sr.sortingLayerName + "/" + sr.sortingOrder;
                }
            }
            int reserved = 0;
            int available = 0;
            foreach (Limb limb in limbs)
            foreach (LeafSlot slot in limb.leaves)
            {
                available++;
                if (slot.occupied) reserved++;
            }
            Debug.Log("Bonsai leaf diagnostics: " + total + " attached leaves, " +
                visible + " renderer(s) enabled, " + reserved + " earned leaf slots out of " +
                available + " possible, resource LeafCount=" +
                (gameState != null ? gameState.LeafCount : fallbackStartingLeaves) +
                ", leafAmount=" + leafAmount + ", springBloom=" + springBloom +
                ". Example: " + example, this);
        }

        [ContextMenu("TEST in Play Mode - Autumn: Individual Leaves Fall")]
        private void TestAutumn()
        {
            if (Application.isPlaying) BeginAutumnLeafFall();
        }

        [ContextMenu("TEST in Play Mode - Spring: New Leaves Grow")]
        private void TestSpring()
        {
            if (Application.isPlaying) BeginSpringLeafRegrowth();
        }

        [ContextMenu("TEST in Play Mode - Decompose Fallen Litter")]
        private void TestDecay()
        {
            if (Application.isPlaying) DecomposeLandedLeaves(8f);
        }

        private static UpgradeDefinition GetUpgrade(BonsaiGrowthLink link)
        {
            if (link.upgrade != null) return link.upgrade;
            UpgradeOrb orb = link.GetComponent<UpgradeOrb>();
            return orb != null ? orb.definition : null;
        }

        private void HandlePurchase(UpgradeDefinition upgrade)
        {
            Limb newestCanopyLimb = null;
            BonsaiGrowthLink[] links = FindObjectsByType<BonsaiGrowthLink>(FindObjectsSortMode.None);
            foreach (BonsaiGrowthLink link in links)
            {
                if (link.gameObject.scene != gameObject.scene) continue;
                if (GetUpgrade(link) != upgrade) continue;
                if (!IsParentReady(link))
                {
                    if (!queuedLinks.Contains(link)) queuedLinks.Add(link);
                    continue;
                }
                Limb newLimb = AddUpgradeLimb(link, false);
                if (newLimb != null && !newLimb.root) newestCanopyLimb = newLimb;
            }
            // TreeGameState has already applied AddLeaves before publishing
            // UpgradePurchased. The late Crown Abundance upgrade deliberately
            // spreads new leaves around ALL existing pads, including branch tips.
            if (upgrade != null && upgrade.displayName == "Crown Abundance")
                newestCanopyLimb = null;
            SyncLeafBudget(newestCanopyLimb, instant: false);
        }

        private bool IsParentReady(BonsaiGrowthLink link)
        {
            if (link == null || link.parentOrb == null || link.parentOrb.definition == null)
                return true;
            if (!upgradeLimbs.TryGetValue(link.parentOrb.definition, out Limb parent))
                return false; // Parent might be queued behind another growing branch.
            return !parent.expanding && parent.progress >= .999f;
        }

        [ContextMenu("DEBUG - Reconcile Leaves With LeafCount (Play Mode)")]
        public void RefreshLeavesFromResourceCount()
        {
            if (Application.isPlaying) SyncLeafBudget(null, instant: false);
        }

        private static int CompareLeafSlots(LeafSlot a, LeafSlot b)
        {
            int rank = a.rankInCluster.CompareTo(b.rankInCluster);
            return rank != 0 ? rank : a.clusterIndex.CompareTo(b.clusterIndex);
        }

        private void SyncLeafBudget(Limb preferred, bool instant)
        {
            int target = gameState != null ? gameState.LeafCount : fallbackStartingLeaves;
            int allocated = 0;
            List<LeafSlot> candidates = new List<LeafSlot>();
            foreach (Limb limb in limbs)
            foreach (LeafSlot slot in limb.leaves)
            {
                if (slot.occupied) allocated++;
                else candidates.Add(slot);
            }
            if (allocated >= target) return;
            // Rank 0 is the first leaf in EACH cluster, so sparse crowns
            // distribute their leaves across the tree instead of piling them
            // into one dense cluster.
            candidates.Sort(CompareLeafSlots);

            int added = 0;
            // An AddLeaves upgrade that physically grows a branch places the
            // additional leaves on that branch and its twigs first.
            if (preferred != null)
            {
                foreach (LeafSlot slot in candidates)
                {
                    if (allocated >= target) break;
                    if (slot.attachedParent != preferred.line.transform &&
                        !IsOffshootParent(preferred, slot.attachedParent)) continue;
                    ActivateLeafSlot(slot, instant, added++);
                    allocated++;
                }
            }
            foreach (LeafSlot slot in candidates)
            {
                if (allocated >= target) break;
                if (slot.occupied) continue;
                ActivateLeafSlot(slot, instant, added++);
                allocated++;
            }
            if (allocated < target)
                Debug.LogWarning("Bonsai: only " + allocated + " visual leaf slots exist for " +
                    target + " resource leaves. Add a canopy branch with foliage clusters.", this);
        }

        private static bool IsOffshootParent(Limb limb, Transform target)
        {
            foreach (Limb child in limb.offshoots)
                if (child.line.transform == target || IsOffshootParent(child, target)) return true;
            return false;
        }

        private void ActivateLeafSlot(LeafSlot slot, bool instant, int addedIndex)
        {
            slot.occupied = true;
            slot.budFraction = instant ? 1f : 0f;
            slot.budWait = instant ? 0f : staggerBetweenNewLeaves * addedIndex;
            if (!autumnInProgress)
            {
                CreateLeaf(slot);
                slot.current.SetGrowth(1f, leafAmount * springBloom * slot.budFraction);
            }
        }

        private void Build()
        {
            if (built) return;
            built = true;
            currentFoliageTint = leafColor;
            Transform oldTree = transform.Find(VisualRootName);
            if (oldTree != null) Destroy(oldTree.gameObject);
            GameObject root = new GameObject(VisualRootName);
            generated = root.transform;
            generated.SetParent(transform, false);
            generated.localPosition = new Vector3(0f, GroundY, 0f);

            Transform oldLitter = transform.Find(LitterRootName);
            if (oldLitter != null) Destroy(oldLitter.gameObject);
            GameObject litter = new GameObject(LitterRootName);
            litterRoot = litter.transform;
            litterRoot.SetParent(transform, false);

            if (woodMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    Debug.LogError("Organic Bonsai: Sprites/Default shader not found. Assign a Wood Material.", this);
                    built = false;
                    return;
                }
                runtimeMaterial = new Material(shader) { name = "Bonsai Wood (runtime)" };
            }
            // SpriteRenderers in a URP 2D scene may default to a *lit* material.
            // If the 2D light does not affect their sorting layer, the leaf mesh exists
            // (and appears in Scene wireframe) but looks almost invisible in Game view.
            // Explicitly use the URP 2D Sprite-Unlit shader instead.
            if (leafMaterial == null)
            {
                Shader leafShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (leafShader == null) leafShader = Shader.Find("Sprites/Default");
                if (leafShader != null)
                {
                    runtimeLeafMaterial = new Material(leafShader)
                    {
                        name = "Bonsai Leaves - Unlit (runtime)"
                    };
                }
                else
                {
                    Debug.LogWarning("Bonsai: no compatible unlit Sprite shader was found. " +
                        "Create a Sprite-Unlit material and assign it to Leaf Material.", this);
                }
            }
            // A true opaque-center leaf texture is provided with this patch.
            // You can replace it with any custom Sprite through the Inspector.
            if (customLeafSprite == null)
                customLeafSprite = Resources.Load<Sprite>("BonsaiLeafSolid");
            if (customLeafSprite == null)
            {
                Debug.LogWarning("BonsaiLeafSolid sprite not found. Using built-in crisp leaf fallback. " +
                    "Run Tools > Tree Jam > Install Crisp Bonsai Leaves to import and assign it.", this);
                CreateFallbackLeafSprite();
            }

            AddBase("Trunk", P(0f, -1f), P(-.12f, -.22f), P(.27f, .72f), P(.04f, 1.12f),
                .32f * trunkThickness, false, 0);
            AddBase("Left scaffold", P(0f, .35f), P(-.25f, .61f), P(-.26f, 1.10f), P(-.57f, 1.24f),
                .14f, false, 3);
            AddBase("Right scaffold", P(.10f, .64f), P(.40f, .64f), P(.37f, 1.25f), P(.72f, 1.34f),
                .13f, false, 3);
            AddBase("Crown", P(.04f, 1.04f), P(-.07f, 1.25f), P(.28f, 1.36f), P(.12f, 1.55f),
                .09f, false, 2);
            AddBase("Left root", P(0f, -1f), P(-.21f, -1.25f), P(-.35f, -1.52f), P(-.58f, -1.73f),
                .16f, true, 0);
            AddBase("Right root", P(0f, -1f), P(.16f, -1.23f), P(.34f, -1.42f), P(.56f, -1.66f),
                .15f, true, 0);
            AddBase("Fine root left", P(-.37f, -1.55f), P(-.50f, -1.67f), P(-.62f, -1.71f), P(-.73f, -1.87f),
                .055f, true, 0);
            AddBase("Fine root right", P(.31f, -1.43f), P(.47f, -1.59f), P(.50f, -1.87f), P(.74f, -1.96f),
                .055f, true, 0);
        }

        private void AddBase(string label, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            float width, bool isRoot, int clusters)
        {
            // Existing starter skeleton is preserved. Register the forks so parent
            // limbs won't taper into needles at their connection points.
            Limb parent;
            float at;
            if (FindConnection(a, isRoot, out parent, out at))
                AddFork(parent, at, width);
            Limb limb = MakeLimb(label, a, b, c, d, width, isRoot, clusters);
            limb.progress = 1f;
            DrawLimb(limb);
        }

        private Limb AddUpgradeLimb(BonsaiGrowthLink link, bool fullyGrown)
        {
            UpgradeDefinition upgrade = GetUpgrade(link);
            if (upgrade == null || renderedUpgrades.Contains(upgrade)) return null;

            // V3+ nodes store their endpoints in the orb Transform (not the legacy
            // Start/End fields). Recompute from the LIVE orb positions so the user can
            // drag an orb to sculpt the tree without rebuilding any definitions.
            Vector2 endpoint = link.growToOrb
                ? (Vector2)transform.InverseTransformPoint(link.transform.position)
                : link.end;
            Vector2 start = link.start;
            Limb parent = null;
            float at = .84f;
            if (link.parentOrb != null)
            {
                UpgradeDefinition parentDef = link.parentOrb.definition;
                if (parentDef != null && upgradeLimbs.TryGetValue(parentDef, out Limb exactParent))
                {
                    parent = exactParent;
                    at = maximumTipAttachment;
                    start = Bezier(parent, at);
                }
                else start = (Vector2)transform.InverseTransformPoint(link.parentOrb.transform.position);
            }
            else if (FindConnection(start, link.isRoot, out Limb nearbyParent, out float nearbyT,
                link.baseThickness * .70f))
            {
                parent = nearbyParent;
                at = Mathf.Min(nearbyT, maximumTipAttachment);
                start = Bezier(parent, at);
            }

            Vector2 direction = endpoint - start;
            if (direction.sqrMagnitude < .025f)
            {
                Debug.LogWarning("Bonsai: orb " + upgrade.displayName +
                    " is too close to its parent's attachment point. Reposition the orb.", link);
                return null;
            }
            renderedUpgrades.Add(upgrade);
            if (parent != null) AddFork(parent, at, link.baseThickness);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;
            Vector2 b = start + direction * .30f + perpendicular * link.bend;
            Vector2 c = start + direction * .67f + perpendicular * link.bend * .65f;
            Limb limb = MakeLimb(upgrade.displayName, start, b, c, endpoint,
                link.baseThickness, link.isRoot, link.isRoot ? 0 : link.foliageClusters);
            upgradeLimbs[upgrade] = limb;
            limb.progress = fullyGrown ? 1f : 0f;
            limb.expanding = !fullyGrown;

            // Decorative offshoots start growing ONLY after the parent has
            // physically reached their attachment site. Previously both twigs
            // started at 48% of the main growth, including the twig at 78%.
            for (int n = 0; n < 2; n++)
            {
                float along = n == 0 ? .57f : .78f;
                Vector2 twigStart = Bezier(limb, along);
                Vector2 tangent = (Bezier(limb, Mathf.Min(1f, along + .02f)) -
                                   Bezier(limb, Mathf.Max(0f, along - .02f))).normalized;
                Vector2 side = new Vector2(-tangent.y, tangent.x) * (n == 0 ? 1f : -1f);
                Vector2 twigEnd = twigStart + tangent * .22f + side * (link.isRoot ? .26f : .32f);
                float twigWidth = link.baseThickness * .34f;
                Limb twig = MakeLimb(upgrade.displayName + " twig " + n, twigStart,
                    twigStart + tangent * .12f, twigEnd - side * .10f, twigEnd,
                    twigWidth, link.isRoot, link.isRoot ? 0 : 1);
                twig.parentAttachT = along;
                twig.progress = fullyGrown ? 1f : 0f;
                twig.expanding = false;
                limb.offshoots.Add(twig);
                AddFork(limb, along, twigWidth);
            }
            DrawLimb(limb);
            return limb;
        }

        private bool FindConnection(Vector2 attachment, bool isRoot, out Limb parent, out float at, float minimumParentWidth = 0f)
        {
            parent = null;
            at = 0f;
            float best = maximumAttachDistance * maximumAttachDistance;
            foreach (Limb limb in limbs)
            {
                if (limb.root != isRoot || limb.width < minimumParentWidth) continue;
                // Sample the entire curve; terminal tips are allowed as candidates.
                for (int i = 2; i <= 50; i++)
                {
                    float candidate = i / 50f;
                    float distance = (Bezier(limb, candidate) - attachment).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        at = candidate;
                        parent = limb;
                    }
                }
            }
            return parent != null;
        }

        private void AddFork(Limb parent, float at, float childWidth)
        {
            parent.forks.Add(new Fork { at = Mathf.Clamp(at, .04f, .96f), childWidth = childWidth });
            UpdateWidthCurve(parent);
        }

        private void UpdateWidthCurve(Limb limb)
        {
            // Keep the wood full enough near connections; only true ENDPOINTS
            // taper to a fine twig. Multiple child branches can share a parent.
            const int count = 40;
            Keyframe[] keys = new Keyframe[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                float baseWidth = Mathf.Lerp(1f, .07f, Mathf.Pow(t, 1.48f));
                float resulting = baseWidth;
                foreach (Fork fork in limb.forks)
                {
                    float needed = fork.childWidth * forkThickness /
                                   Mathf.Max(.015f, limb.width);
                    float weight;
                    if (t <= fork.at)
                    {
                        float approach = 1f - Mathf.Clamp01((fork.at - t) / forkBlendLength);
                        weight = Mathf.SmoothStep(0f, 1f, approach);
                    }
                    else
                    {
                        float decline = Mathf.Clamp01((t - fork.at) /
                            Mathf.Max(.08f, 1f - fork.at));
                        weight = 1f - Mathf.SmoothStep(0f, 1f, decline);
                    }
                    resulting = Mathf.Max(resulting, Mathf.Lerp(baseWidth, needed, weight));
                }
                keys[i] = new Keyframe(t, Mathf.Max(.035f, resulting));
            }
            limb.line.widthCurve = new AnimationCurve(keys);
        }

        private Limb MakeLimb(string label, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            float width, bool isRoot, int clusters)
        {
            GameObject go = new GameObject(label + " (curved wood)");
            go.transform.SetParent(generated, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = woodMaterial != null ? woodMaterial : runtimeMaterial;
            line.startColor = isRoot ? rootColor : barkColor;
            line.endColor = isRoot ? rootColor : barkColor;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 8;
            line.numCornerVertices = 5;
            line.sortingOrder = isRoot ? 2 : 3;
            line.widthMultiplier = width;
            Limb limb = new Limb
            {
                a = a, b = b, c = c, d = d, width = width,
                root = isRoot, line = line
            };
            UpdateWidthCurve(limb);
            limbs.Add(limb);

            if (!isRoot && clusters > 0)
            {
                for (int i = 0; i < clusters; i++)
                {
                    float point = clusters == 1 ? .95f : Mathf.Lerp(.70f, .98f,
                        i / (float)(clusters - 1));
                    AddLeafCluster(limb, go.transform, point,
                        Mathf.Max(1, Mathf.RoundToInt(leavesPerCluster * foliageDensity)),
                        331 + limbs.Count * 41 + i * 53);
                }
            }
            return limb;
        }

        private void AddLeafCluster(Limb limb, Transform parent, float at, int count, int seed)
        {
            int thisCluster = nextLeafClusterIndex++;
            var random = new System.Random(seed);
            Vector2 center = Bezier(limb, at) - new Vector2(0f, GroundY);
            // Lift the pad away from the wood. Previously leaves were centered
            // directly on the line geometry and looked like green stains on bark.
            Vector2 tangent = (Bezier(limb, Mathf.Min(1f, at + .025f)) -
                               Bezier(limb, Mathf.Max(0f, at - .025f))).normalized;
            Vector2 outward = new Vector2(-tangent.y, tangent.x);
            if (outward.y < 0f) outward = -outward;
            center += outward * foliagePadRadius * .78f;
            for (int i = 0; i < count; i++)
            {
                // Center-weighted elliptical pads; enough overlap to look lush,
                // but each leaf is still an independent object.
                float angle = (float)(i * (Math.PI * 2.0 / count) + random.NextDouble() * .5);
                float radial = i < count / 4 ? .25f + (float)random.NextDouble() * .23f :
                    .40f + (float)random.NextDouble() * .59f;
                float radius = foliagePadRadius * radial;
                Vector2 offset = new Vector2(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * .66f);
                float rotation = (float)random.NextDouble() * 160f - 80f;
                float diameter = individualLeafSize * leafSize *
                    Mathf.Lerp(.75f, 1.25f, (float)random.NextDouble());
                LeafSlot slot = new LeafSlot
                {
                    attachedParent = parent,
                    localPosition = center + offset,
                    localRotation = Quaternion.Euler(0f, 0f, rotation),
                    fullScale = new Vector3(diameter, diameter * .80f, 1f),
                    tint = Color.Lerp(leafColor, leafHighlight,
                        (float)random.NextDouble() * .9f),
                    threshold = Mathf.Clamp(at * .70f + (float)random.NextDouble() * .07f,
                        .42f, .84f),
                    clusterIndex = thisCluster,
                    rankInCluster = i
                };
                // Store potential growth sites, not 19 visible leaves. Only
                // earned leaves become GameObjects in SyncLeafBudget().
                limb.leaves.Add(slot);
            }
        }

        private void CreateLeaf(LeafSlot slot)
        {
            GameObject go = new GameObject("Bonsai leaf (individual)");
            go.transform.SetParent(slot.attachedParent, false);
            go.transform.localPosition = slot.localPosition;
            go.transform.localRotation = slot.localRotation;
            go.transform.localScale = slot.fullScale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = customLeafSprite != null ? customLeafSprite : generatedLeafSprite;
            // Assign a deliberately UNLIT material, not the renderer's default.
            sr.sharedMaterial = leafMaterial != null ? leafMaterial : runtimeLeafMaterial;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 8;
            sr.maskInteraction = SpriteMaskInteraction.None;
            // Keep actual independent leaves, never a single foliage blob.
            sr.color = Color.white;
            BonsaiLeaf leaf = go.AddComponent<BonsaiLeaf>();
            leaf.Initialize(slot.fullScale, slot.threshold, slot.tint);
            leaf.SetTint(currentFoliageTint != leafColor ? currentFoliageTint : slot.tint);
            slot.current = leaf;
        }

        private static Vector2 P(float x, float y) => new Vector2(x, y);

        private static Vector2 Bezier(Limb limb, float t)
        {
            float u = 1f - t;
            return u * u * u * limb.a + 3f * u * u * t * limb.b +
                   3f * u * t * t * limb.c + t * t * t * limb.d;
        }

        private void DrawLimb(Limb limb)
        {
            float p = Mathf.Clamp01(limb.progress);
            if (p < .002f)
            {
                limb.line.positionCount = 0;
            }
            else
            {
                int segments = Mathf.Max(2, Mathf.CeilToInt(CurveSegments * p));
                limb.line.positionCount = segments + 1;
                for (int i = 0; i <= segments; i++)
                {
                    float t = p * (i / (float)segments);
                    Vector2 pos = Bezier(limb, t) - new Vector2(0f, GroundY);
                    limb.line.SetPosition(i, new Vector3(pos.x, pos.y, 0f));
                }
            }
            foreach (Limb offshoot in limb.offshoots)
            {
                offshoot.progress = Mathf.Clamp01((p - offshoot.parentAttachT) /
                    Mathf.Max(.04f, 1f - offshoot.parentAttachT));
                DrawLimb(offshoot);
            }
        }

        private void CreateFallbackLeafSprite()
        {
            const int size = 64;
            generatedLeafTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            generatedLeafTexture.name = "Individual bonsai leaf (runtime)";
            generatedLeafTexture.wrapMode = TextureWrapMode.Clamp;
            generatedLeafTexture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = (x + .5f - size * .5f) / (size * .46f);
                float py = (y + .5f - size * .5f) / (size * .46f);
                // Deliberately SOLID interior. Only a 1-pixel AA outline.
                // Full renderer alpha does not imply the texture itself is opaque.
                float permittedY = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(px)), .62f);
                float edge = Mathf.Min(1f - Mathf.Abs(px), permittedY - Mathf.Abs(py));
                float alpha = Mathf.SmoothStep(-.012f, .012f, edge);
                generatedLeafTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            generatedLeafTexture.Apply(false);
            generatedLeafSprite = Sprite.Create(generatedLeafTexture,
                new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }
    }
}
