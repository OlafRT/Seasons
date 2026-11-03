using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Non-destructive bridge for the existing individual BonsaiLeaf class.
    /// Corrects falling leaves onto the real soil outline and makes photosynthesis
    /// reflect attached leaves, without destroying earned leaf capacity/upgrades.
    /// Forces any stubborn leaves off at winter (the original fall coroutine can
    /// miss leaves on recently grown branches).
    /// </summary>
    [DefaultExecutionOrder(4000)]
    [DisallowMultipleComponent]
    public sealed class TreeJamLeafGroundAndSeasons : MonoBehaviour
    {
        public TreeJamSoilSurface ground;
        public TreeJamSeasonCycle seasons;
        public TreeGameState resources;
        public OrganicBonsaiTree bonsai;
        public TreeJamFungiMarket fungiMarket;
        [Header("Leaf-to-ground alignment")]
        [Range(-.06f,.13f)] public float leafRestHeight = .035f;
        [Header("Real gameplay: attached foliage versus earned leaf capacity")]
        public bool photosynthesisUsesAttachedLeaves = true;
        public bool clearRemainingLeavesInWinter = true;
        [Header("Read-only diagnostics (Play Mode)")]
        [SerializeField] private int attachedLeaves;
        [SerializeField] private int fallingLeaves;
        [SerializeField] private int landedLeaves;

        private sealed class Flight
        {
            public BonsaiLeaf.LeafState lastState;
            public float startY;
            public float nominalGround;
            public float finalOffset;
        }
        private readonly Dictionary<BonsaiLeaf, Flight> watched = new Dictionary<BonsaiLeaf, Flight>();
        private readonly List<BonsaiLeaf> remove = new List<BonsaiLeaf>();
        private float scanTimer;
        private bool reflectionWarningShown;
        private TreeSeason lastSeason;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            if (ground == null) ground = FindFirstObjectByType<TreeJamSoilSurface>();
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (resources == null) resources = FindFirstObjectByType<TreeGameState>();
            if (bonsai == null) bonsai = FindFirstObjectByType<OrganicBonsaiTree>();
            if (fungiMarket == null) fungiMarket = FindFirstObjectByType<TreeJamFungiMarket>();
            lastSeason = seasons != null ? seasons.CurrentSeason : TreeSeason.Spring;
            ScanLeaves();
        }

        private void LateUpdate()
        {
            scanTimer -= Time.deltaTime;
            bool scanNow = scanTimer <= 0f;
            if (scanNow) { scanTimer = .15f; ScanLeaves(); }
            if (seasons != null)
            {
                if (seasons.CurrentSeason != lastSeason)
                {
                    lastSeason = seasons.CurrentSeason;
                    if (lastSeason == TreeSeason.Winter && clearRemainingLeavesInWinter)
                        ReleaseStragglers();
                }
                if (lastSeason == TreeSeason.Winter && clearRemainingLeavesInWinter && scanNow)
                    ReleaseStragglers(); // Handles upgrades purchased during winter too.
            }

            attachedLeaves = fallingLeaves = landedLeaves = 0;
            remove.Clear();
            foreach (var pair in watched)
            {
                BonsaiLeaf leaf = pair.Key;
                if (leaf == null) { remove.Add(leaf); continue; }
                Flight f = pair.Value;
                switch (leaf.State)
                {
                    case BonsaiLeaf.LeafState.Attached:
                        SpriteRenderer renderer = leaf.GetComponent<SpriteRenderer>();
                        if (renderer != null && renderer.enabled && leaf.transform.lossyScale.sqrMagnitude > .00001f)
                            attachedLeaves++;
                        break;
                    case BonsaiLeaf.LeafState.Falling:
                        fallingLeaves++;
                        if (f.lastState != BonsaiLeaf.LeafState.Falling)
                        {
                            f.startY = leaf.transform.position.y;
                            f.nominalGround = bonsai != null ? bonsai.soilSurfaceWorldY : -1f;
                            f.finalOffset = leafRestHeight;
                        }
                        GroundFallingLeaf(leaf, f);
                        break;
                    case BonsaiLeaf.LeafState.Landed:
                        landedLeaves++;
                        GroundRestingLeaf(leaf, f);
                        break;
                    case BonsaiLeaf.LeafState.Decomposing:
                        GroundRestingLeaf(leaf, f);
                        break;
                }
                f.lastState = leaf.State;
            }
            foreach (var key in remove) watched.Remove(key);

            // TreeGameState.LeafCount remains the PURCHASED leaf capacity. Its
            // sunlight factor is scaled by the share of leaves actually on the tree.
            // This runs after DayNight, SeasonCycle and NordicSky LateUpdate.
            if (photosynthesisUsesAttachedLeaves && resources != null)
            {
                float fraction = resources.LeafCount > 0
                    ? Mathf.Clamp01(attachedLeaves / (float)resources.LeafCount) : 0f;
                resources.SetSunlightFactor(resources.SunlightFactor * fraction);
            }
        }

        private void ScanLeaves()
        {
            foreach (BonsaiLeaf leaf in FindObjectsByType<BonsaiLeaf>(FindObjectsSortMode.None))
            {
                if (leaf != null && !watched.ContainsKey(leaf))
                {
                    watched.Add(leaf, new Flight
                    {
                        lastState = leaf.State,
                        nominalGround = bonsai != null ? bonsai.soilSurfaceWorldY : -1f,
                        startY = leaf.transform.position.y,
                        finalOffset = leafRestHeight
                    });
                }
            }
        }

        private void GroundFallingLeaf(BonsaiLeaf leaf, Flight f)
        {
            if (ground == null || !ground.TryGetHeight(leaf.transform.position.x, out float y)) return;
            Vector3 pos = leaf.transform.position;
            float nominalEnd = f.nominalGround + .035f;
            float progress = Mathf.Clamp01((f.startY - pos.y) /
                Mathf.Max(.10f, f.startY - nominalEnd));
            // Preserve the leaf's own flutter animation, but smoothly redirect its
            // vertical destination to the real ground at its CURRENT drifting x.
            pos.y += (y + f.finalOffset - nominalEnd) * Mathf.SmoothStep(0f, 1f, progress);
            leaf.transform.position = pos;
        }

        private void GroundRestingLeaf(BonsaiLeaf leaf, Flight f)
        {
            if (ground == null || !ground.TryGetHeight(leaf.transform.position.x, out float y)) return;
            Vector3 pos = leaf.transform.position;
            pos.y = y + f.finalOffset;
            leaf.transform.position = pos;
        }

        private void ReleaseStragglers()
        {
            if (bonsai == null) return;
            // The current renderer tracks earned leaves in private LeafSlot entries.
            // Modifying the slot is essential: spring must create a NEW leaf there.
            FieldInfo limbsField = typeof(OrganicBonsaiTree).GetField("limbs", Fields);
            FieldInfo fallenField = typeof(OrganicBonsaiTree).GetField("fallenLeaves", Fields);
            FieldInfo litterField = typeof(OrganicBonsaiTree).GetField("litterRoot", Fields);
            if (limbsField == null || fallenField == null || litterField == null)
            {
                WarnReflection();
                return;
            }
            IEnumerable limbs = limbsField.GetValue(bonsai) as IEnumerable;
            List<BonsaiLeaf> fallen = fallenField.GetValue(bonsai) as List<BonsaiLeaf>;
            Transform litter = litterField.GetValue(bonsai) as Transform;
            if (limbs == null || fallen == null || litter == null) return;
            foreach (object limb in limbs)
            {
                FieldInfo leavesField = limb.GetType().GetField("leaves", Fields);
                IEnumerable slots = leavesField != null ? leavesField.GetValue(limb) as IEnumerable : null;
                if (slots == null) continue;
                foreach (object slot in slots)
                {
                    FieldInfo current = slot.GetType().GetField("current", Fields);
                    if (current == null) continue;
                    BonsaiLeaf leaf = current.GetValue(slot) as BonsaiLeaf;
                    if (leaf == null || leaf.State != BonsaiLeaf.LeafState.Attached) continue;
                    current.SetValue(slot, null);
                    fallen.Add(leaf);
                    leaf.Landed += OnExtraLeafLanded;
                    leaf.Decomposed += OnExtraLeafDecomposed;
                    leaf.Fall(litter, bonsai.soilSurfaceWorldY, UnityEngine.Random.Range(2f, 4f));
                }
            }
        }

        private void OnExtraLeafLanded(BonsaiLeaf leaf)
        {
            if (seasons != null && seasons.CurrentSeason == TreeSeason.Winter)
                leaf.BeginDecomposition(Mathf.Max(5f, seasons.secondsPerDay));
        }

        private void OnExtraLeafDecomposed(BonsaiLeaf leaf)
        {
            if (bonsai != null)
            {
                FieldInfo fallenField = typeof(OrganicBonsaiTree).GetField("fallenLeaves", Fields);
                List<BonsaiLeaf> fallen = fallenField != null ? fallenField.GetValue(bonsai) as List<BonsaiLeaf> : null;
                if (fallen != null) fallen.Remove(leaf);
            }
            if (resources != null && fungiMarket != null)
                TreeJamEcoResources.GiveNutrients(resources, fungiMarket.nutrientsPerDecomposedLeaf);
        }

        private void WarnReflection()
        {
            if (reflectionWarningShown) return;
            reflectionWarningShown = true;
            Debug.LogWarning("Tree Jam: Could not access Bonsai leaf slots. The patch's winter clean-up needs the current OrganicBonsaiTree version. Leaf landing and economy still work.", this);
        }
    }
}
