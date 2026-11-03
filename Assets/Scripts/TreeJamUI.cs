using UnityEngine;
using UnityEngine.UI;

namespace TreeJam
{
    /// <summary>
    /// View-only controller for the editable scene Canvas. Every image, font, size,
    /// colour and position can be changed directly in the Unity Hierarchy.
    /// </summary>
    public class TreeJamUI : MonoBehaviour
    {
        [Header("Data")]
        public TreeGameState gameState;
        public Camera worldCamera;
        public Canvas rootCanvas;

        [Header("Resource panel (drag UI Text/Image components here)")]
        public Text energyText;
        public Text nutrientsText;
        public Text leavesText;
        public Image energyFill;
        public Image nutrientsFill;

        [Header("Hover tooltip")]
        public RectTransform tooltipPanel;
        public Text tooltipTitle;
        public Text tooltipDescription;
        public Text tooltipCosts;
        public Text tooltipHint;

        [Header("Message")]
        public GameObject toastPanel;
        public Text toastText;

        [Header("Positioning")]
        [Tooltip("Distance from the upgrade orb to the tooltip, in Canvas units.")]
        [Min(0)] public float tooltipGap = 26f;
        [Min(0)] public float screenMargin = 14f;
        [Min(0.1f)] public float toastDuration = 2.5f;

        private float toastHideTime;

        private void Awake()
        {
            if (gameState == null) gameState = FindFirstObjectByType<TreeGameState>();
            if (worldCamera == null) worldCamera = Camera.main;
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            if (tooltipPanel != null) tooltipPanel.gameObject.SetActive(false);
            if (toastPanel != null) toastPanel.SetActive(false);
        }

        private void OnEnable() => TreeJamHUD.MessageRaised += OnMessage;
        private void OnDisable() => TreeJamHUD.MessageRaised -= OnMessage;

        private void OnMessage(string message)
        {
            if (toastText != null) toastText.text = message;
            if (toastPanel != null) toastPanel.SetActive(true);
            toastHideTime = Time.unscaledTime + toastDuration;
        }

        private void LateUpdate()
        {
            UpdateResources();
            UpdateTooltip();

            if (toastPanel != null && toastPanel.activeSelf && Time.unscaledTime >= toastHideTime)
                toastPanel.SetActive(false);
        }

        private void UpdateResources()
        {
            if (gameState == null) return;
            if (energyText != null)
                energyText.text = $"{gameState.Energy:0} / {gameState.EnergyCapacity:0}    +{gameState.EnergyPerSecond:0.0}/s";
            if (nutrientsText != null)
                nutrientsText.text = $"{gameState.Nutrients:0} / {gameState.NutrientCapacity:0}    +{gameState.NutrientsPerSecond:0.0}/s";
            if (leavesText != null) leavesText.text = $"LEAVES   {gameState.LeafCount}";

            if (energyFill != null)
                energyFill.fillAmount = gameState.EnergyCapacity > 0f
                    ? Mathf.Clamp01(gameState.Energy / gameState.EnergyCapacity) : 0f;
            if (nutrientsFill != null)
                nutrientsFill.fillAmount = gameState.NutrientCapacity > 0f
                    ? Mathf.Clamp01(gameState.Nutrients / gameState.NutrientCapacity) : 0f;
        }

        private void UpdateTooltip()
        {
            if (tooltipPanel == null) return;

            UpgradeOrb orb = TreeJamMouse.HoveredOrb;
            if (orb == null || !orb.IsVisible || orb.definition == null || worldCamera == null || rootCanvas == null)
            {
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            UpgradeDefinition definition = orb.definition;
            tooltipPanel.gameObject.SetActive(true);

            if (tooltipTitle != null) tooltipTitle.text = definition.displayName;
            if (tooltipDescription != null) tooltipDescription.text = definition.description;
            if (tooltipCosts != null)
                tooltipCosts.text = $"ENERGY  {definition.energyCost:0}     NUTRIENTS  {definition.nutrientCost:0}";
            if (tooltipHint != null)
            {
                bool canAfford = gameState != null && gameState.CanAfford(definition);
                tooltipHint.text = canAfford ? "CLICK TO GROW" : "NOT ENOUGH RESOURCES";
                tooltipHint.color = canAfford
                    ? new Color(0.64f, 0.86f, 0.61f) : new Color(0.96f, 0.71f, 0.49f);
            }

            // Position the tooltip next to the ORB, not at the screen corner.
            // Clamp it to the visible canvas, switching sides near the right edge.
            Vector3 screenPos = worldCamera.WorldToScreenPoint(orb.transform.position);
            if (screenPos.z <= 0f)
            {
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            if (canvasRect == null) return;
            Camera canvasCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : rootCanvas.worldCamera;
            Vector2 orbLocal;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPos, canvasCamera, out orbLocal)) return;

            // Tooltip RectTransform must be center-anchored with top-left pivot.
            float width = tooltipPanel.rect.width;
            float height = tooltipPanel.rect.height;
            float minX = -canvasRect.rect.width * 0.5f + screenMargin;
            float maxX = canvasRect.rect.width * 0.5f - screenMargin - width;
            float minTop = -canvasRect.rect.height * 0.5f + screenMargin + height;
            float maxTop = canvasRect.rect.height * 0.5f - screenMargin;

            float rightX = orbLocal.x + tooltipGap;
            float leftX = orbLocal.x - tooltipGap - width;
            float x = rightX + width > canvasRect.rect.width * 0.5f - screenMargin
                ? leftX : rightX;
            x = Mathf.Clamp(x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
            float y = Mathf.Clamp(orbLocal.y + height * 0.5f,
                Mathf.Min(minTop, maxTop), Mathf.Max(minTop, maxTop));
            tooltipPanel.anchoredPosition = new Vector2(x, y);
        }
    }
}
