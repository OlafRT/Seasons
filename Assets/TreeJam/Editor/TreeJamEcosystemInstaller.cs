#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TreeJam.Editor
{
    /// <summary>
    /// New objects only, installs into currently open scene, safe to run twice.
    /// Does NOT copy, replace, delete, or move any previous TreeJam scripts/assets/orbs.
    /// </summary>
    public static class TreeJamEcosystemInstaller
    {
        private const string RootName = "ECOSYSTEM - NIGHT SKY, SNOW, FUNGI (EDIT HERE)";
        private static Font uiFont;
        private static SpriteRenderer daySky;
        private static SpriteRenderer nightSky;

        [MenuItem("Tools/Tree Jam/Install Ecosystem - Night Sky, Snow, Fungi (Current Scene)")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Please exit Play Mode before installing.", "OK");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            TreeJamDayNightCycle dayNight = UnityEngine.Object.FindFirstObjectByType<TreeJamDayNightCycle>();
            TreeJamSeasonCycle seasons = UnityEngine.Object.FindFirstObjectByType<TreeJamSeasonCycle>();
            TreeGameState state = UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
            OrganicBonsaiTree bonsai = UnityEngine.Object.FindFirstObjectByType<OrganicBonsaiTree>();
            if (!scene.IsValid() || dayNight == null || seasons == null || state == null || bonsai == null)
            {
                EditorUtility.DisplayDialog("Tree Jam: required systems missing",
                    "This add-on needs the existing Day/Night, Seasons, Tree Game State, and Organic Bonsai components. " +
                    "Open your playable scene, install the earlier Seasons add-on if necessary, and retry. No changes were made.", "OK");
                return;
            }
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            FindSkyLayers();

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Install Tree Jam Ecosystem");
            }

            var sky = GetOrAdd<TreeJamNordicSky>(root);
            Undo.RecordObject(sky, "Wire sky references");
            sky.dayNight = dayNight;
            sky.seasons = seasons;
            sky.resources = state;
            if (sky.daySky == null) sky.daySky = daySky;
            if (sky.nightSky == null) sky.nightSky = nightSky;
            EditorUtility.SetDirty(sky);
            // Never move/resize your custom artwork; only ensure backdrop sorting.
            if (daySky != null && nightSky != null)
            {
                Undo.RecordObject(nightSky, "Place night sky behind day sky");
                nightSky.sortingLayerID = daySky.sortingLayerID;
                nightSky.sortingOrder = daySky.sortingOrder - 1;
                EditorUtility.SetDirty(nightSky);
            }

            var snow = GetOrAdd<TreeJamNordicSnow>(GetOrCreateChild(root.transform, "WINTER - snow accumulation + flakes"));
            Undo.RecordObject(snow, "Wire snow references");
            snow.seasons = seasons;
            snow.worldCamera = dayNight.worldCamera != null ? dayNight.worldCamera : Camera.main;
            snow.middleGroundY = bonsai.soilSurfaceWorldY;
            EditorUtility.SetDirty(snow);

            var fungi = GetOrAdd<TreeJamMyceliumNetwork>(GetOrCreateChild(root.transform, "FUNGAL NETWORK - white underground threads"));
            Undo.RecordObject(fungi, "Wire network references");
            fungi.worldCamera = snow.worldCamera;
            fungi.treeRoot = bonsai.transform;
            fungi.soilCenterY = bonsai.soilSurfaceWorldY - 1.15f;
            EditorUtility.SetDirty(fungi);

            var market = GetOrAdd<TreeJamFungiMarket>(GetOrCreateChild(root.transform, "MARKET - edit trade values"));
            Undo.RecordObject(market, "Wire market references");
            market.network = fungi;
            market.resources = state;
            market.bonsai = bonsai;
            CreateMarketCanvas(market);
            EditorUtility.SetDirty(market);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("Tree Jam: Ecosystem installed",
                "Added NIGHT SKY fade, WINTER snow, FUNGAL NETWORK, and MARKET UI. " +
                "Autumn leaves remain controlled by your existing Seasons script; the market also credits nutrients for decomposed leaves.\n\n" +
                "IMPORTANT: Select the Ecosystem root and check Day Sky / Night Sky assignments. " +
                "If either is empty, drag your two existing SpriteRenderers in manually. " +
                "No old script, upgrade, orb, camera, or artwork was replaced.\n\nSave scene (Ctrl+S).", "OK");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var value = go.GetComponent<T>();
            if (value == null) value = Undo.AddComponent<T>(go);
            return value;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void FindSkyLayers()
        {
            daySky = null;
            nightSky = null;
            SpriteRenderer[] sprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int bestDay = -1, bestNight = -1;
            foreach (SpriteRenderer sr in sprites)
            {
                if (sr == null || sr.sprite == null) continue;
                string name = sr.gameObject.name.ToLowerInvariant();
                string hierarchy = sr.transform.parent != null ? sr.transform.parent.name.ToLowerInvariant() : "";
                bool night = name.Contains("night") || name.Contains("stars") || name.Contains("moon") ||
                             (hierarchy.Contains("night") && name.Contains("sky"));
                bool sky = name.Contains("sky") || (name.Contains("background") && !name.Contains("soil"));
                if (night && (sky || name.Contains("night")))
                {
                    int score = (name.Contains("sky") ? 10 : 0) + (name.Contains("night") ? 10 : 0);
                    if (score > bestNight) { bestNight = score; nightSky = sr; }
                }
                else if (sky && !name.Contains("sun"))
                {
                    int score = (name.Contains("sky") ? 10 : 0) + (name.Contains("01") ? 3 : 0);
                    if (score > bestDay) { bestDay = score; daySky = sr; }
                }
            }
        }

        private static void CreateMarketCanvas(TreeJamFungiMarket market)
        {
            Canvas canvas = null;
            TreeJamUI existingUI = UnityEngine.Object.FindFirstObjectByType<TreeJamUI>();
            if (existingUI != null) canvas = existingUI.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Canvas candidate in canvases)
                    if (candidate.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = candidate; break; }
            }
            if (canvas == null)
            {
                GameObject go = new GameObject("Tree Jam - Ecosystem Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(go, "Create Tree Jam Ecosystem Canvas");
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                // Input module is usually already in a working Unity UI scene. The
                // EventSystem object can be added manually if buttons don't respond.
                GameObject es = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(es, "Add EventSystem for market UI");
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            GameObject root = GetOrCreateUIChild(canvas.transform, "FUNGI MARKET - editable UI");
            RectTransform uiRoot = GetRect(root);
            uiRoot.anchorMin = uiRoot.anchorMax = new Vector2(1f, 0f);
            uiRoot.pivot = new Vector2(1f, 0f);
            uiRoot.anchoredPosition = new Vector2(-24f, 25f);
            uiRoot.sizeDelta = new Vector2(350, 300);

            Button open = MakeButton(root.transform, "Open market (unlocks at fungal connection)",
                "FUNGI MARKET", new Vector2(0f, 0f), new Vector2(170f, 42f));
            RectTransform openRect = GetRect(open.gameObject);
            openRect.anchorMin = openRect.anchorMax = new Vector2(1f, 0f);
            openRect.pivot = new Vector2(1f, 0f);
            openRect.anchoredPosition = Vector2.zero;
            market.openButton = open;
            GameObject panel = GetOrCreateUIChild(root.transform, "Trade panel - EDIT COLORS, FONTS, POSITIONS");
            RectTransform panelRect = GetRect(panel);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 53f);
            panelRect.sizeDelta = new Vector2(350f, 241f);
            Image panelImage = GetOrAdd<Image>(panel);
            panelImage.color = new Color(.09f, .14f, .15f, .94f);
            MakeText(panel.transform, "Market title", "THE FUNGAL NETWORK", new Vector2(13f, -10f),
                new Vector2(300f, 31f), 19, TextAnchor.MiddleLeft, new Color(.86f, .93f, .76f));
            MakeText(panel.transform, "Market explanation", "The fungi share resources with your roots.",
                new Vector2(13f, -46f), new Vector2(320f, 30f), 13, TextAnchor.MiddleLeft, new Color(.79f, .85f, .85f));
            Button one = MakeButton(panel.transform, "Energy to nutrients", "20 SUNLIGHT  →  12 NUTRIENTS",
                new Vector2(12f, -87f), new Vector2(326f, 40f));
            Button two = MakeButton(panel.transform, "Nutrients to energy", "18 NUTRIENTS  →  13 SUNLIGHT",
                new Vector2(12f, -136f), new Vector2(326f, 40f));
            Text status = MakeText(panel.transform, "Wallet status", "Stored: ...",
                new Vector2(14f, -190f), new Vector2(305f, 25f), 12, TextAnchor.MiddleLeft, new Color(.78f, .83f, .78f));
            Button close = MakeButton(panel.transform, "Close market", "×", new Vector2(308f, -8f), new Vector2(32f, 31f));
            market.panel = panel;
            market.closeButton = close;
            market.energyToNutrientButton = one;
            market.nutrientToEnergyButton = two;
            market.statusText = status;
            panel.SetActive(false);
            open.gameObject.SetActive(false);
            EditorUtility.SetDirty(root);
        }

        private static GameObject GetOrCreateUIChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform GetRect(GameObject go) => go.GetComponent<RectTransform>();
        // Child anchors default top-left, local pixels; parent rect is 350x241.
        private static void SetTopLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
        private static Text MakeText(Transform parent, string name, string content,
            Vector2 offset, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            var go = GetOrCreateUIChild(parent, name);
            Text t = GetOrAdd<Text>(go);
            SetTopLeft(GetRect(go), offset, size);
            t.font = uiFont;
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }
        private static Button MakeButton(Transform parent, string name, string label, Vector2 offset, Vector2 size)
        {
            var go = GetOrCreateUIChild(parent, name);
            SetTopLeft(GetRect(go), offset, size);
            var image = GetOrAdd<Image>(go);
            image.color = new Color(.26f, .37f, .30f, .98f);
            var button = GetOrAdd<Button>(go);
            button.targetGraphic = image;
            var block = button.colors;
            block.highlightedColor = new Color(.81f, .96f, .82f);
            block.pressedColor = new Color(.60f, .8f, .65f);
            block.disabledColor = new Color(.15f, .18f, .18f, .65f);
            button.colors = block;
            Text labelText = MakeText(go.transform, "Label", label, Vector2.zero, size, 14,
                TextAnchor.MiddleCenter, Color.white);
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            return button;
        }
    }
}
#endif
