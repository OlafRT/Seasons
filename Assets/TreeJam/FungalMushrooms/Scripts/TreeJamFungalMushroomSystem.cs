using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TreeJam
{
    /// <summary>
    /// Two physical mushrooms replace the market: each purchase consumes a mushroom,
    /// sends a fungal-mail parcel, and regrows that specific mushroom after a delay.
    /// Four small in-world fungal upgrades modify fruiting, transport, returns and colonies.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(6000)]
    public sealed class TreeJamFungalMushroomSystem : MonoBehaviour
    {
        public TreeJamFungiMarket market;
        public TreeJamMyceliumNetwork network;
        public TreeJamSoilSurface ground;
        public TreeGameState resources;
        public Camera worldCamera;
        [Header("Growth & seasonal behavior")]
        [Min(5f)] public float mushroomRegrowSeconds = 48f;
        [Min(.5f)] public float mushroomBloomSeconds = 3f;
        [Min(.2f)] public float firstFruitDelay = 0.5f;
        [Range(1f,3f)] public float winterRegrowthSlowdown = 1.45f;
        [Min(0f)] public float surfaceOffsetY = .02f;
        public TreeJamSeasonCycle seasons;
        [Header("Trading mushroom appearance (repairable)")]
        [Tooltip("The imported MushroomTrade.png sprite. The repair menu assigns this automatically.")]
        public Sprite mushroomArt;
        [Tooltip("Unlit 2D material used by trading mushrooms.")]
        public Material mushroomMaterial;
        [Tooltip("The visible grass/ground-surface sprite, used for mushroom draw order.")]
        public SpriteRenderer grassSprite;
        [Range(0.05f, 0.5f)] public float visibleBudScale = 0.22f;
        [Header("Existing tooltip style cloned from your editable UI")]
        public Canvas rootCanvas;
        public RectTransform fungalTooltip;
        public Text tooltipTitle;
        public Text tooltipDescription;
        public Text tooltipCosts;
        public Text tooltipHint;
        public float tooltipGap = 22f;
        [Header("Upgrade effects (editable)")]
        [Range(.3f,1f)] public float fasterFruitingMultiplier = .7f;
        [Range(.25f,1f)] public float fasterCourierMultiplier = .7f;
        [Range(1f,2f)] public float betterReturnsMultiplier = 1.2f;
        public bool verboseNotifications = false;

        private TreeJamTradeMushroom[] mushrooms;
        private TreeJamFungalUpgradeNode[] upgradeNodes;
        private readonly HashSet<FungalUpgradeKind> purchased = new HashSet<FungalUpgradeKind>();
        private bool wasConnected;
        private bool previewForced;
        private TreeJamTradeMushroom hoveredMushroom;
        private TreeJamFungalUpgradeNode hoveredUpgrade;

        private void Awake()
        {
            if (market == null) market = FindFirstObjectByType<TreeJamFungiMarket>();
            if (network == null) network = FindFirstObjectByType<TreeJamMyceliumNetwork>();
            if (ground == null) ground = FindFirstObjectByType<TreeJamSoilSurface>();
            if (resources == null) resources = FindFirstObjectByType<TreeGameState>();
            if (worldCamera == null) worldCamera = Camera.main;
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            mushrooms = GetComponentsInChildren<TreeJamTradeMushroom>(true);
            upgradeNodes = GetComponentsInChildren<TreeJamFungalUpgradeNode>(true);
            if (grassSprite == null)
            {
                TreeJamMyceliumPolish polish = FindFirstObjectByType<TreeJamMyceliumPolish>();
                if (polish != null) grassSprite = polish.grassSprite;
            }
            if (mushrooms.Length == 0)
                Debug.LogWarning("Tree Jam mushrooms: no TreeJamTradeMushroom children found. Run the Mushroom Visibility + Daylight repair menu in Edit Mode.", this);
        }

        private void Start()
        {
            if (market != null)
            {
                market.mushroomTradingMode = true;
                market.cooldownAfterDelivery = 0f;
                if (market.openButton != null) market.openButton.gameObject.SetActive(false);
                if (market.panel != null) market.panel.SetActive(false);
            }
            foreach (TreeJamTradeMushroom fruit in mushrooms)
            {
                fruit.established = false;
                fruit.ripe = false;
                fruit.nextGrowthAt = -1f;
                fruit.growingSince = -1f;
            }
            if (fungalTooltip != null) fungalTooltip.gameObject.SetActive(false);
            foreach (TreeJamTradeMushroom fruit in mushrooms) EnsureMushroomArt(fruit);
        }

        private void EnsureMushroomArt(TreeJamTradeMushroom fruit)
        {
            if (fruit == null) return;
            SpriteRenderer renderer = fruit.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                GameObject visual = new GameObject("Mushroom art - replace sprite here");
                visual.transform.SetParent(fruit.transform, false);
                visual.transform.localPosition = new Vector3(0f, .70f, 0f);
                renderer = visual.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null && mushroomArt != null) renderer.sprite = mushroomArt;
            if (mushroomMaterial != null) renderer.sharedMaterial = mushroomMaterial;
            renderer.color = Color.white;
            renderer.enabled = true;
            if (grassSprite != null)
            {
                renderer.sortingLayerID = grassSprite.sortingLayerID;
                renderer.sortingOrder = grassSprite.sortingOrder + 5;
            }
            if (renderer.sprite == null)
                Debug.LogWarning("Tree Jam mushrooms: '" + fruit.name + "' has no Sprite assigned. Use the repair menu to restore MushroomTrade.png.", fruit);
        }

        [ContextMenu("DEBUG - Report trading mushroom visibility (Play Mode)")]
        private void ReportMushroomVisibility()
        {
            if (!Application.isPlaying) return;
            bool connected = network != null && network.Connected;
            Debug.Log("Fungal mushrooms: network connected=" + connected +
                      ", progress=" + (network != null ? network.Progress01.ToString("0.00") : "MISSING") +
                      ", count=" + (mushrooms != null ? mushrooms.Length : 0), this);
            if (mushrooms == null) return;
            foreach (TreeJamTradeMushroom fruit in mushrooms)
            {
                if (fruit == null) continue;
                SpriteRenderer sr = fruit.GetComponentInChildren<SpriteRenderer>(true);
                Debug.Log(fruit.name + " ripe=" + fruit.ripe +
                          " visibleScale=" + fruit.transform.localScale.x.ToString("0.00") +
                          " sprite=" + (sr != null && sr.sprite != null ? sr.sprite.name : "MISSING") +
                          " sorting=" + (sr != null ? sr.sortingOrder.ToString() : "MISSING") +
                          " secondsUntilStart=" + Mathf.Max(0, fruit.nextGrowthAt - Time.time).ToString("0.0"), fruit);
            }
        }

        [ContextMenu("TEST - Grow the trade mushrooms now (Play Mode)")]
        private void TestRipenMushrooms()
        {
            if (!Application.isPlaying || mushrooms == null) return;
            previewForced = true; // Allows a visual test even if the network timer has not finished.
            wasConnected = true; // Do not overwrite the forced bloom timestamps on the next Update.
            foreach (TreeJamTradeMushroom fruit in mushrooms)
            {
                if (fruit.extraColony && !HasUpgrade(FungalUpgradeKind.SporeColony)) continue;
                fruit.established = true;
                fruit.nextGrowthAt = Time.time - mushroomBloomSeconds - 1f;
                fruit.growingSince = Time.time - mushroomBloomSeconds - 1f;
            }
        }

        public bool HasUpgrade(FungalUpgradeKind kind) => purchased.Contains(kind);
        public float CurrentYieldMultiplier =>
            (HasUpgrade(FungalUpgradeKind.BetterReturns) ? betterReturnsMultiplier : 1f) *
            (HasUpgrade(FungalUpgradeKind.GenerousSymbiosis) ? 1.10f : 1f);
        public float CurrentTravelMultiplier =>
            (HasUpgrade(FungalUpgradeKind.CourierHyphae) ? fasterCourierMultiplier : 1f) *
            (HasUpgrade(FungalUpgradeKind.ExpressHyphae) ? .80f : 1f);
        public float CurrentFruitingMultiplier =>
            (HasUpgrade(FungalUpgradeKind.RapidFruiting) ? fasterFruitingMultiplier : 1f) *
            (HasUpgrade(FungalUpgradeKind.FruitingFans) ? .85f : 1f);

        private void Update()
        {
            bool connected = (network != null && network.Connected) || previewForced;
            if (connected && !wasConnected)
            {
                foreach (TreeJamTradeMushroom fruit in mushrooms)
                {
                    fruit.nextGrowthAt = Time.time + firstFruitDelay + (fruit.leftSide ? 0f : .8f);
                    fruit.established = true;
                }
            }
            wasConnected = connected;
            UpdateMushrooms(connected);
            UpdateUpgradeNodes(connected);
            GetHoverAndClick();
        }

        private void UpdateMushrooms(bool connected)
        {
            foreach (TreeJamTradeMushroom fruit in mushrooms)
            {
                if (fruit == null) continue;
                bool exists = connected && (!fruit.extraColony || HasUpgrade(FungalUpgradeKind.SporeColony));
                float x = (network != null && network.treeRoot != null ? network.treeRoot.position.x : 0f)
                          + (fruit.leftSide ? -1f : 1f) * fruit.xOffsetFromTree;
                if (ground != null && ground.TryGetHeight(x, out float y))
                    fruit.transform.position = new Vector3(x,y+surfaceOffsetY,-.05f);
                if (!fruit.established && exists)
                {
                    fruit.established = true;
                    fruit.nextGrowthAt = Time.time + firstFruitDelay;
                }
                // Defend against partially restored scene data, without resetting a valid cooldown.
                if (exists && fruit.nextGrowthAt < 0f && fruit.growingSince < 0f)
                    fruit.nextGrowthAt = Time.time + firstFruitDelay;
                if (!exists)
                {
                    fruit.ripe = false;
                    fruit.transform.localScale = Vector3.zero;
                    if (fruit.clickCollider != null) fruit.clickCollider.enabled = false;
                    continue;
                }
                float growth = 0f;
                if (Time.time >= fruit.nextGrowthAt)
                {
                    if (fruit.growingSince < 0f) fruit.growingSince = Time.time;
                    growth = Mathf.Clamp01((Time.time - fruit.growingSince) / mushroomBloomSeconds);
                    growth = Mathf.SmoothStep(0f,1f,growth);
                }
                fruit.ripe = growth >= .999f;
                // A small visible bud is present immediately after connection, then opens gradually.
                float displayGrowth = fruit.growingSince < 0f ? 0f : Mathf.Lerp(visibleBudScale, 1f, growth);
                fruit.transform.localScale = fruit.visualScale * displayGrowth;
                if (fruit.clickCollider != null) fruit.clickCollider.enabled = fruit.ripe;
            }
        }

        private void UpdateUpgradeNodes(bool connected)
        {
            foreach (TreeJamFungalUpgradeNode node in upgradeNodes)
            {
                if (node == null) continue;
                node.visible = connected && !node.purchased && (!node.hasPrerequisite || HasUpgrade(node.prerequisite));
                foreach (SpriteRenderer sr in node.GetComponentsInChildren<SpriteRenderer>(true))
                    sr.enabled = node.visible;
                Collider2D collider = node.GetComponent<Collider2D>();
                if (collider != null) collider.enabled = node.visible;
            }
        }

        private void GetHoverAndClick()
        {
            hoveredMushroom = null;
            hoveredUpgrade = null;
            if (worldCamera == null) return;
            Vector2 screen;
            bool click;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return;
            screen = Mouse.current.position.ReadValue();
            click = Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            screen = Input.mousePosition;
            click = Input.GetMouseButtonDown(0);
#else
            return;
#endif
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,0));
            float closest = float.MaxValue;
            foreach (Collider2D col in Physics2D.OverlapPointAll(new Vector2(world.x,world.y)))
            {
                TreeJamTradeMushroom fruit = col.GetComponent<TreeJamTradeMushroom>();
                if (fruit != null && fruit.ripe && fruit.clickCollider != null && fruit.clickCollider.enabled)
                {
                    float distance = (fruit.transform.position - new Vector3(world.x,world.y,fruit.transform.position.z)).sqrMagnitude;
                    if (distance < closest) {closest=distance; hoveredMushroom=fruit; hoveredUpgrade=null;}
                }
                TreeJamFungalUpgradeNode node = col.GetComponent<TreeJamFungalUpgradeNode>();
                if (node != null && node.visible)
                {
                    float distance = (node.transform.position - new Vector3(world.x,world.y,node.transform.position.z)).sqrMagnitude;
                    if (distance < closest) {closest=distance; hoveredUpgrade=node; hoveredMushroom=null;}
                }
            }
            if (click)
            {
                if (hoveredMushroom != null) TryHarvest(hoveredMushroom);
                else if (hoveredUpgrade != null) TryPurchaseUpgrade(hoveredUpgrade);
            }
        }

        private void TryHarvest(TreeJamTradeMushroom fruit)
        {
            if (market == null || !fruit.ripe) return;
            if (!market.TryMushroomTrade(fruit.payEnergy, fruit.leftSide, CurrentYieldMultiplier, CurrentTravelMultiplier))
            {
                if (verboseNotifications) TreeJamHUD.Notify("The fungal trade is not ready, or there is not enough room in storage.");
                return;
            }
            fruit.ripe = false;
            fruit.growingSince = -1f;
            float seasonal = seasons != null && seasons.CurrentSeason == TreeSeason.Winter
                ? (HasUpgrade(FungalUpgradeKind.WinterWeb) ? 1f : winterRegrowthSlowdown) : 1f;
            fruit.nextGrowthAt = Time.time + mushroomRegrowSeconds * CurrentFruitingMultiplier * seasonal;
            if (fruit.clickCollider != null) fruit.clickCollider.enabled = false;
            fruit.transform.localScale = Vector3.zero;
        }

        private void TryPurchaseUpgrade(TreeJamFungalUpgradeNode node)
        {
            if (!node.visible || resources == null) return;
            if (!TreeJamEcoResources.TryTrade(resources,-node.energyCost,-node.nutrientCost))
            {
                TreeJamHUD.Notify("Not enough energy or nutrients.");
                return;
            }
            node.purchased = true;
            purchased.Add(node.upgrade);
            TreeJamHUD.Notify("Mycelium adapted: " + node.displayName);
        }

        private void LateUpdate()
        {
            if (fungalTooltip == null) return;
            bool any = hoveredMushroom != null || hoveredUpgrade != null;
            fungalTooltip.gameObject.SetActive(any);
            if (!any || rootCanvas == null || worldCamera == null) return;
            Vector3 worldPosition;
            if (hoveredMushroom != null)
            {
                bool paidEnergy = hoveredMushroom.payEnergy;
                float spent = paidEnergy ? market.spendEnergy : market.spendNutrients;
                float gained = (paidEnergy ? market.receiveNutrients : market.receiveEnergy)*CurrentYieldMultiplier;
                if (tooltipTitle != null) tooltipTitle.text = paidEnergy ? "FUNGAL EXCHANGE · NUTRIENTS" : "FUNGAL EXCHANGE · ENERGY";
                if (tooltipDescription != null) tooltipDescription.text = "Send resources through the mycelium. The return arrives after the courier's journey.";
                if (tooltipCosts != null) tooltipCosts.text = string.Format("{0:0.#} {1}  →  {2:0.#} {3}",spent,paidEnergy?"ENERGY":"NUTRIENTS",gained,paidEnergy?"NUTRIENTS":"ENERGY");
                if (tooltipHint != null) tooltipHint.text = market.IsDelivering ? "COURIER IN TRANSIT" :
                    market.CanMushroomTrade(paidEnergy,CurrentYieldMultiplier) ? "CLICK TO EXCHANGE" : "NEED RESOURCES OR STORAGE";
                worldPosition = hoveredMushroom.transform.position;
            }
            else
            {
                if (tooltipTitle != null) tooltipTitle.text = hoveredUpgrade.displayName;
                if (tooltipDescription != null) tooltipDescription.text = hoveredUpgrade.description;
                if (tooltipCosts != null) tooltipCosts.text = string.Format("ENERGY {0:0} · NUTRIENTS {1:0}", hoveredUpgrade.energyCost, hoveredUpgrade.nutrientCost);
                if (tooltipHint != null) tooltipHint.text = resources != null && resources.Energy >= hoveredUpgrade.energyCost && resources.Nutrients >= hoveredUpgrade.nutrientCost ? "CLICK TO GROW" : "NOT ENOUGH RESOURCES";
                worldPosition = hoveredUpgrade.transform.position;
            }
            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            if (canvasRect == null) return;
            Vector3 screenPosition=worldCamera.WorldToScreenPoint(worldPosition);
            Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 local)) return;
            float w=fungalTooltip.rect.width, h=fungalTooltip.rect.height;
            float minX=canvasRect.rect.xMin+12f, maxX=canvasRect.rect.xMax-12f-w;
            float preferredX=local.x+tooltipGap+w>canvasRect.rect.xMax ? local.x-tooltipGap-w : local.x+tooltipGap;
            float x=Mathf.Clamp(preferredX,Mathf.Min(minX,maxX),Mathf.Max(minX,maxX));
            float y=Mathf.Clamp(local.y+h*.5f,canvasRect.rect.yMin+12f+h,canvasRect.rect.yMax-12f);
            fungalTooltip.anchoredPosition=new Vector2(x,y);
        }
    }
}
