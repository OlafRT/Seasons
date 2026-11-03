using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TreeJam;

namespace TreeJam.Editor
{
    /// <summary>
    /// Non-destructive repair of the EXISTING mushroom trading setup plus the requested
    /// day/night ratios. Does not recreate the tree, fungal routes, economy, or UI.
    /// </summary>
    public static class TreeJamMushroomDaylightHotfix
    {
        private const string MushroomSpritePath = "Assets/TreeJam/FungalMushrooms/Art/MushroomTrade.png";
        private const string MushroomMaterialPath = "Assets/TreeJam/FungalMushrooms/Art/TJ_FungalObjects_Unlit.mat";

        [MenuItem("Tools/Tree Jam/Fix Trading Mushrooms + Seasonal Daylight (Current Scene)")]
        public static void Repair()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Please exit Play Mode before applying the repair.", "OK");
                return;
            }
            TreeJamFungalMushroomSystem system = UnityEngine.Object.FindFirstObjectByType<TreeJamFungalMushroomSystem>();
            TreeJamSeasonCycle seasons = UnityEngine.Object.FindFirstObjectByType<TreeJamSeasonCycle>();
            if (system == null)
            {
                EditorUtility.DisplayDialog("Mushroom system not found",
                    "The clickable mushroom system is not in this scene. First run Tools > Tree Jam > Install Clickable Mushroom Trading (Current Scene) from the earlier patch, then use this repair.", "OK");
                return;
            }

            Sprite art = LoadMushroomSprite();
            if (art == null)
            {
                EditorUtility.DisplayDialog("Mushroom artwork missing",
                    "MushroomTrade.png was not found at " + MushroomSpritePath + ". Reimport the Art folder from the previous mushroom trading patch, then retry.", "OK");
                return;
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MushroomMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    material = new Material(shader) { name = "TJ Fungal Objects Unlit" };
                    AssetDatabase.CreateAsset(material, MushroomMaterialPath);
                }
            }

            TreeJamMyceliumNetwork network = system.network != null ? system.network : UnityEngine.Object.FindFirstObjectByType<TreeJamMyceliumNetwork>();
            TreeJamSoilSurface soil = system.ground != null ? system.ground : UnityEngine.Object.FindFirstObjectByType<TreeJamSoilSurface>();
            TreeJamMyceliumPolish polish = UnityEngine.Object.FindFirstObjectByType<TreeJamMyceliumPolish>();
            SpriteRenderer grass = system.grassSprite != null ? system.grassSprite : polish != null ? polish.grassSprite : null;
            if (grass == null)
                foreach (SpriteRenderer sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (sr.name.IndexOf("GROUND SURFACE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sr.name.IndexOf("GRASS", StringComparison.OrdinalIgnoreCase) >= 0)
                    { grass = sr; break; }

            Undo.RecordObject(system, "Repair mushroom references and visibility");
            system.network = network;
            system.ground = soil;
            system.resources = system.resources != null ? system.resources : UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
            system.worldCamera = system.worldCamera != null ? system.worldCamera : Camera.main;
            system.seasons = seasons;
            system.mushroomArt = art;
            system.mushroomMaterial = material;
            system.grassSprite = grass;
            system.firstFruitDelay = .5f;
            system.mushroomBloomSeconds = 3f;
            EditorUtility.SetDirty(system);

            TreeJamFungiMarket market = system.market != null ? system.market : UnityEngine.Object.FindFirstObjectByType<TreeJamFungiMarket>();
            if (market != null)
            {
                Undo.RecordObject(market, "Repair mushroom market references");
                market.mushroomTradingMode = true;
                if (network != null) market.network = network;
                if (market.fungalRoute == null) market.fungalRoute = UnityEngine.Object.FindFirstObjectByType<TreeJamFungalRoute>();
                system.market = market;
                if (market.openButton != null) market.openButton.gameObject.SetActive(false);
                if (market.panel != null) market.panel.SetActive(false);
                EditorUtility.SetDirty(market);
            }

            // The decorative fungus used to leave bare stems when its caps were hidden.
            // Only actual clickable trading mushrooms should represent trading locations.
            if (polish != null)
            {
                Undo.RecordObject(polish, "Disable legacy decorative mushroom sprouts");
                polish.showDecorativeFruits = false;
                polish.hideLegacyFloatingMushrooms = true;
                EditorUtility.SetDirty(polish);
            }

            int repaired = 0;
            TreeJamTradeMushroom[] fruits = system.GetComponentsInChildren<TreeJamTradeMushroom>(true);
            foreach (TreeJamTradeMushroom fruit in fruits)
            {
                if (fruit == null) continue;
                Undo.RecordObject(fruit, "Repair trading mushroom");
                fruit.gameObject.SetActive(true);
                if (fruit.visualScale.x < .3f || fruit.visualScale.y < .3f)
                    fruit.visualScale = new Vector3(.42f, .42f, 1f);

                Collider2D click = fruit.GetComponent<Collider2D>();
                if (click == null) click = Undo.AddComponent<CircleCollider2D>(fruit.gameObject);
                if (click is CircleCollider2D circle)
                {
                    Undo.RecordObject(circle, "Repair mushroom click area");
                    circle.isTrigger = true;
                    circle.radius = .95f;
                    circle.offset = new Vector2(0f, .70f);
                    circle.enabled = true;
                    EditorUtility.SetDirty(circle);
                }

                SpriteRenderer sprite = fruit.GetComponentInChildren<SpriteRenderer>(true);
                if (sprite == null)
                {
                    GameObject artObject = new GameObject("Mushroom art - replace sprite here");
                    Undo.RegisterCreatedObjectUndo(artObject, "Restore mushroom sprite");
                    artObject.transform.SetParent(fruit.transform, false);
                    artObject.transform.localPosition = new Vector3(0f, .70f, 0f);
                    sprite = Undo.AddComponent<SpriteRenderer>(artObject);
                }
                Undo.RecordObject(sprite, "Restore trading mushroom sprite");
                sprite.gameObject.SetActive(true);
                if (sprite.sprite == null || sprite.sprite.name == "MushroomTrade") sprite.sprite = art;
                if (material != null) sprite.sharedMaterial = material;
                sprite.color = Color.white;
                sprite.enabled = true;
                if (grass != null)
                {
                    sprite.sortingLayerID = grass.sortingLayerID;
                    sprite.sortingOrder = grass.sortingOrder + 5;
                }
                if (sprite.transform.localPosition.y < .2f)
                {
                    Undo.RecordObject(sprite.transform, "Raise mushroom cap above soil");
                    sprite.transform.localPosition = new Vector3(0f, .70f, 0f);
                }
                EditorUtility.SetDirty(fruit);
                EditorUtility.SetDirty(sprite);
                repaired++;
            }

            if (seasons != null)
            {
                Undo.RecordObject(seasons, "Set seasonal day/night ratios");
                // Center daylight on 12:00. The day always lasts the same real-time
                // number of seconds; these values change the day/night proportions.
                seasons.springDaylight = new Vector2(4.8f, 19.2f); // day:night = 1.5:1
                seasons.summerDaylight = new Vector2(4f, 20f);      // day:night = 2:1
                seasons.autumnDaylight = new Vector2(4.8f, 19.2f); // day:night = 1.5:1
                seasons.winterDaylight = new Vector2(8f, 16f);     // night:day = 2:1
                EditorUtility.SetDirty(seasons);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = system.gameObject;
            EditorUtility.DisplayDialog("Mushroom & daylight repair applied",
                "Restored " + repaired + " trading mushroom(s), their sprites and rendering order. Set short first-fruit timing and seasonal daylight ratios." +
                (seasons == null ? "\n\nWARNING: TreeJamSeasonCycle wasn't found, so daylight settings were not changed." :
                "\n\nSpring 60% day; Summer 66.7%; Autumn 60%; Winter 33.3%.") +
                "\n\nSave the scene. After the network connects, the mushrooms should bud quickly and open within 3 seconds. You can also use the component's TEST command in Play Mode.", "OK");
        }

        private static Sprite LoadMushroomSprite()
        {
            TextureImporter importer = AssetImporter.GetAtPath(MushroomSpritePath) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(MushroomSpritePath);
        }
    }
}
