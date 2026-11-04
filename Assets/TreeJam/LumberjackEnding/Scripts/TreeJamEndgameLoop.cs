using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam
{
    /// <summary>
    /// Endgame: all permanently reachable upgrades purchased -> walk in -> chop ->
    /// tree falls, roots and stump remain -> black -> reload -> wider trunk.
    /// Scene reload intentionally resets the existing tree, fungi and resource systems
    /// without changing their scripts or private upgrade state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamEndgameLoop : MonoBehaviour
    {
        [Header("Existing scene")]
        public OrganicBonsaiTree tree;
        public TreeGameState resources;
        public TreeJamSoilSurface soil;
        public TreeJamLumberjackPuppet lumberjack;
        public Camera worldCamera;

        [Header("When to trigger")]
        public bool autoTriggerWhenFullyGrown = true;
        public bool includeFungalUpgrades = true;
        [Min(1f)] public float settleAfterFinalPurchaseSeconds = 9f;

        [Header("Cinematic")]
        public float startingSide = -1f;
        [Min(.1f)] public float approachDistance = .61f;
        [Min(.1f)] public float approachSeconds = 4f;
        [Header("Walking entrance")]
        [Min(0f)] public float offscreenEntryPadding = .55f;
        [Min(.25f)] public float walkingUnitsPerSecond = 1.75f;
        [Range(1, 8)] public int axeStrikes = 3;
        [Min(.2f)] public float secondsPerStrike = 1.15f;
        [Header("Axe contact with trunk (world units)")]
        [Min(.25f)] public float chopContactHeight = .69f;
        [Range(0f, .4f)] public float chopBarkOffset = .10f;
        public bool showWoodChips = true;
        [Min(.2f)] public float treeFallSeconds = 2.45f;
        [Range(45f, 105f)] public float fallingAngle = 84f;
        [Min(.1f)] public float fadeSeconds = 1.6f;
        [Min(0f)] public float blackHoldSeconds = 2.8f;
        [Min(.1f)] public float newSproutRevealSeconds = 3f;

        [Header("Rebirth: incremental trunk thickening")]
        [Min(.5f)] public float firstTrunkThickness = 1f;
        [Range(0f, .40f)] public float addedThicknessPerCycle = .11f;
        [Range(.6f, 1.8f)] public float maximumTrunkThickness = 1.8f;
        public bool saveRebirthCountBetweenSessions = true;
        public bool showOldStumpOnRebirth = true;

        [Header("Optional sounds")]
        public AudioClip chopSound;
        public AudioClip treeCrashSound;
        public AudioSource audioSource;

        public int RebirthCount { get; private set; }
        public bool EndingPlaying { get; private set; }

        private float lastPurchaseTime;
        private float nextCheckTime;
        private UpgradeOrb[] allOrbs;
        private TreeJamFungalUpgradeNode[] fungalNodes;
        private const string RebirthKey = "TreeJam_Lumberjack_Rebirths_v1";

        private void Awake()
        {
            if (tree == null) tree = FindFirstObjectByType<OrganicBonsaiTree>();
            if (resources == null) resources = FindFirstObjectByType<TreeGameState>();
            if (soil == null) soil = FindFirstObjectByType<TreeJamSoilSurface>();
            if (worldCamera == null) worldCamera = Camera.main;
            RebirthCount = saveRebirthCountBetweenSessions ? PlayerPrefs.GetInt(RebirthKey, 0) : TreeJamRebirthState.SessionRebirthCount;
            if (tree != null)
                tree.trunkThickness = Mathf.Clamp(firstTrunkThickness + RebirthCount * addedThicknessPerCycle,
                    .5f, maximumTrunkThickness);
        }

        private void Start()
        {
            if (lumberjack != null) lumberjack.gameObject.SetActive(false);
            if (RebirthCount > 0 && showOldStumpOnRebirth) StartCoroutine(MakeStumpAfterTreeBuild());
            GatherOrbs();
            lastPurchaseTime = Time.time;
            if (resources != null) resources.UpgradePurchased += OnPurchased;
        }

        private void OnDestroy()
        {
            if (resources != null) resources.UpgradePurchased -= OnPurchased;
        }

        private void OnPurchased(UpgradeDefinition _) => lastPurchaseTime = Time.time;

        private void GatherOrbs()
        {
            allOrbs = FindObjectsByType<UpgradeOrb>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            fungalNodes = FindObjectsByType<TreeJamFungalUpgradeNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void Update()
        {
            if (!autoTriggerWhenFullyGrown || EndingPlaying || resources == null || tree == null) return;
            if (Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + .65f;
            if (Time.time - lastPurchaseTime < settleAfterFinalPurchaseSeconds) return;
            if (IsComplete()) StartCoroutine(Ending());
        }

        /// <summary>Respect mutually exclusive paths: their descendants cannot be purchased.</summary>
        private bool PermanentlyUnreachable(UpgradeDefinition upgrade, HashSet<UpgradeDefinition> visiting)
        {
            if (upgrade == null) return false;
            if (resources.IsPurchased(upgrade)) return false;
            if (resources.IsExcludedByChoice(upgrade)) return true;
            if (!visiting.Add(upgrade)) return false;
            if (upgrade.prerequisites != null)
                foreach (UpgradeDefinition prerequisite in upgrade.prerequisites)
                    if (prerequisite != null && PermanentlyUnreachable(prerequisite, visiting))
                    {
                        visiting.Remove(upgrade);
                        return true;
                    }
            visiting.Remove(upgrade);
            return false;
        }

        public bool IsComplete()
        {
            if (allOrbs == null) GatherOrbs();
            int counted = 0;
            foreach (UpgradeOrb orb in allOrbs)
            {
                if (orb == null || !orb.gameObject.activeInHierarchy || orb.definition == null) continue;
                counted++;
                if (!resources.IsPurchased(orb.definition) &&
                    !PermanentlyUnreachable(orb.definition, new HashSet<UpgradeDefinition>())) return false;
            }
            if (counted < 5) return false; // Never trigger in an incomplete installer scene.
            if (includeFungalUpgrades && fungalNodes != null)
                foreach (TreeJamFungalUpgradeNode node in fungalNodes)
                    if (node != null && node.gameObject.activeInHierarchy && !node.purchased) return false;
            return true;
        }

        [ContextMenu("TEST - Trigger lumberjack ending (Play Mode)")]
        public void TestEnding()
        {
            if (Application.isPlaying && !EndingPlaying) StartCoroutine(Ending());
        }

        [ContextMenu("RESET - Clear stored rebirth count")]
        public void ResetSavedRebirths()
        {
            PlayerPrefs.DeleteKey(RebirthKey);
            PlayerPrefs.Save();
            TreeJamRebirthState.SessionRebirthCount = 0;
            Debug.Log("Tree Jam: Rebirth counter reset. Restart Play Mode to see the initial tree.", this);
        }

        private IEnumerator Ending()
        {
            EndingPlaying = true;
            foreach (TreeJamMouse mouse in FindObjectsByType<TreeJamMouse>(FindObjectsSortMode.None)) mouse.enabled = false;
            foreach (TreeJamUI hud in FindObjectsByType<TreeJamUI>(FindObjectsSortMode.None)) hud.gameObject.SetActive(false);
            foreach (UpgradeOrb orb in allOrbs)
                if (orb != null && orb.orbGraphics != null) orb.orbGraphics.SetActive(false);

            if (lumberjack == null) { Debug.LogError("Endgame: lumberjack puppet reference is missing.", this); EndingPlaying = false; yield break; }
            Vector3 trunkPosition = tree.transform.position;
            float groundY = soil != null ? soil.HeightOr(trunkPosition.x, trunkPosition.y) : trunkPosition.y;
            float sign = startingSide < 0f ? -1f : 1f;
            // The current puppet's arms can only reach the tree if he stands
            // near the trunk. Clamp older scenes with the original 1.3-unit
            // stand-off distance so the axe cannot chop empty air.
            float nearX = trunkPosition.x + sign * Mathf.Min(approachDistance, .70f);
            lumberjack.gameObject.SetActive(true);
            Vector3 scale = lumberjack.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (sign < 0f ? 1f : -1f);
            lumberjack.transform.localScale = scale;
            lumberjack.PoseIdle();

            // Begin entirely outside the ACTIVE camera's horizontal view, not
            // a fixed distance from the tree. Works with the gradual zoom camera.
            if (worldCamera == null) worldCamera = Camera.main;
            float farX = nearX + sign * Mathf.Max(2.5f, approachDistance * 2f);
            if (worldCamera != null && worldCamera.orthographic)
            {
                float halfViewWidth = worldCamera.orthographicSize * worldCamera.aspect;
                float viewEdge = worldCamera.transform.position.x +
                    sign * halfViewWidth;
                float halfActorWidth = GetLumberjackHalfWidth(lumberjack);
                farX = viewEdge + sign * (halfActorWidth + offscreenEntryPadding);
            }
            Vector3 startPosition = new Vector3(farX, Ground(farX, groundY), 0f);
            Vector3 endPosition = new Vector3(nearX, Ground(nearX, groundY), 0f);
            lumberjack.transform.position = startPosition;

            float distance = Mathf.Abs(farX - nearX);
            // The first/last 10% of movement gently ease in/out, but the rest
            // remains nearly constant speed so the footsteps read naturally.
            float walkDuration = Mathf.Max(approachSeconds,
                distance / (Mathf.Max(.25f, walkingUnitsPerSecond) * .90f));
            float walk = 0f;
            while (walk < walkDuration)
            {
                walk += Time.deltaTime;
                float t = Mathf.Clamp01(walk / walkDuration);
                float easedTravel = SoftStartStop(t);
                float x = Mathf.Lerp(farX, nearX, easedTravel);
                lumberjack.transform.position = new Vector3(x, Ground(x, groundY), 0f);
                float edge = Mathf.Min(t, 1f - t);
                float gaitBlend = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(edge / .10f));
                lumberjack.PoseWalking(Mathf.Abs(x - farX), gaitBlend);
                yield return null;
            }
            lumberjack.transform.position = endPosition;
            lumberjack.PoseIdle();
            yield return new WaitForSeconds(.35f);
            // The cutting edge, NOT the arm rotation, owns the impact point.
            Vector3 hitPoint = new Vector3(
                trunkPosition.x + sign * chopBarkOffset,
                groundY + chopContactHeight, 0f);
            // Authored poses bypass the old two-hand IK, avoiding elbow flipping.
            TreeJamAuthoredChop authoredChop = lumberjack.GetComponent<TreeJamAuthoredChop>();
            bool useAuthored = authoredChop != null && authoredChop.IsReady;
            if (useAuthored) authoredChop.PreviewPose(0);
            else lumberjack.PrepareChopping(hitPoint);
            yield return new WaitForSeconds(.18f);
            GameObject falling = null;
            for (int i = 0; i < Mathf.Max(1, axeStrikes); i++)
            {
                bool finalStrike = i == Mathf.Max(1, axeStrikes) - 1;
                float elapsed = 0f;
                bool impactPlayed = false;
                while (elapsed < secondsPerStrike)
                {
                    elapsed += Time.deltaTime;
                    float strikeT = Mathf.Clamp01(elapsed / Mathf.Max(.2f, secondsPerStrike));
                    if (useAuthored) authoredChop.PoseAt(strikeT);
                    else lumberjack.PoseChop(strikeT);
                    float hitTime = useAuthored ? authoredChop.impactTime : TreeJamLumberjackPuppet.ContactTime;
                    if (!impactPlayed && strikeT >= hitTime)
                    {
                        impactPlayed = true;
                        Play(chopSound);
                        if (showWoodChips) StartCoroutine(ScatterWoodChips(hitPoint));
#if UNITY_EDITOR
                        Debug.DrawLine(lumberjack.CurrentBladeEdgeWorld(), hitPoint,
                            Color.yellow, 1f);
#endif
                        if (finalStrike)
                        {
                            // Fell the tree ON the final blade impact, not after
                            // recovering to idle or completing another strike cycle.
                            falling = SnapshotAndSeverTree(tree);
                            break;
                        }
                    }
                    yield return null;
                }
                if (finalStrike) break;
                if (useAuthored) authoredChop.PoseAt(1f);
                else lumberjack.PoseChop(1f); // Earlier chops recover before the next wind-up.
                yield return new WaitForSeconds(.12f);
            }

            // Intentionally keep the blade at the last impact pose while the
            // trunk begins to topple. The scene reload resets the puppet later.
            if (falling == null) falling = SnapshotAndSeverTree(tree);
            if (falling != null)
            {
                float fall = 0f;
                bool crashPlayed = false;
                float direction = sign < 0 ? -1f : 1f;
                while (fall < treeFallSeconds)
                {
                    fall += Time.deltaTime;
                    float p = Mathf.Clamp01(fall / Mathf.Max(.2f, treeFallSeconds));
                    float eased = 1f - Mathf.Pow(1f - p, 2.5f);
                    falling.transform.localRotation = Quaternion.Euler(0f,0f,direction * fallingAngle * eased);
                    if (!crashPlayed && p >= .90f) { Play(treeCrashSound); crashPlayed = true; }
                    yield return null;
                }
            }
            yield return new WaitForSeconds(.65f);
            TreeJamEndgameTransition transition = TreeJamEndgameTransition.Create();
            yield return transition.FadeToBlack(fadeSeconds);
            int next = RebirthCount + 1;
            TreeJamRebirthState.SessionRebirthCount = next;
            if (saveRebirthCountBetweenSessions) { PlayerPrefs.SetInt(RebirthKey,next);PlayerPrefs.Save(); }
            transition.BeginRebirth(SceneManager.GetActiveScene().path, blackHoldSeconds, fadeSeconds, newSproutRevealSeconds);
        }

        private static float SoftStartStop(float t)
        {
            t = Mathf.Clamp01(t);
            const float edge = .10f;
            const float normalizer = 1f / (1f - edge);
            if (t < edge) return .5f * t * t / edge * normalizer;
            if (t > 1f - edge)
            {
                float remaining = 1f - t;
                return 1f - .5f * remaining * remaining / edge * normalizer;
            }
            return (t - .5f * edge) * normalizer;
        }

        private static float GetLumberjackHalfWidth(TreeJamLumberjackPuppet puppet)
        {
            float width = .45f;
            foreach (SpriteRenderer r in puppet.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r == null || r.sprite == null) continue;
                // World-space bounds include all currently attached sprite parts.
                // Account for their offsets relative to the actor's root.
                float left = Mathf.Abs(r.bounds.min.x - puppet.transform.position.x);
                float right = Mathf.Abs(r.bounds.max.x - puppet.transform.position.x);
                width = Mathf.Max(width, left, right);
            }
            return width;
        }

        private void OnDrawGizmosSelected()
        {
            if (tree == null) return;
            Vector3 origin = tree.transform.position;
            float y = soil != null ? soil.HeightOr(origin.x, origin.y) : origin.y;
            float side = startingSide < 0f ? -1f : 1f;
            Gizmos.color = new Color(1f, .72f, .23f, .95f);
            Gizmos.DrawWireSphere(new Vector3(origin.x + side * chopBarkOffset,
                y + chopContactHeight, 0f), .075f);
        }

        // Small visual confirmation at each CONTACT frame. Purely cosmetic.
        private IEnumerator ScatterWoodChips(Vector3 hit)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) yield break;
            Material mat = new Material(shader);
            var chips = new LineRenderer[5];
            var velocities = new Vector3[chips.Length];
            for (int i = 0; i < chips.Length; i++)
            {
                GameObject chip = new GameObject("Bark chip (temporary)");
                chip.transform.position = hit;
                LineRenderer line = chip.AddComponent<LineRenderer>();
                line.sharedMaterial = mat;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.sortingOrder = 35;
                line.widthMultiplier = UnityEngine.Random.Range(.022f, .044f);
                line.startColor = new Color(.75f, .42f, .22f);
                line.endColor = new Color(.48f, .28f, .14f);
                chips[i] = line;
                velocities[i] = new Vector3(UnityEngine.Random.Range(-.65f, .65f),
                    UnityEngine.Random.Range(.5f, 1.35f), 0f);
            }
            float elapsed = 0f;
            const float duration = .48f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < chips.Length; i++)
                {
                    if (chips[i] == null) continue;
                    Vector3 p = hit + velocities[i] * elapsed +
                                Vector3.down * (1.8f * elapsed * elapsed);
                    chips[i].SetPosition(0, p);
                    chips[i].SetPosition(1, p + new Vector3(.025f, -.055f, 0f));
                    Color c = new Color(.75f, .42f, .22f, 1f - t);
                    chips[i].startColor = c;
                    chips[i].endColor = c;
                }
                yield return null;
            }
            foreach (LineRenderer line in chips)
                if (line != null) Destroy(line.gameObject);
            Destroy(mat);
        }

        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.PlayOneShot(clip);
        }

        private float Ground(float x, float fallback) => soil != null ? soil.HeightOr(x, fallback) : fallback;

        private IEnumerator MakeStumpAfterTreeBuild()
        {
            yield return null; // OrganicBonsaiTree.Start creates its runtime wood material.
            CreateOldStump();
        }

        private void CreateOldStump()
        {
            GameObject old = new GameObject("OLD STUMP - new growth emerges here");
            old.transform.SetParent(transform, false);
            old.transform.position = tree.transform.TransformPoint(new Vector3(0f,-1f,0f));
            LineRenderer bark = old.AddComponent<LineRenderer>();
            bark.useWorldSpace = false;
            bark.sharedMaterial = tree.woodMaterial;
            if (bark.sharedMaterial == null)
                bark.sharedMaterial = tree.GetComponentInChildren<LineRenderer>()?.sharedMaterial;
            if (bark.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) bark.material = new Material(shader);
            }
            bark.startColor = bark.endColor = tree.barkColor;
            bark.numCapVertices = 8;
            bark.sortingOrder = 4;
            bark.positionCount = 2;
            bark.SetPosition(0,Vector3.zero);
            bark.SetPosition(1,new Vector3(.012f,.17f,0f));
            bark.widthMultiplier = .37f * tree.trunkThickness;
            bark.widthCurve = AnimationCurve.Linear(0f,1.15f,1f,1f);
        }

        private static GameObject SnapshotAndSeverTree(OrganicBonsaiTree bonsai)
        {
            Transform source = bonsai.transform.Find("PROCEDURAL BONSAI (runtime)");
            if (source == null) { Debug.LogWarning("Endgame: procedural tree visuals not found.", bonsai); return null; }
            GameObject pivot = new GameObject("FALLING TREE - cinematic");
            pivot.transform.position = source.position;
            pivot.transform.rotation = Quaternion.identity;
            GameObject canopy = UnityEngine.Object.Instantiate(source.gameObject, pivot.transform);
            canopy.name = "Falling canopy";
            canopy.transform.localPosition = Vector3.zero;
            canopy.transform.localRotation = Quaternion.identity;
            canopy.transform.localScale = source.localScale;
            foreach (MonoBehaviour behaviour in canopy.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (LineRenderer line in canopy.GetComponentsInChildren<LineRenderer>(true))
                if (line.sortingOrder <= 2) line.enabled = false; // underground roots stay planted
            foreach (LineRenderer line in source.GetComponentsInChildren<LineRenderer>(true))
                if (line.sortingOrder > 2) line.enabled = false;
            foreach (SpriteRenderer leaf in source.GetComponentsInChildren<SpriteRenderer>(true)) leaf.enabled = false;
            bonsai.enabled = false; // Don't re-enable hidden original leaf sprites during the cutscene.

            GameObject stump = new GameObject("STUMP - left after felling");
            stump.transform.position = source.position;
            LineRenderer bark = stump.AddComponent<LineRenderer>();
            bark.sharedMaterial = bonsai.woodMaterial != null ? bonsai.woodMaterial :
                source.GetComponentInChildren<LineRenderer>()?.sharedMaterial;
            bark.startColor = bark.endColor = bonsai.barkColor;
            bark.useWorldSpace = false;
            bark.alignment = LineAlignment.View;
            bark.sortingOrder = 5;
            bark.numCapVertices = 8;
            bark.positionCount = 3;
            bark.SetPosition(0, new Vector3(0f,0f,0f));
            bark.SetPosition(1, new Vector3(0f,.09f,0f));
            bark.SetPosition(2, new Vector3(.015f,.22f,0f));
            bark.widthMultiplier = .34f * bonsai.trunkThickness;
            bark.widthCurve = AnimationCurve.Linear(0f,1.15f,1f,.96f);
            return pivot;
        }
    }

    internal static class TreeJamRebirthState
    {
        internal static int SessionRebirthCount;
    }
}
