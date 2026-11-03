#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    /// <summary>
    /// Adds eight genuinely new branching upgrade choices to the CURRENT scene.
    /// Existing assets, orbs, tree art, HUD, and environment are preserved.
    /// Idempotent: safe to run again without duplicating orbs/assets.
    /// </summary>
    public static class SparseCanopyChoicesInstaller
    {
        private const string DataRoot = "Assets/TreeJam/Data";
        private const string CirclePath = "Assets/TreeJam/Art/TJ_Circle.png";
        private const string FallbackCirclePath = "Assets/TreeJam/Art/TJ_ChoiceCircle.png";
        private const string MaterialPath = "Assets/TreeJam/Art/TJ_ChoiceOrbUnlit.mat";

        [MenuItem("Tools/Tree Jam/Add Sparse Canopy + 8 Choices (Current Scene)")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Exit Play Mode", "Stop Play Mode first.", "OK");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            OrganicBonsaiTree tree = UnityEngine.Object.FindFirstObjectByType<OrganicBonsaiTree>();
            TreeGameState state = UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
            if (tree == null || state == null || !scene.IsValid())
            {
                EditorUtility.DisplayDialog("Tree Jam", "Open your existing Tree Jam scene with The Tree and Game Systems before running this installer.", "OK");
                return;
            }
            UpgradeDefinition firstBranch = FindUpgrade(scene, "Young Branch");
            UpgradeDefinition firstRoots = FindUpgrade(scene, "Fine Root Hairs");
            UpgradeDefinition deepRoots = FindUpgrade(scene, "Deep Reserves");
            if (firstBranch == null || firstRoots == null || deepRoots == null)
            {
                EditorUtility.DisplayDialog("Tree Jam", "Couldn't identify Young Branch, Fine Root Hairs, and Deep Reserves in this scene. This add-on expects the original four starter upgrades.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Add 8 upgrade choices?",
                "Add six canopy upgrades and two root upgrades to the CURRENT scene? " +
                "Existing orbs, their ScriptableObjects, your tree, environment and UI will NOT be replaced. " +
                "The new renderer will display only TreeGameState.LeafCount leaves at startup (normally five).",
                "Add Choices", "Cancel")) return;

            Directory.CreateDirectory(DataRoot);
            AssetDatabase.Refresh();
            Sprite circle = EnsureCircle();
            Material orbMaterial = EnsureOrbMaterial();

            UpgradeDefinition buds = GetOrCreate("05_LeafBuds", "Leaf Buds",
                "New leaves unfurl across this bough. +7 leaves, which also increase photosynthesis.",
                23, 13, new[] { firstBranch }, Effect(UpgradeEffectType.AddLeaves, 7));
            UpgradeDefinition sunward = GetOrCreate("06_SunwardTwigs", "Sunward Twigs",
                "Grow toward brighter parts of the sky. +0.9 energy per second without needing more leaves.",
                22, 13, new[] { firstBranch }, Effect(UpgradeEffectType.AddEnergyPerSecond, .9f));
            UpgradeDefinition dense = GetOrCreate("07_DenseCrown", "Dense Crown",
                "Turn your sparse boughs into leafy pads. +12 individual leaves.",
                48, 29, new[] { buds }, Effect(UpgradeEffectType.AddLeaves, 12));
            UpgradeDefinition lowlight = GetOrCreate("08_LowLightFoliage", "Low-Light Foliage",
                "Small leaves capable of using weak daylight. +6 leaves and +0.45 energy per second.",
                44, 25, new[] { sunward },
                Effect(UpgradeEffectType.AddLeaves, 6), Effect(UpgradeEffectType.AddEnergyPerSecond, .45f));
            UpgradeDefinition reserves = GetOrCreate("09_SummerReserves", "Summer Reserves",
                "Store the surplus from sunny days. +45 maximum energy.",
                40, 28, new[] { sunward }, Effect(UpgradeEffectType.AddEnergyCapacity, 45));
            UpgradeDefinition harmony = GetOrCreate("10_CrownHarmony", "Crown Harmony",
                "Your two canopy specializations come together. +10 leaves and +0.7 energy per second.",
                74, 50, new[] { dense, lowlight },
                Effect(UpgradeEffectType.AddLeaves, 10), Effect(UpgradeEffectType.AddEnergyPerSecond, .7f));
            UpgradeDefinition feeders = GetOrCreate("11_FeederRoots", "Feeder Roots",
                "Fine roots spread through fertile soil. +0.8 nutrients per second.",
                36, 24, new[] { firstRoots }, Effect(UpgradeEffectType.AddNutrientsPerSecond, .8f));
            UpgradeDefinition reservoir = GetOrCreate("12_RootReservoir", "Root Reservoir",
                "An underground reserve for lean seasons. +45 maximum nutrient storage.",
                42, 25, new[] { deepRoots }, Effect(UpgradeEffectType.AddNutrientCapacity, 45));

            GameObject group = GameObject.Find("Upgrade Orbs");
            if (group == null || group.scene != scene)
            {
                group = new GameObject("Upgrade Orbs");
                Undo.RegisterCreatedObjectUndo(group, "Create Upgrade Orbs group");
            }
            int count = 0;
            Color green = new Color(.58f, .98f, .50f, 1f);
            Color blue = new Color(.46f, .81f, 1f, 1f);
            count += MakeOrb(group.transform, state, circle, orbMaterial, green,
                "Canopy 03 - Leaf Buds", buds, new Vector2(-2.34f, 3.02f),
                new Vector2(-1.32f, 1.96f), new Vector2(-2.14f, 2.82f), -.15f, .115f, 2, false);
            count += MakeOrb(group.transform, state, circle, orbMaterial, green,
                "Canopy 04 - Sunward Twigs", sunward, new Vector2(-.24f, 2.82f),
                new Vector2(-1.14f, 1.84f), new Vector2(-.35f, 2.55f), .24f, .115f, 2, false);
            count += MakeOrb(group.transform, state, circle, orbMaterial, green,
                "Canopy 05 - Dense Crown", dense, new Vector2(-3.41f, 3.78f),
                new Vector2(-2.10f, 2.78f), new Vector2(-3.14f, 3.55f), -.20f, .12f, 3, false);
            count += MakeOrb(group.transform, state, circle, orbMaterial, green,
                "Canopy 06 - Low-Light Foliage", lowlight, new Vector2(1.02f, 3.51f),
                new Vector2(-.37f, 2.52f), new Vector2(.79f, 3.31f), .18f, .115f, 2, false);
            count += MakeOrb(group.transform, state, circle, orbMaterial, green,
                "Canopy 07 - Summer Reserves", reserves, new Vector2(-.74f, 3.81f),
                new Vector2(-.33f, 2.52f), new Vector2(-.73f, 3.52f), -.13f, .11f, 1, false);
            count += MakeOrb(group.transform, state, circle, orbMaterial, green,
                "Canopy 08 - Crown Harmony", harmony, new Vector2(-2.40f, 4.45f),
                new Vector2(-3.08f, 3.51f), new Vector2(-2.26f, 4.11f), -.16f, .10f, 2, false);
            count += MakeOrb(group.transform, state, circle, orbMaterial, blue,
                "Roots 03 - Feeder Roots", feeders, new Vector2(-3.48f, -3.80f),
                new Vector2(-1.36f, -2.40f), new Vector2(-3.22f, -3.53f), -.15f, .105f, 0, true);
            count += MakeOrb(group.transform, state, circle, orbMaterial, blue,
                "Roots 04 - Root Reservoir", reservoir, new Vector2(-1.77f, -4.42f),
                new Vector2(-2.42f, -3.20f), new Vector2(-1.75f, -4.14f), .12f, .115f, 0, true);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = tree.gameObject;
            int startingLeaves = new SerializedObject(state).FindProperty("startingLeaves").intValue;
            EditorUtility.DisplayDialog("Tree Jam", "Added " + count + " new orbs (" +
                (8 - count) + " were already present).\n\nPress Play: the starting sapling should have " +
                startingLeaves + " leaves. Purchase Young Branch, then Leaf Buds to grow foliage " +
                "on its new branch. Save your scene (Ctrl+S).", "OK");
            Debug.Log("Tree Jam: added " + count + " upgrade orbs without replacing the scene.");
        }

        private static UpgradeDefinition FindUpgrade(Scene scene, string displayName)
        {
            foreach (UpgradeOrb orb in UnityEngine.Object.FindObjectsByType<UpgradeOrb>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (orb.gameObject.scene == scene && orb.definition != null &&
                    string.Equals(orb.definition.displayName, displayName, StringComparison.OrdinalIgnoreCase))
                    return orb.definition;
            return null;
        }

        private static UpgradeEffect Effect(UpgradeEffectType type, float amount)
            => new UpgradeEffect { type = type, amount = amount };

        private static UpgradeDefinition GetOrCreate(string filename, string name, string description,
            float energy, float nutrients, UpgradeDefinition[] prerequisites, params UpgradeEffect[] effects)
        {
            string path = DataRoot + "/" + filename + ".asset";
            UpgradeDefinition result = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (result != null) return result; // keep any edits made by the user
            result = ScriptableObject.CreateInstance<UpgradeDefinition>();
            result.displayName = name;
            result.description = description;
            result.energyCost = energy;
            result.nutrientCost = nutrients;
            result.prerequisites = prerequisites;
            result.effects = effects;
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static Sprite EnsureCircle()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            if (sprite != null) return sprite;
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FallbackCirclePath);
            if (sprite != null) return sprite;
            Directory.CreateDirectory("Assets/TreeJam/Art");
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x + .5f - size / 2f, y + .5f - size / 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f,
                    delta.magnitude <= size * .47f ? 1f : 0f));
            }
            texture.Apply();
            File.WriteAllBytes(FallbackCirclePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FallbackCirclePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(FallbackCirclePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(FallbackCirclePath);
        }

        private static Material EnsureOrbMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            material = new Material(shader) { name = "TJ Choice Orbs Unlit" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // Returns one if created; zero if a prior install already created this orb.
        private static int MakeOrb(Transform group, TreeGameState state, Sprite circle,
            Material material, Color tint, string label, UpgradeDefinition upgrade,
            Vector2 orbPosition, Vector2 start, Vector2 end, float bend,
            float thickness, int clusters, bool root)
        {
            if (group.Find(label) != null) return 0;
            GameObject go = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(go, "Add " + label);
            go.transform.SetParent(group, false);
            go.transform.localPosition = orbPosition;
            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = .36f;
            collider.isTrigger = true;
            GameObject visuals = new GameObject("Glowing orb visuals");
            visuals.transform.SetParent(go.transform, false);
            AddCircle(visuals.transform, "Halo", circle, material,
                new Color(tint.r, tint.g, tint.b, .20f), .69f, 7);
            AddCircle(visuals.transform, "Core", circle, material, tint, .31f, 8);
            UpgradeOrb orb = go.AddComponent<UpgradeOrb>();
            orb.definition = upgrade;
            orb.gameState = state;
            orb.orbGraphics = visuals;
            // Existing geometry placeholders are intentionally absent: the
            // procedural renderer will grow the branch from BonsaiGrowthLink.
            BonsaiGrowthLink link = go.AddComponent<BonsaiGrowthLink>();
            link.upgrade = upgrade;
            link.isRoot = root;
            link.start = start;
            link.end = end;
            link.bend = bend;
            link.baseThickness = thickness;
            link.foliageClusters = clusters;
            return 1;
        }

        private static void AddCircle(Transform parent, string name, Sprite sprite,
            Material material, Color tint, float size, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size, size, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = order;
            if (material != null) renderer.sharedMaterial = material;
        }
    }
}
#endif
