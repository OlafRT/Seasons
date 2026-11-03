#if UNITY_EDITOR
using System.IO;
using TreeJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Add a normal, fully editable uGUI Canvas and empty art slots to the scene
/// without regenerating the tree, orb definitions, or existing environment.
/// </summary>
public static class TreeJamEditableUIInstaller
{
    private const string UiArtFolder = "Assets/TreeJam/UI";
    private static Font defaultFont;
    private static Sprite roundedSprite;

    private static readonly Color Panel = Hex("#152B2A", 0.94f);
    private static readonly Color PanelDark = Hex("#0C201E", 0.98f);
    private static readonly Color Cream = Hex("#F5EEDD");
    private static readonly Color Muted = Hex("#A8B7A6");
    private static readonly Color Sun = Hex("#E2BE78");
    private static readonly Color Moss = Hex("#89C397");

    [MenuItem("Tools/Tree Jam/Install Editable UI + Art Slots (Current Scene)")]
    public static void InstallFromMenu()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Exit Play Mode", "Stop Play Mode before installing the UI.", "OK");
            return;
        }
        InstallIntoCurrentScene();
    }

    // Called by the starter scene builder too, so fresh scenes also have editable UI.
    public static void InstallIntoCurrentScene()
    {
        TreeGameState state = Object.FindFirstObjectByType<TreeGameState>();
        if (state == null)
        {
            EditorUtility.DisplayDialog("No Tree Game State", "Open your Tree Jam starter scene first.", "OK");
            return;
        }

        TreeJamUI existing = Object.FindFirstObjectByType<TreeJamUI>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("Tree Jam: Editable UI already installed. Select UI / YOUR DESIGN to edit it.");
            return;
        }

        EnsureFontAndSprite();
        GameObject canvasGO = new GameObject("UI - YOUR DESIGN", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Tree Jam editable UI");
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Resources: a small, restrained top-left panel; all objects editable.
        RectTransform resources = CreateImage("Resource Panel - change background", canvasGO.transform,
            "TL", new Vector2(30, -30), new Vector2(392, 222), Panel);
        Text heading = Label("Header - edit text/font", resources, new Vector2(20, -13),
            new Vector2(350, 36), "A LIVING TREE", 23, Cream, FontStyle.Bold);
        Label("Subtitle", resources, new Vector2(21, -49), new Vector2(350, 22),
            "SUMMER  /  YEAR ONE", 12, Muted);

        Label("Energy Caption", resources, new Vector2(21, -78), new Vector2(145, 22),
            "SUNLIGHT", 13, Sun, FontStyle.Bold);
        Text energyValue = Label("Energy Value", resources, new Vector2(158, -78),
            new Vector2(212, 22), "0 / 100", 13, Cream, FontStyle.Normal, TextAnchor.MiddleRight);
        CreateImage("Energy Bar Background", resources, "TL", new Vector2(21, -107),
            new Vector2(349, 11), Hex("#0A191A"));
        Image energyFill = Bar("Energy Bar Fill", resources, new Vector2(21, -107),
            new Vector2(349, 11), Sun);

        Label("Nutrients Caption", resources, new Vector2(21, -137), new Vector2(145, 22),
            "NUTRIENTS", 13, Moss, FontStyle.Bold);
        Text nutrientsValue = Label("Nutrients Value", resources, new Vector2(158, -137),
            new Vector2(212, 22), "0 / 100", 13, Cream, FontStyle.Normal, TextAnchor.MiddleRight);
        CreateImage("Nutrient Bar Background", resources, "TL", new Vector2(21, -166),
            new Vector2(349, 11), Hex("#0A191A"));
        Image nutrientFill = Bar("Nutrient Bar Fill", resources, new Vector2(21, -166),
            new Vector2(349, 11), Moss);
        Text leavesValue = Label("Leaf Count", resources, new Vector2(21, -188),
            new Vector2(349, 23), "LEAVES  5", 12, Muted);

        // Tooltip is in the Canvas, but continuously follows the world-space orb.
        RectTransform tooltip = CreateImage("Tooltip - edit layout here", canvasGO.transform,
            "CENTER", Vector2.zero, new Vector2(342, 191), PanelDark);
        tooltip.pivot = new Vector2(0, 1);
        CreateImage("Accent Stripe", tooltip, "TL", new Vector2(0, 0),
            new Vector2(5, 191), Moss);
        Text tooltipTitle = Label("Upgrade Name", tooltip, new Vector2(20, -14),
            new Vector2(303, 31), "New growth", 22, Cream, FontStyle.Bold);
        Text tooltipDesc = Label("Description", tooltip, new Vector2(20, -53),
            new Vector2(301, 73), "What this upgrade does.", 16, Muted);
        Text tooltipCosts = Label("Costs", tooltip, new Vector2(20, -133),
            new Vector2(301, 23), "ENERGY  25   NUTRIENTS  12", 14, Cream);
        Text tooltipHint = Label("Purchase Hint", tooltip, new Vector2(20, -159),
            new Vector2(301, 23), "CLICK TO GROW", 13, Moss, FontStyle.Bold);
        tooltip.gameObject.SetActive(false);

        RectTransform toast = CreateImage("Notification - edit layout here", canvasGO.transform,
            "BOTTOM", new Vector2(0, 35), new Vector2(470, 52), Panel);
        Text toastText = Label("Message", toast, new Vector2(20, -10),
            new Vector2(430, 32), "New growth!", 18, Cream,
            FontStyle.Bold, TextAnchor.MiddleCenter);
        toast.gameObject.SetActive(false);

        TreeJamUI ui = canvasGO.AddComponent<TreeJamUI>();
        ui.gameState = state;
        ui.worldCamera = Camera.main;
        ui.rootCanvas = canvas;
        ui.energyText = energyValue;
        ui.nutrientsText = nutrientsValue;
        ui.leavesText = leavesValue;
        ui.energyFill = energyFill;
        ui.nutrientsFill = nutrientFill;
        ui.tooltipPanel = tooltip;
        ui.tooltipTitle = tooltipTitle;
        ui.tooltipDescription = tooltipDesc;
        ui.tooltipCosts = tooltipCosts;
        ui.tooltipHint = tooltipHint;
        ui.toastPanel = toast.gameObject;
        ui.toastText = toastText;

        // Old components are kept as harmless message bridges; no IMGUI will draw.
        MakeArtSlots();
        Selection.activeGameObject = canvasGO;
        EditorGUIUtility.PingObject(canvasGO);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Tree Jam: Installed editable Canvas + blank sprite slots. Save the scene (Ctrl+S). " +
                  "Nothing in the existing tree/environment was replaced.");
    }

    private static void MakeArtSlots()
    {
        if (GameObject.Find("ART - YOUR OWN SPRITES") != null) return;
        GameObject root = new GameObject("ART - YOUR OWN SPRITES");
        Undo.RegisterCreatedObjectUndo(root, "Add customizable scene art slots");
        ArtSlot("01  SKY / BACKGROUND", root.transform, -30);
        ArtSlot("02  DISTANT LANDSCAPE", root.transform, -20);
        ArtSlot("03  SOIL TEXTURE", root.transform, -7);
        ArtSlot("04  GROUND SURFACE", root.transform, -6);
        ArtSlot("05  FOREGROUND DETAILS", root.transform, 12);
    }

    private static void ArtSlot(string name, Transform parent, int order)
    {
        GameObject slot = new GameObject(name, typeof(SpriteRenderer));
        slot.transform.SetParent(parent, false);
        SpriteRenderer sprite = slot.GetComponent<SpriteRenderer>();
        sprite.sortingOrder = order;
        // No sprite assigned: the user can drag in their own artwork.
    }

    private static RectTransform CreateImage(string name, Transform parent, string anchor,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        if (anchor == "TL")
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
        }
        else if (anchor == "BOTTOM")
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image graphic = go.GetComponent<Image>();
        graphic.sprite = roundedSprite;
        graphic.type = Image.Type.Sliced;
        graphic.color = color;
        graphic.raycastTarget = false;
        return rect;
    }

    private static Image Bar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateImage(name, parent, "TL", position, size, color);
        Image graphic = rect.GetComponent<Image>();
        graphic.type = Image.Type.Filled;
        graphic.fillMethod = Image.FillMethod.Horizontal;
        graphic.fillOrigin = (int)Image.OriginHorizontal.Left;
        graphic.fillAmount = 0.5f;
        return graphic;
    }

    private static Text Label(string name, Transform parent, Vector2 position, Vector2 size,
        string value, int fontSize, Color color,
        FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text text = go.GetComponent<Text>();
        text.text = value;
        text.font = defaultFont;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void EnsureFontAndSprite()
    {
        if (!Directory.Exists(UiArtFolder)) Directory.CreateDirectory(UiArtFolder);
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        string path = UiArtFolder + "/SoftRoundedPanel.png";
        if (!File.Exists(path))
        {
            const int size = 64;
            const float radius = 14f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Rounded rectangular signed distance field with a small anti-alias.
                float px = Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius);
                float py = Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius);
                float dist = Mathf.Sqrt(Mathf.Max(0, px) * Mathf.Max(0, px)
                                      + Mathf.Max(0, py) * Mathf.Max(0, py))
                                      + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                float alpha = Mathf.Clamp01(0.5f - dist);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 64;
            importer.spriteBorder = new Vector4(18, 18, 18, 18);
            importer.SaveAndReimport();
        }
        roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Color Hex(string value, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        color.a = alpha;
        return color;
    }
}
#endif
