using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TreeJam
{
    /// <summary>
    /// Fungal deliveries follow the actual, fully-grown mycelium LineRenderer path.
    /// Replaces the instant market. Preserves all serialized fields from the old version.
    /// Every transaction sends exactly one parcel and has a travel time and cooldown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamFungiMarket : MonoBehaviour
    {
        // DO NOT RENAME THESE: existing scene references depend on these field names.
        public TreeJamMyceliumNetwork network;
        public TreeGameState resources;
        public OrganicBonsaiTree bonsai;
        [Header("Editable Canvas elements")]
        public Button openButton;
        public GameObject panel;
        public Button closeButton;
        public Button energyToNutrientButton;
        public Button nutrientToEnergyButton;
        public Text statusText;
        [Header("Trade rates (one parcel)")]
        [Min(.1f)] public float spendEnergy = 22f;
        [Min(.1f)] public float receiveNutrients = 9f;
        [Min(.1f)] public float spendNutrients = 18f;
        [Min(.1f)] public float receiveEnergy = 7f;
        [Header("Autumn / winter recycling")]
        [Min(0f)] public float nutrientsPerDecomposedLeaf = .4f;

        [Header("Fungal mail - timing")]
        [Min(.2f)] public float secondsOutbound = 5f;
        [Min(0f)] public float secondsAtDestination = 1.5f;
        [Min(.2f)] public float secondsInbound = 5f;
        [Min(0f)] public float cooldownAfterDelivery = 18f;
        [Header("Fungal mail - look")]
        [Range(.08f, .6f)] public float packetDiameter = .28f;
        public Color energyParcelColor = new Color(1f, .83f, .35f, 1f);
        public Color nutrientParcelColor = new Color(.55f, 1f, .72f, 1f);
        [Tooltip("Send alternating parcels to the left and right fungal endpoints.")]
        public bool alternateSides = true;
        [Header("Mushroom trading (new mode)")]
        [Tooltip("When enabled, trades start by clicking the mushrooms, not the old market panel.")]
        public bool mushroomTradingMode = false;
        [Tooltip("The shaped main mycelium road. Optional fallback is the original network paths.")]
        public TreeJamFungalRoute fungalRoute;
        [Range(.15f,1f)] public float courierScale = .35f;
        public bool IsDelivering => deliveryInProgress;
        public bool IsNetworkReady => network != null && network.Connected;


        private bool hadConnection;
        private bool subscribed;
        private bool deliveryInProgress;
        private bool nextDeliveryToLeft = true;
        private float readyTime;
        private Coroutine deliveryRoutine;
        private GameObject parcelVisual;
        private SpriteRenderer parcelCore;
        private SpriteRenderer parcelHalo;
        private static Sprite parcelSprite;
        private static Material parcelMaterial;

        private void Start()
        {
            if (network == null) network = FindFirstObjectByType<TreeJamMyceliumNetwork>();
            if (resources == null) resources = FindFirstObjectByType<TreeGameState>();
            if (bonsai == null) bonsai = FindFirstObjectByType<OrganicBonsaiTree>();
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (fungalRoute == null) fungalRoute = FindFirstObjectByType<TreeJamFungalRoute>();
            // Keep the close button reference for backward compatibility, but hide its artwork.
            // The MARKET button now toggles the panel; trades close it automatically.
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
                closeButton.gameObject.SetActive(false);
            }
            if (energyToNutrientButton != null) energyToNutrientButton.onClick.AddListener(TradeEnergy);
            if (nutrientToEnergyButton != null) nutrientToEnergyButton.onClick.AddListener(TradeNutrients);
            if (panel != null) panel.SetActive(false);
            if (openButton != null) openButton.gameObject.SetActive(false);
            if (bonsai != null)
            {
                bonsai.LeafDecomposed += OnDecomposed;
                subscribed = true;
            }
            RefreshTradeLabels();
        }

        private void Update()
        {
            bool connected = network != null && network.Connected;
            if (connected != hadConnection)
            {
                hadConnection = connected;
                if (openButton != null) openButton.gameObject.SetActive(connected && !mushroomTradingMode);
                if (!connected) Close();
            }
            bool ready = connected && !deliveryInProgress && Time.time >= readyTime;
            if (mushroomTradingMode)
            {
                if (openButton != null && openButton.gameObject.activeSelf) openButton.gameObject.SetActive(false);
                if (panel != null && panel.activeSelf) Close();
            }
            if (openButton != null) openButton.interactable = ready && !mushroomTradingMode;
            if (energyToNutrientButton != null)
                energyToNutrientButton.interactable = ready && resources != null &&
                    resources.Energy >= spendEnergy && resources.Nutrients + receiveNutrients <= resources.NutrientCapacity;
            if (nutrientToEnergyButton != null)
                nutrientToEnergyButton.interactable = ready && resources != null &&
                    resources.Nutrients >= spendNutrients && resources.Energy + receiveEnergy <= resources.EnergyCapacity;
            if (statusText != null && panel != null && panel.activeInHierarchy && resources != null)
                statusText.text = string.Format("Stored: {0:0} energy / {1:0} nutrients", resources.Energy, resources.Nutrients);
        }

        public void Open()
        {
            if (mushroomTradingMode) return;
            if (network == null || !network.Connected || deliveryInProgress || Time.time < readyTime || panel == null) return;
            panel.SetActive(!panel.activeSelf); // tap market again to dismiss
        }

        public void Close() { if (panel != null) panel.SetActive(false); }

        public void TradeEnergy() { if (!mushroomTradingMode) BeginTrade(true, alternateSides ? nextDeliveryToLeft : false, 1f, 1f); }
        public void TradeNutrients() { if (!mushroomTradingMode) BeginTrade(false, alternateSides ? nextDeliveryToLeft : true, 1f, 1f); }

        /// <summary>Returns true only after resources have been deducted and the courier launched.</summary>
        public bool TryMushroomTrade(bool payEnergy, bool sendLeft, float yieldMultiplier, float travelTimeMultiplier)
        {
            if (!mushroomTradingMode) return false;
            return BeginTrade(payEnergy, sendLeft, Mathf.Max(.1f, yieldMultiplier), Mathf.Clamp(travelTimeMultiplier,.2f,2f));
        }
        public bool CanMushroomTrade(bool payEnergy, float yieldMultiplier)
        {
            if (resources == null || !IsNetworkReady || deliveryInProgress || Time.time < readyTime) return false;
            float spent = payEnergy ? spendEnergy : spendNutrients;
            float received = (payEnergy ? receiveNutrients : receiveEnergy) * Mathf.Max(.1f, yieldMultiplier);
            return payEnergy ? resources.Energy >= spent && resources.Nutrients + received <= resources.NutrientCapacity :
                resources.Nutrients >= spent && resources.Energy + received <= resources.EnergyCapacity;
        }

        private bool BeginTrade(bool payEnergy, bool left, float yieldMultiplier, float travelTimeMultiplier)
        {
            if (deliveryInProgress || Time.time < readyTime || resources == null || network == null || !network.Connected) return false;
            float spent = payEnergy ? spendEnergy : spendNutrients;
            float received = (payEnergy ? receiveNutrients : receiveEnergy) * yieldMultiplier;
            if (spent <= 0f || received <= 0f) return false;

            // Validate capacity before charging the outgoing parcel.
            bool enough = payEnergy ? resources.Energy >= spent : resources.Nutrients >= spent;
            bool room = payEnergy ? resources.Nutrients + received <= resources.NutrientCapacity :
                                    resources.Energy + received <= resources.EnergyCapacity;
            if (!enough || !room)
            {
                TreeJamHUD.Notify("Not enough resources, or insufficient storage for the return parcel.");
                return false;
            }

            List<Vector3> route = fungalRoute != null && fungalRoute.isActiveAndEnabled
                ? fungalRoute.BuildCourierRoute(left) : BuildRoute(left);
            if (route == null || route.Count < 2)
            {
                TreeJamHUD.Notify("The fungal mail route is not ready yet.");
                return false;
            }

            // Charge ONLY the outgoing resource now. Arrival is the only point
            // where the return resource is credited; there is no instant trade.
            bool charged = payEnergy ? TreeJamEcoResources.TryTrade(resources, -spent, 0f) :
                                       TreeJamEcoResources.TryTrade(resources, 0f, -spent);
            if (!charged) return false;
            nextDeliveryToLeft = !nextDeliveryToLeft;
            deliveryInProgress = true;
            Close();
            deliveryRoutine = StartCoroutine(Deliver(route, payEnergy, received, travelTimeMultiplier));
            return true;
        }

        private List<Vector3> BuildRoute(bool left)
        {
            LineRenderer main = null;
            string mainName = left ? "Main mycelium LEFT" : "Main mycelium RIGHT";
            foreach (LineRenderer lr in network.GetComponentsInChildren<LineRenderer>(true))
                if (lr.name == mainName) { main = lr; break; }
            if (main == null || main.positionCount < 2) return null;

            var result = new List<Vector3>();
            Vector3 nearTree = new Vector3(
                network.treeRoot != null ? network.treeRoot.position.x : 0f,
                network.soilCenterY + network.centerRootDepth, 0f);
            result.Add(nearTree);
            for (int i = main.positionCount - 1; i >= 0; i--)
                result.Add(main.GetPosition(i));

            // The optional polished line continues beyond the older main line.
            // Attach it when available so deliveries really disappear off-screen.
            string extensionName = left ? "OFFSCREEN mycelium LEFT" : "OFFSCREEN mycelium RIGHT";
            foreach (TreeJamMyceliumPolish polish in FindObjectsByType<TreeJamMyceliumPolish>(FindObjectsSortMode.None))
            {
                foreach (LineRenderer lr in polish.GetComponentsInChildren<LineRenderer>(true))
                {
                    if (lr.name != extensionName || lr.positionCount < 2) continue;
                    for (int i = lr.positionCount - 2; i >= 0; i--)
                        result.Add(lr.GetPosition(i));
                    return result;
                }
            }
            return result;
        }

        private IEnumerator Deliver(List<Vector3> outgoing, bool paidEnergy, float expectedReturn, float travelTimeMultiplier)
        {
            Color sentColor = paidEnergy ? energyParcelColor : nutrientParcelColor;
            Color returnedColor = paidEnergy ? nutrientParcelColor : energyParcelColor;
            MakeParcel(sentColor);

            yield return Travel(outgoing, secondsOutbound * travelTimeMultiplier);
            if (parcelVisual != null) parcelVisual.SetActive(false);
            if (secondsAtDestination > 0f) yield return new WaitForSeconds(secondsAtDestination);
            if (parcelVisual != null) parcelVisual.SetActive(true);
            SetParcelColor(returnedColor);

            outgoing.Reverse();
            yield return Travel(outgoing, secondsInbound * travelTimeMultiplier);

            // Only deliver on arrival. Clamp if resources accumulated during transit.
            float available = resources == null ? 0f : (paidEnergy ? resources.NutrientCapacity - resources.Nutrients :
                                           resources.EnergyCapacity - resources.Energy);
            float actualReturn = Mathf.Clamp(expectedReturn, 0f, Mathf.Max(0f, available));
            bool delivered = false;
            if (resources != null && actualReturn > 0.00001f)
                delivered = paidEnergy ? TreeJamEcoResources.TryTrade(resources, 0f, actualReturn) :
                                         TreeJamEcoResources.TryTrade(resources, actualReturn, 0f);
            if (delivered)
                TreeJamHUD.Notify(string.Format("Fungal mail arrived: +{0:0.#} {1}.", actualReturn,
                    paidEnergy ? "nutrients" : "energy"));
            else
                TreeJamHUD.Notify("Fungal mail arrived, but your storage has no room.");

            if (parcelVisual != null) Destroy(parcelVisual);
            parcelVisual = null;
            parcelCore = null;
            parcelHalo = null;
            deliveryInProgress = false;
            deliveryRoutine = null;
            readyTime = Time.time + cooldownAfterDelivery;
        }

        private IEnumerator Travel(List<Vector3> points, float duration)
        {
            float total = 0f;
            for (int i = 1; i < points.Count; i++) total += Vector3.Distance(points[i - 1], points[i]);
            if (total < .0001f) yield break;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float distance = Mathf.Clamp01(time / Mathf.Max(.01f, duration)) * total;
                Vector3 position = points[0];
                for (int i = 1; i < points.Count; i++)
                {
                    float length = Vector3.Distance(points[i - 1], points[i]);
                    if (distance <= length || i == points.Count - 1)
                    {
                        position = Vector3.Lerp(points[i - 1], points[i],
                            length > .0001f ? Mathf.Clamp01(distance / length) : 1f);
                        break;
                    }
                    distance -= length;
                }
                if (parcelVisual != null) parcelVisual.transform.position = position;
                yield return null;
            }
            if (parcelVisual != null) parcelVisual.transform.position = points[points.Count - 1];
        }

        private void MakeParcel(Color color)
        {
            EnsureParcelAssets();
            parcelVisual = new GameObject("Fungal mail packet (in transit)");
            parcelVisual.transform.position = new Vector3(
                network.treeRoot != null ? network.treeRoot.position.x : 0f,
                network.soilCenterY + network.centerRootDepth, -0.02f);
            parcelHalo = NewPacketSprite("Soft outer glow", packetDiameter * 2.1f * courierScale, color, -1);
            parcelCore = NewPacketSprite("Resource packet", packetDiameter * courierScale, color, 0);
        }

        private SpriteRenderer NewPacketSprite(string name, float size, Color color, int depthOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parcelVisual.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = parcelSprite;
            sr.sharedMaterial = parcelMaterial;
            sr.sortingOrder = 20 + depthOffset;
            sr.color = color;
            go.transform.localScale = new Vector3(size, size, 1f);
            return sr;
        }

        private void SetParcelColor(Color color)
        {
            if (parcelCore != null) parcelCore.color = color;
            if (parcelHalo != null) parcelHalo.color = color;
        }

        private static void EnsureParcelAssets()
        {
            if (parcelSprite != null && parcelMaterial != null) return;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Fungal courier radial sprite";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f - size * .5f) / (size * .5f);
                    float dy = (y + .5f - size * .5f) / (size * .5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((1f - d) * 3.1f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            texture.Apply();
            parcelSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 64f);
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) parcelMaterial = new Material(shader) { name = "Fungal mail unlit material" };
        }

        public void RefreshTradeLabels()
        {
            SetButtonText(energyToNutrientButton,
                string.Format("{0:0} ENERGY  ->  {1:0} NUTRIENTS", spendEnergy, receiveNutrients));
            SetButtonText(nutrientToEnergyButton,
                string.Format("{0:0} NUTRIENTS  ->  {1:0} ENERGY", spendNutrients, receiveEnergy));
        }

        private static void SetButtonText(Button button, string value)
        {
            if (button == null) return;
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = value;
        }

        private void OnDecomposed(BonsaiLeaf leaf)
        {
            TreeJamEcoResources.GiveNutrients(resources, nutrientsPerDecomposedLeaf);
        }

        private void OnDestroy()
        {
            if (subscribed && bonsai != null) bonsai.LeafDecomposed -= OnDecomposed;
            if (parcelVisual != null) Destroy(parcelVisual);
        }
    }
}
