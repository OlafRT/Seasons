#if UNITY_EDITOR
using System;
using System.IO;
using TreeJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

public static class TreeJamSceneBuilder
{
    private const string Root = "Assets/TreeJam";
    private static Sprite square;
    private static Sprite circle;

    [MenuItem("Tools/Tree Jam/Create Starter Scene")]
    public static void CreateStarterScene()
    {
        // Never silently overwrite a customized scene or upgrade assets.
        if (File.Exists(Root + "/Scenes/TreeJamStarter.unity") &&
            !EditorUtility.DisplayDialog("Overwrite Tree Jam Starter?",
                "Recreating the starter will OVERWRITE the starter scene AND its four sample upgrade assets. " +
                "Your custom artwork/UI in that scene could be lost. Continue?",
                "Overwrite", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(Root + "/Art");
        EnsureFolder(Root + "/Data");
        EnsureFolder(Root + "/Scenes");

        square = GenerateSprite(Root + "/Art/TJ_Square.png", false);
        circle = GenerateSprite(Root + "/Art/TJ_Circle.png", true);

        UpgradeDefinition branchOne = MakeUpgrade(
            "01_Branch", "Young Branch",
            "Grow your first large branch and four fresh leaves. More leaves produce more energy every second.",
            25, 12, null,
            new UpgradeEffect { type = UpgradeEffectType.AddLeaves, amount = 4 });

        UpgradeDefinition branchTwo = MakeUpgrade(
            "02_Sunlight", "Broad Leaves",
            "Grow a higher branch with wider leaves. Improve sunlight energy production by 1.2 per second.",
            35, 22, new[] { branchOne },
            new UpgradeEffect { type = UpgradeEffectType.AddEnergyPerSecond, amount = 1.2f });

        UpgradeDefinition rootOne = MakeUpgrade(
            "03_FineRoots", "Fine Root Hairs",
            "Stretch your roots into fresh soil. Absorb 0.8 more nutrients every second.",
            20, 13, null,
            new UpgradeEffect { type = UpgradeEffectType.AddNutrientsPerSecond, amount = 0.8f });

        UpgradeDefinition rootTwo = MakeUpgrade(
            "04_DeepRoots", "Deep Reserves",
            "Grow deeper roots that can store more of what you collect: +60 nutrient capacity and +25 energy capacity.",
            30, 20, new[] { rootOne },
            new UpgradeEffect { type = UpgradeEffectType.AddNutrientCapacity, amount = 60 },
            new UpgradeEffect { type = UpgradeEffectType.AddEnergyCapacity, amount = 25 });

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 0, -10);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.1f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.50f, 0.72f, 0.78f);

        // A fresh 2D URP scene needs a Global Light 2D for lit sprites.
        GameObject globalLightObject = new GameObject("Global Light 2D");
        Light2D globalLight = globalLightObject.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.intensity = 1f;

        GameObject systems = new GameObject("Game Systems");
        TreeGameState state = systems.AddComponent<TreeGameState>();
        systems.AddComponent<TreeJamMouse>();
        TreeJamHUD hud = systems.AddComponent<TreeJamHUD>();
        hud.gameState = state;

        GameObject environment = new GameObject("Environment");
        MakeSprite("Soil", environment.transform, square,
            new Vector2(0, -3.15f), new Vector2(25, 4.3f),
            new Color(0.29f, 0.19f, 0.15f), -10);
        MakeSprite("Soil Surface", environment.transform, square,
            new Vector2(0, -1.01f), new Vector2(25, 0.08f),
            new Color(0.34f, 0.45f, 0.27f), -9);
        MakeSprite("Sun", environment.transform, circle,
            new Vector2(5.7f, 3.65f), new Vector2(1.1f, 1.1f),
            new Color(1f, 0.91f, 0.57f), -8);

        GameObject tree = new GameObject("The Tree");
        CreateSegment("Trunk", tree.transform, new Vector2(0, -1.0f), new Vector2(0, 1.07f),
            0.25f, new Color(0.37f, 0.23f, 0.15f), 1);
        CreateSegment("Small Left Branch", tree.transform, new Vector2(0, 0.45f), new Vector2(-0.55f, 1.22f),
            0.12f, new Color(0.37f, 0.23f, 0.15f), 2);
        CreateSegment("Small Right Branch", tree.transform, new Vector2(0, 0.65f), new Vector2(0.62f, 1.37f),
            0.12f, new Color(0.37f, 0.23f, 0.15f), 2);
        MakeLeaf(tree.transform, -0.55f, 1.22f, 0.30f);
        MakeLeaf(tree.transform, -0.73f, 1.15f, 0.21f);
        MakeLeaf(tree.transform, 0.62f, 1.37f, 0.30f);
        MakeLeaf(tree.transform, 0.8f, 1.42f, 0.21f);
        MakeLeaf(tree.transform, 0.0f, 1.08f, 0.26f);
        CreateSegment("Root Left", tree.transform, new Vector2(0, -1.0f), new Vector2(-0.55f, -1.68f),
            0.13f, new Color(0.52f, 0.34f, 0.23f), 1);
        CreateSegment("Root Right", tree.transform, new Vector2(0, -1.0f), new Vector2(0.5f, -1.62f),
            0.13f, new Color(0.52f, 0.34f, 0.23f), 1);

        GameObject growthGroup = new GameObject("Growth Visuals (unlocked by orbs)");
        growthGroup.transform.SetParent(tree.transform, false);
        GameObject growBranch1 = Growth("Young Branch Growth", growthGroup.transform,
            new Vector2(-0.54f, 1.18f), new Vector2(-1.40f, 2.02f), true);
        GameObject growBranch2 = Growth("Broad Leaves Growth", growthGroup.transform,
            new Vector2(-1.40f, 2.02f), new Vector2(-2.48f, 2.85f), true);
        GameObject growRoot1 = Growth("Fine Roots Growth", growthGroup.transform,
            new Vector2(-0.50f, -1.62f), new Vector2(-1.42f, -2.42f), false);
        GameObject growRoot2 = Growth("Deep Reserves Growth", growthGroup.transform,
            new Vector2(-1.42f, -2.42f), new Vector2(-2.51f, -3.23f), false);

        GameObject orbs = new GameObject("Upgrade Orbs");
        MakeOrb("Canopy 01 — Young Branch", orbs.transform, new Vector2(-1.58f, 2.23f),
            branchOne, growBranch1, state, new Color(0.70f, 1f, 0.54f));
        MakeOrb("Canopy 02 — Broad Leaves (locked)", orbs.transform, new Vector2(-2.68f, 3.05f),
            branchTwo, growBranch2, state, new Color(0.70f, 1f, 0.54f));
        MakeOrb("Roots 01 — Fine Root Hairs", orbs.transform, new Vector2(-1.58f, -2.54f),
            rootOne, growRoot1, state, new Color(0.48f, 0.83f, 1f));
        MakeOrb("Roots 02 — Deep Reserves (locked)", orbs.transform, new Vector2(-2.71f, -3.37f),
            rootTwo, growRoot2, state, new Color(0.48f, 0.83f, 1f));

        TreeJamEditableUIInstaller.InstallIntoCurrentScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, Root + "/Scenes/TreeJamStarter.unity");
        Selection.activeGameObject = systems;
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/Scenes/TreeJamStarter.unity"));
        Debug.Log("Tree Jam starter scene created! Press Play; hover and click the two visible orbs.");
    }

    private static void EnsureFolder(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    private static UpgradeDefinition MakeUpgrade(string assetName, string displayName, string description,
        float energyCost, float nutrientCost, UpgradeDefinition[] prerequisites, params UpgradeEffect[] effects)
    {
        string path = Root + "/Data/" + assetName + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path) != null)
            AssetDatabase.DeleteAsset(path);

        UpgradeDefinition definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
        definition.displayName = displayName;
        definition.description = description;
        definition.energyCost = energyCost;
        definition.nutrientCost = nutrientCost;
        definition.prerequisites = prerequisites;
        definition.effects = effects;
        AssetDatabase.CreateAsset(definition, path);
        return definition;
    }

    private static Sprite GenerateSprite(string path, bool circleShape)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
            float alpha = !circleShape || d < size * 0.47f ? 1f : 0f;
            texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 64;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject MakeSprite(string name, Transform parent, Sprite sprite,
        Vector2 position, Vector2 scale, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static void CreateSegment(string name, Transform parent, Vector2 from, Vector2 to,
        float thickness, Color color, int sortingOrder)
    {
        Vector2 direction = to - from;
        GameObject segment = MakeSprite(name, parent, square, (from + to) / 2f,
            new Vector2(direction.magnitude, thickness), color, sortingOrder);
        segment.transform.localRotation = Quaternion.Euler(0, 0,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private static void MakeLeaf(Transform parent, float x, float y, float diameter)
    {
        MakeSprite("Leaf", parent, circle, new Vector2(x, y), new Vector2(diameter, diameter),
            new Color(0.27f, 0.64f, 0.30f), 3);
    }

    private static GameObject Growth(string name, Transform parent, Vector2 start, Vector2 end, bool canopy)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = start;
        Vector2 localTip = end - start;
        Color color = canopy ? new Color(0.37f, 0.23f, 0.15f) : new Color(0.52f, 0.34f, 0.23f);
        CreateSegment("New " + (canopy ? "Branch" : "Root"), go.transform,
            Vector2.zero, localTip, canopy ? 0.13f : 0.12f, color, 2);
        if (canopy)
        {
            MakeLeaf(go.transform, localTip.x, localTip.y, 0.38f);
            MakeLeaf(go.transform, localTip.x - 0.21f, localTip.y + 0.05f, 0.25f);
            MakeLeaf(go.transform, localTip.x + 0.12f, localTip.y - 0.21f, 0.26f);
        }
        else
        {
            CreateSegment("Rootlet", go.transform, localTip * 0.68f,
                localTip + new Vector2(-0.16f, -0.35f), 0.055f, color, 2);
        }
        go.SetActive(false);
        return go;
    }

    private static void MakeOrb(string name, Transform parent, Vector2 position,
        UpgradeDefinition definition, GameObject growthVisual, TreeGameState state, Color tint)
    {
        GameObject orb = new GameObject(name);
        orb.transform.SetParent(parent, false);
        orb.transform.localPosition = position;
        CircleCollider2D collider = orb.AddComponent<CircleCollider2D>();
        collider.radius = 0.43f;
        collider.isTrigger = true;

        GameObject graphics = new GameObject("Glowing orb visuals");
        graphics.transform.SetParent(orb.transform, false);
        MakeSprite("Halo", graphics.transform, circle, Vector2.zero,
            new Vector2(0.65f, 0.65f), new Color(tint.r, tint.g, tint.b, 0.22f), 7);
        MakeSprite("Core", graphics.transform, circle, Vector2.zero,
            new Vector2(0.32f, 0.32f), tint, 8);
        UpgradeOrb script = orb.AddComponent<UpgradeOrb>();
        script.definition = definition;
        script.gameState = state;
        script.orbGraphics = graphics;
        script.growthVisual = growthVisual;
    }
}
#endif
