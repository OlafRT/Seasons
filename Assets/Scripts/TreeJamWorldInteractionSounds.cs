using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TreeJam
{
    /// <summary>
    /// World-space counterpart to UIButtonSound. Add ONCE to a scene GameObject.
    /// It observes the same 2D colliders used by the existing orb / mushroom scripts;
    /// it never processes clicks or buys anything itself.
    ///
    /// Pre-Update: remember which clickable object the player hovered/clicked.
    /// LateUpdate: check whether the existing interaction ACTUALLY succeeded.
    /// This avoids playing a purchase sound for an unaffordable node or failed trade.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class TreeJamWorldInteractionSounds : MonoBehaviour
    {
        [Header("Assign your sound clips")]
        public AudioClip hoverSound;
        public AudioClip upgradePurchasedSound;
        public AudioClip fungalUpgradePurchasedSound;
        public AudioClip mushroomTradeSound;
        public AudioClip unsuccessfulClickSound;

        [Header("Volumes")]
        [Range(0f, 1f)] public float hoverVolume = .36f;
        [Range(0f, 1f)] public float purchasedVolume = .65f;
        [Range(0f, 1f)] public float fungalUpgradeVolume = .65f;
        [Range(0f, 1f)] public float tradeVolume = .65f;
        [Range(0f, 1f)] public float unsuccessfulVolume = .28f;

        [Header("Optional variation")]
        public bool randomizePitch = true;
        public Vector2 hoverPitchRange = new Vector2(.98f, 1.02f);
        public Vector2 clickPitchRange = new Vector2(.96f, 1.04f);

        [Header("References (optional, auto-found)")]
        public Camera worldCamera;

        private enum Kind { None, TreeUpgrade, FungalUpgrade, Mushroom }

        private struct Target
        {
            public Kind kind;
            public UpgradeOrb orb;
            public TreeJamFungalUpgradeNode fungalNode;
            public TreeJamTradeMushroom mushroom;
            public Component Component
            {
                get
                {
                    switch (kind)
                    {
                        case Kind.TreeUpgrade: return orb;
                        case Kind.FungalUpgrade: return fungalNode;
                        case Kind.Mushroom: return mushroom;
                        default: return null;
                    }
                }
            }
        }

        private Target hovered;
        private Target pendingClick;
        private bool checkClickInLateUpdate;
        private static AudioSource sharedPlayer;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void Update()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (!ReadPointer(out Vector2 screen, out bool clicked) || worldCamera == null)
            {
                hovered = default(Target);
                return;
            }

            Target next = IsPointerOverBlockingUI() ? default(Target) : PickTarget(screen);
            if (next.Component != hovered.Component)
            {
                if (next.Component != null)
                    Play(hoverSound, hoverVolume, hoverPitchRange);
            }
            hovered = next;

            if (clicked && next.Component != null)
            {
                // Check only after the actual gameplay interaction scripts have run.
                pendingClick = next;
                checkClickInLateUpdate = true;
            }
        }

        private void LateUpdate()
        {
            if (!checkClickInLateUpdate) return;
            checkClickInLateUpdate = false;

            bool successful = false;
            switch (pendingClick.kind)
            {
                case Kind.TreeUpgrade:
                    UpgradeOrb orb = pendingClick.orb;
                    successful = orb != null && orb.gameState != null &&
                                 orb.gameState.IsPurchased(orb.definition);
                    if (successful) Play(upgradePurchasedSound, purchasedVolume, clickPitchRange);
                    break;

                case Kind.FungalUpgrade:
                    TreeJamFungalUpgradeNode node = pendingClick.fungalNode;
                    successful = node != null && node.purchased;
                    if (successful) Play(fungalUpgradePurchasedSound != null
                        ? fungalUpgradePurchasedSound : upgradePurchasedSound,
                        fungalUpgradeVolume, clickPitchRange);
                    break;

                case Kind.Mushroom:
                    TreeJamTradeMushroom mushroom = pendingClick.mushroom;
                    // The existing fungus controller sets ripe=false and schedules
                    // a new growth time ONLY AFTER a successful trade.
                    successful = mushroom != null && !mushroom.ripe &&
                                 mushroom.nextGrowthAt > Time.time;
                    if (successful) Play(mushroomTradeSound, tradeVolume, clickPitchRange);
                    break;
            }

            if (!successful) Play(unsuccessfulClickSound, unsuccessfulVolume, clickPitchRange);
            pendingClick = default(Target);
        }

        private Target PickTarget(Vector2 screen)
        {
            Vector3 w = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            Collider2D[] hits = Physics2D.OverlapPointAll(new Vector2(w.x, w.y));
            float bestDistance = float.PositiveInfinity;
            Target best = default(Target);

            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.enabled) continue;
                Target candidate = default(Target);
                TreeJamTradeMushroom mushroom = hit.GetComponent<TreeJamTradeMushroom>();
                TreeJamFungalUpgradeNode fungalNode = hit.GetComponent<TreeJamFungalUpgradeNode>();
                UpgradeOrb orb = hit.GetComponent<UpgradeOrb>();

                if (mushroom != null && mushroom.ripe)
                {
                    candidate.kind = Kind.Mushroom;
                    candidate.mushroom = mushroom;
                }
                else if (fungalNode != null && fungalNode.visible)
                {
                    candidate.kind = Kind.FungalUpgrade;
                    candidate.fungalNode = fungalNode;
                }
                else if (orb != null && orb.IsVisible)
                {
                    candidate.kind = Kind.TreeUpgrade;
                    candidate.orb = orb;
                }
                else continue;

                Component component = candidate.Component;
                float distance = ((Vector2)component.transform.position - new Vector2(w.x, w.y)).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        private static bool ReadPointer(out Vector2 position, out bool clicked)
        {
            position = default(Vector2);
            clicked = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            position = Mouse.current.position.ReadValue();
            clicked = Mouse.current.leftButton.wasPressedThisFrame;
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            clicked = Input.GetMouseButtonDown(0);
            return true;
#else
            return false;
#endif
        }

        private static bool IsPointerOverBlockingUI()
        {
            // Optional: if the scene has an EventSystem, ordinary menus can shield
            // world objects beneath them. The game's editable resource art may not
            // be raycast targets; it doesn't need to be for the separate tooltips.
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void Play(AudioClip clip, float volume, Vector2 pitchRange)
        {
            if (clip == null || volume <= 0f) return;
            if (sharedPlayer == null)
            {
                GameObject go = new GameObject("Tree Jam - UI one-shot audio");
                DontDestroyOnLoad(go);
                sharedPlayer = go.AddComponent<AudioSource>();
                sharedPlayer.playOnAwake = false;
                sharedPlayer.loop = false;
                sharedPlayer.spatialBlend = 0f;
                sharedPlayer.ignoreListenerPause = true;
                sharedPlayer.volume = 1f;
            }
            sharedPlayer.pitch = randomizePitch
                ? Random.Range(Mathf.Min(pitchRange.x, pitchRange.y),
                               Mathf.Max(pitchRange.x, pitchRange.y))
                : 1f;
            sharedPlayer.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void OnDisable()
        {
            hovered = default(Target);
            pendingClick = default(Target);
            checkClickInLateUpdate = false;
        }
    }
}
