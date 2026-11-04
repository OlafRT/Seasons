using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TreeJam
{
    /// <summary>
    /// Add this beside TreeJamUI on "UI - YOUR DESIGN". Uses rectangle hit tests,
    /// not UI Buttons / raycast targets, so it works with decorative Image bars
    /// and Text labels. Does NOT modify TreeJamUI's upgrade-orb tooltip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamResourceBarTooltips : MonoBehaviour
    {
        [Header("Existing UI (automatically detected if blank)")]
        public TreeJamUI ui;
        public Canvas canvas;
        public RectTransform energyBarBackground;
        public RectTransform nutrientBarBackground;
        public RectTransform energyCaption;
        public RectTransform nutrientCaption;

        [Header("Tooltip wording")]
        [TextArea(2, 3)] public string energyExplanation =
            "Energy is produced by your leaves through photosynthesis.";
        [TextArea(2, 3)] public string nutrientExplanation =
            "Nutrients are absorbed from the soil through your roots.";

        [Header("Tooltip appearance")]
        public Font font;
        public Color panelColor = new Color(.065f, .12f, .12f, .96f);
        public Color bodyColor = new Color(.89f, .93f, .85f, 1f);
        public Color energyHeaderColor = new Color(.98f, .84f, .47f, 1f);
        public Color nutrientHeaderColor = new Color(.56f, .84f, .59f, 1f);
        [Min(240f)] public float width = 355f;
        [Min(64f)] public float height = 84f;
        public Vector2 cursorOffset = new Vector2(20f, -16f);
        [Min(0f)] public float canvasMargin = 12f;

        private RectTransform panel;
        private Text header;
        private Text explanation;

        private void Awake()
        {
            if (ui == null) ui = GetComponent<TreeJamUI>();
            if (ui == null) ui = FindFirstObjectByType<TreeJamUI>();
            if (canvas == null) canvas = ui != null && ui.rootCanvas != null
                ? ui.rootCanvas : GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("TreeJamResourceBarTooltips: No Canvas found. Attach this to your UI Canvas.", this);
                enabled = false;
                return;
            }
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (energyBarBackground == null) energyBarBackground = FindNamedRect("Energy Bar Background");
            if (nutrientBarBackground == null) nutrientBarBackground = FindNamedRect("Nutrient Bar Background");
            if (energyCaption == null) energyCaption = FindNamedRect("Energy Caption");
            if (nutrientCaption == null) nutrientCaption = FindNamedRect("Nutrients Caption");
            CreateTooltip();
        }

        private RectTransform FindNamedRect(string name)
        {
            foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }

        private void CreateTooltip()
        {
            GameObject go = new GameObject("RESOURCE TOOLTIP (runtime)",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(canvas.transform, false);
            panel = (RectTransform)go.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(width, height);
            Image background = go.GetComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = false;
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            header = CreateLabel("Resource name", new Vector2(16f, -11f),
                new Vector2(width - 32f, 23f), 15, FontStyle.Bold);
            explanation = CreateLabel("Description", new Vector2(16f, -38f),
                new Vector2(width - 32f, height - 42f), 14, FontStyle.Normal);
            explanation.color = bodyColor;
            panel.gameObject.SetActive(false);
        }

        private Text CreateLabel(string name, Vector2 position, Vector2 size, int sizeInPoints, FontStyle style)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            child.transform.SetParent(panel, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = child.GetComponent<Text>();
            text.font = font;
            text.fontSize = sizeInPoints;
            text.fontStyle = style;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void LateUpdate()
        {
            if (panel == null || ui == null) return;
            if (!ReadMouse(out Vector2 screen))
            {
                panel.gameObject.SetActive(false);
                return;
            }
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;

            bool energy = Matches(screen, eventCamera, energyBarBackground) ||
                          Matches(screen, eventCamera, energyCaption) ||
                          (ui.energyFill != null && Matches(screen, eventCamera, ui.energyFill.rectTransform)) ||
                          (ui.energyText != null && Matches(screen, eventCamera, ui.energyText.rectTransform));
            bool nutrients = !energy &&
                             (Matches(screen, eventCamera, nutrientBarBackground) ||
                              Matches(screen, eventCamera, nutrientCaption) ||
                              (ui.nutrientsFill != null && Matches(screen, eventCamera, ui.nutrientsFill.rectTransform)) ||
                              (ui.nutrientsText != null && Matches(screen, eventCamera, ui.nutrientsText.rectTransform)));

            if (!energy && !nutrients)
            {
                panel.gameObject.SetActive(false);
                return;
            }

            header.text = energy ? "ENERGY" : "NUTRIENTS";
            header.color = energy ? energyHeaderColor : nutrientHeaderColor;
            explanation.text = energy ? energyExplanation : nutrientExplanation;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screen, eventCamera, out Vector2 local)) return;

            panel.sizeDelta = new Vector2(width, height);
            Vector2 target = local + cursorOffset;
            float xMin = canvasRect.rect.xMin + canvasMargin;
            float xMax = canvasRect.rect.xMax - width - canvasMargin;
            float yMin = canvasRect.rect.yMin + height + canvasMargin;
            float yMax = canvasRect.rect.yMax - canvasMargin;
            target.x = Mathf.Clamp(target.x, Mathf.Min(xMin, xMax), Mathf.Max(xMin, xMax));
            target.y = Mathf.Clamp(target.y, Mathf.Min(yMin, yMax), Mathf.Max(yMin, yMax));
            panel.anchoredPosition = target;
        }

        private static bool Matches(Vector2 pointer, Camera cam, RectTransform area)
        {
            return area != null && area.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(area, pointer, cam);
        }

        private static bool ReadMouse(out Vector2 position)
        {
            position = default(Vector2);
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            position = Mouse.current.position.ReadValue();
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return true;
#else
            return false;
#endif
        }

        private void OnDisable()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel.gameObject);
        }
    }
}
