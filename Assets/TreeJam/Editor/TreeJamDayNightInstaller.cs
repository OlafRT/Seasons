#if UNITY_EDITOR
using System;
using System.IO;
using TreeJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds a day/night cycle to the CURRENT Tree Jam scene. Never regenerates the
/// scene, orb graph, tree, resources, or Photoshop art; safe to run only once.
/// </summary>
public static class TreeJamDayNightInstaller
{
    private const string WhitePath = "Assets/TreeJam/Art/TJ_DayNightWhite.png";
    private const string MaterialPath = "Assets/TreeJam/Art/TJ_NightTintUnlit.mat";

    [MenuItem("Tools/Tree Jam/Install Day + Night Cycle (Current Scene)")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Stop Play Mode", "Exit Play Mode before adding the day/night cycle.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        TreeGameState gameState = UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
        if (!scene.IsValid() || gameState == null)
        {
            EditorUtility.DisplayDialog("Tree Jam", "Open the Tree Jam scene containing Game Systems / Tree Game State first.", "OK");
            return;
        }
        TreeJamDayNightCycle existing = UnityEngine.Object.FindFirstObjectByType<TreeJamDayNightCycle>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            EditorUtility.DisplayDialog("Already installed", "The day/night system already exists. Select it in the Hierarchy to edit settings. Nothing was changed.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Install day and night?",
            "Add a moving sun, editable day duration, an editable clock, and a nighttime tint?\n\n" +
            "Energy production will scale with daylight (zero at night). Nutrient gathering continues.\n\n" +
            "Your existing Photoshop environment, orb references, tree and UI will NOT be regenerated.",
            "Install", "Cancel")) return;

        Sprite white = EnsureWhiteSprite();
        Material unlit = EnsureUnlitMaterial();
        if (white == null || unlit == null)
        {
            EditorUtility.DisplayDialog("Failed to set up art", "Couldn't create the overlay sprite/material. Check Console for details.", "OK");
            return;
        }
        GameObject controller = new GameObject("DAY & NIGHT - edit settings here");
        Undo.RegisterCreatedObjectUndo(controller, "Create Tree Jam day/night controller");
        controller.transform.SetParent(gameState.transform, false);

        TreeJamDayNightCycle cycle = controller.AddComponent<TreeJamDayNightCycle>();
        cycle.gameState = gameState;
        cycle.worldCamera = Camera.main;
        cycle.sunSprite = FindSun(scene);

        GameObject overlayObj = new GameObject("Night overlay - world darkening");
        Undo.RegisterCreatedObjectUndo(overlayObj, "Create night overlay");
        overlayObj.transform.SetParent(controller.transform, false);
        SpriteRenderer overlay = overlayObj.AddComponent<SpriteRenderer>();
        overlay.sprite = white;
        overlay.sharedMaterial = unlit;
        overlay.sortingLayerName = "Default";
        overlay.sortingOrder = 30000;
        overlay.color = new Color(0.025f, .065f, .16f, 0f);
        cycle.nightOverlay = overlay;

        Canvas canvas = FindEditableCanvas(scene);
        if (canvas != null)
        {
            GameObject go = new GameObject("Day clock - EDIT FONT / POSITION", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            Undo.RegisterCreatedObjectUndo(go, "Create editable day clock");
            go.transform.SetParent(canvas.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -22f);
            rect.sizeDelta = new Vector2(390f, 80f);
            Text txt = go.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 21;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(.20f, .28f, .32f, .92f);
            txt.alignment = TextAnchor.UpperRight;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.text = "DAY 01  |  07:30\nSUNLIGHT  0%";
            TreeJamDayNightClockUI clock = go.AddComponent<TreeJamDayNightClockUI>();
            clock.cycle = cycle;
            clock.label = txt;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = controller;
        Debug.Log("Tree Jam: Day/night installed. Sun sprite: " +
            (cycle.sunSprite == null ? "NOT FOUND (assign manually on controller)" : cycle.sunSprite.name) +
            ". Editable clock: " + (canvas != null ? "added" : "no Canvas found; add one manually") + ".");
        EditorUtility.DisplayDialog("Day/night installed",
            "Created DAY & NIGHT - edit settings here under Game Systems.\n\n" +
            "Found sun: " + (cycle.sunSprite != null ? cycle.sunSprite.name : "None - assign your Sun SpriteRenderer manually") + "\n" +
            "Editable clock: " + (canvas != null ? "yes" : "no Canvas was found") +
            "\n\nSave the scene (Ctrl+S), then press Play. The time/day length and sun path are editable in the Inspector.", "OK");
    }

    private static SpriteRenderer FindSun(Scene scene)
    {
        SpriteRenderer best = null;
        int bestScore = -1;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string name = sprite.name.ToLowerInvariant();
            if (!name.Contains("sun")) continue;
            int score = 0;
            if (name == "02 sun" || name == "sun") score += 40;
            if (sprite.transform.parent != null &&
                sprite.transform.parent.name.ToLowerInvariant().Contains("sunrotator")) score += 60;
            if (sprite.gameObject.activeInHierarchy) score += 20;
            if (sprite.sprite != null) score += 10;
            if (score > bestScore) { bestScore = score; best = sprite; }
        }
        return best;
    }

    private static Canvas FindEditableCanvas(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay &&
                (c.name.Contains("YOUR DESIGN") || c.GetComponent<TreeJamUI>() != null)) return c;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        return null;
    }

    private static Sprite EnsureWhiteSprite()
    {
        Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath);
        if (result != null) return result;
        Directory.CreateDirectory("Assets/TreeJam/Art");
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] colors = new Color[16 * 16];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        File.WriteAllBytes(WhitePath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(WhitePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(WhitePath) as TextureImporter;
        if (importer == null) return null;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath);
    }

    private static Material EnsureUnlitMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        mat = new Material(shader) { name = "TJ Night Overlay (unlit + transparent)" };
        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
#endif
