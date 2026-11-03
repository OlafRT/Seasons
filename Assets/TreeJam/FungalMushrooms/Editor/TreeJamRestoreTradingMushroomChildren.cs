#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TreeJam.Editor
{
    /// <summary>
    /// Restores the missing CHILD OBJECTS of an installed mushroom trading system.
    /// Does not replace any scene controllers, scripts, or upgrade definitions.
    /// Safe to run repeatedly: each named mushroom/upgrade is only created when missing.
    /// </summary>
    public static class TreeJamRestoreTradingMushroomChildren
    {
        private const string SpritePath = "Assets/TreeJam/FungalMushrooms/Art/MushroomTrade.png";
        private const string UpgradeSpritePath = "Assets/TreeJam/FungalMushrooms/Art/FungalUpgradeOrb.png";
        private const string MaterialPath = "Assets/TreeJam/FungalMushrooms/Art/TJ_FungalObjects_Unlit.mat";
        private const string MushroomHolderName = "MUSHROOMS - drag X offset to reposition";
        private const string UpgradeHolderName = "MYCELIUM UPGRADES - editable nodes";

        [MenuItem("Tools/Tree Jam/RESTORE Missing Trading Mushrooms (Current Scene)")]
        public static void Restore()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Stop Play Mode first. This repair saves mushroom objects into the scene.", "OK");
                return;
            }

            TreeJamFungalMushroomSystem system = UnityEngine.Object.FindFirstObjectByType<TreeJamFungalMushroomSystem>();
            if (system == null)
            {
                EditorUtility.DisplayDialog("Trading system missing", "No TreeJamFungalMushroomSystem was found in the active scene. Do not create a second system; check whether FUNGAL MUSHROOM TRADING - EDIT ME is disabled.", "OK");
                return;
            }
            Sprite mushroomSprite = GetSprite(SpritePath);
            if (mushroomSprite == null)
            {
                EditorUtility.DisplayDialog("Mushroom art missing", "Could not load MushroomTrade.png from " + SpritePath + ". Import the Art folder from the earlier mushroom patch, then run this repair again.", "OK");
                return;
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    material = new Material(shader) { name = "TJ Fungal Objects Unlit" };
                    AssetDatabase.CreateAsset(material, MaterialPath);
                }
            }

            Undo.RecordObject(system, "Repair mushroom system references");
            if (system.network == null) system.network = UnityEngine.Object.FindFirstObjectByType<TreeJamMyceliumNetwork>();
            if (system.ground == null) system.ground = UnityEngine.Object.FindFirstObjectByType<TreeJamSoilSurface>();
            if (system.resources == null) system.resources = UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
            if (system.market == null) system.market = UnityEngine.Object.FindFirstObjectByType<TreeJamFungiMarket>();
            if (system.worldCamera == null) system.worldCamera = Camera.main;
            if (system.seasons == null) system.seasons = UnityEngine.Object.FindFirstObjectByType<TreeJamSeasonCycle>();
            if (system.grassSprite == null)
            {
                TreeJamMyceliumPolish polish = UnityEngine.Object.FindFirstObjectByType<TreeJamMyceliumPolish>();
                if (polish != null) system.grassSprite = polish.grassSprite;
            }
            system.mushroomArt = mushroomSprite;
            if (material != null) system.mushroomMaterial = material;
            EditorUtility.SetDirty(system);

            Transform holder = GetOrMakeHolder(system.transform, MushroomHolderName);
            int createdMushrooms = 0;
            createdMushrooms += RestoreMushroom(holder, system, "LEFT - Energy to Nutrients", true, true, false, 2.75f) ? 1 : 0;
            createdMushrooms += RestoreMushroom(holder, system, "RIGHT - Nutrients to Energy", false, false, false, 2.75f) ? 1 : 0;
            createdMushrooms += RestoreMushroom(holder, system, "BONUS LEFT - Spore Colony", true, true, true, 3.90f) ? 1 : 0;
            createdMushrooms += RestoreMushroom(holder, system, "BONUS RIGHT - Spore Colony", false, false, true, 3.90f) ? 1 : 0;

            int createdUpgrades = 0;
            Sprite orbSprite = GetSprite(UpgradeSpritePath);
            if (orbSprite != null)
            {
                Transform upgrades = GetOrMakeHolder(system.transform, UpgradeHolderName);
                createdUpgrades += RestoreUpgrade(upgrades, system, "F01 - Rapid Fruiting", FungalUpgradeKind.RapidFruiting,
                    "Mushrooms regrow 30% faster.", 36, 18, false, FungalUpgradeKind.RapidFruiting, -3.1f, 1.4f, orbSprite) ? 1 : 0;
                createdUpgrades += RestoreUpgrade(upgrades, system, "F02 - Courier Hyphae", FungalUpgradeKind.CourierHyphae,
                    "Resource packets travel 30% faster.", 39, 22, false, FungalUpgradeKind.CourierHyphae, 3.0f, 1.4f, orbSprite) ? 1 : 0;
                createdUpgrades += RestoreUpgrade(upgrades, system, "F03 - Better Returns", FungalUpgradeKind.BetterReturns,
                    "Receive 20% more resources from each trade.", 55, 30, true, FungalUpgradeKind.RapidFruiting, -4.2f, 2.1f, orbSprite) ? 1 : 0;
                createdUpgrades += RestoreUpgrade(upgrades, system, "F04 - Spore Colony", FungalUpgradeKind.SporeColony,
                    "Grow one extra trading mushroom on each side.", 60, 34, true, FungalUpgradeKind.CourierHyphae, 4.15f, 2.1f, orbSprite) ? 1 : 0;
            }
            else Debug.LogWarning("Tree Jam: FungalUpgradeOrb.png was not found; existing upgrade nodes were left alone.", system);

            RestoreTooltipIfMissing(system);
            if (system.market != null)
            {
                Undo.RecordObject(system.market, "Keep fungal mail mode");
                system.market.mushroomTradingMode = true;
                if (system.market.openButton != null) system.market.openButton.gameObject.SetActive(false);
                if (system.market.panel != null) system.market.panel.SetActive(false);
                EditorUtility.SetDirty(system.market);
            }

            int totalMushrooms = system.GetComponentsInChildren<TreeJamTradeMushroom>(true).Length;
            int totalUpgrades = system.GetComponentsInChildren<TreeJamFungalUpgradeNode>(true).Length;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = system.gameObject;
            Debug.Log($"Tree Jam: Restored {createdMushrooms} missing trading mushrooms, {createdUpgrades} missing fungal upgrade nodes. Now: {totalMushrooms} mushroom(s), {totalUpgrades} fungal upgrade(s).", system);
            EditorUtility.DisplayDialog("Trading mushrooms restored",
                $"Added {createdMushrooms} missing mushroom object(s) and {createdUpgrades} missing fungal upgrade object(s).\n\n" +
                $"Now found: {totalMushrooms} mushrooms and {totalUpgrades} fungal upgrades.\n\n" +
                "Save your scene. In Play Mode, after network connection, the two regular mushrooms should grow; the two BONUS mushrooms appear after Spore Colony is bought. Use DEBUG - Report trading mushroom visibility to verify count=4.", "OK");
        }

        private static Transform GetOrMakeHolder(Transform parent, string name)
        {
            Transform holder = parent.Find(name);
            if (holder == null)
            {
                GameObject go = NewChild(parent, name);
                holder = go.transform;
            }
            if (!holder.gameObject.activeSelf) holder.gameObject.SetActive(true);
            return holder;
        }

        private static bool RestoreMushroom(Transform holder, TreeJamFungalMushroomSystem system,
            string name, bool left, bool payEnergy, bool bonus, float xOffset)
        {
            // Respect any matching existing mushroom anywhere under this controller.
            foreach (TreeJamTradeMushroom existing in system.GetComponentsInChildren<TreeJamTradeMushroom>(true))
            {
                if (existing.name == name) return false;
            }
            GameObject go = NewChild(holder, name);
            CircleCollider2D circle = Undo.AddComponent<CircleCollider2D>(go);
            circle.isTrigger = true;
            circle.radius = .95f;
            circle.offset = new Vector2(0f, .70f);
            TreeJamTradeMushroom fruit = Undo.AddComponent<TreeJamTradeMushroom>(go);
            fruit.leftSide = left;
            fruit.payEnergy = payEnergy;
            fruit.extraColony = bonus;
            fruit.xOffsetFromTree = xOffset;
            fruit.visualScale = new Vector3(.42f, .42f, 1f);
            fruit.clickCollider = circle;
            fruit.nextGrowthAt = -1f;
            fruit.growingSince = -1f;

            GameObject visual = NewChild(go.transform, "Mushroom art - replace sprite here");
            visual.transform.localPosition = new Vector3(0f, .70f, 0f);
            SpriteRenderer sr = Undo.AddComponent<SpriteRenderer>(visual);
            sr.sprite = system.mushroomArt;
            sr.sharedMaterial = system.mushroomMaterial;
            sr.color = Color.white;
            if (system.grassSprite != null)
            {
                sr.sortingLayerID = system.grassSprite.sortingLayerID;
                sr.sortingOrder = system.grassSprite.sortingOrder + 5;
            }
            else sr.sortingOrder = 30;
            float treeX = system.network != null && system.network.treeRoot != null ? system.network.treeRoot.position.x : 0f;
            float x = treeX + (left ? -xOffset : xOffset);
            float y = system.ground != null ? system.ground.HeightOr(x, -1.3f) : -1.3f;
            go.transform.position = new Vector3(x, y + system.surfaceOffsetY, -.05f);
            EditorUtility.SetDirty(fruit);
            return true;
        }

        private static bool RestoreUpgrade(Transform holder, TreeJamFungalMushroomSystem system,
            string name, FungalUpgradeKind kind, string description, float energy, float nutrients,
            bool requires, FungalUpgradeKind prerequisite, float x, float depth, Sprite art)
        {
            // The enum identity is more reliable than the object name if the user renamed a node.
            foreach (TreeJamFungalUpgradeNode existing in system.GetComponentsInChildren<TreeJamFungalUpgradeNode>(true))
                if (existing.upgrade == kind) return false;
            GameObject go = NewChild(holder, name);
            float treeX = system.network != null && system.network.treeRoot != null ? system.network.treeRoot.position.x : 0f;
            float px = treeX + x;
            float py = (system.ground != null ? system.ground.HeightOr(px, -1.3f) : -1.3f) - depth;
            go.transform.position = new Vector3(px, py, 0f);
            CircleCollider2D collider = Undo.AddComponent<CircleCollider2D>(go);
            collider.isTrigger = true;
            collider.radius = .26f;
            TreeJamFungalUpgradeNode node = Undo.AddComponent<TreeJamFungalUpgradeNode>(go);
            node.upgrade = kind;
            node.displayName = name.Substring(6);
            node.description = description;
            node.energyCost = energy;
            node.nutrientCost = nutrients;
            node.hasPrerequisite = requires;
            node.prerequisite = prerequisite;
            GameObject visual = NewChild(go.transform, "Glowing mycelium upgrade orb");
            SpriteRenderer sr = Undo.AddComponent<SpriteRenderer>(visual);
            sr.sprite = art;
            sr.sharedMaterial = system.mushroomMaterial;
            sr.color = new Color(.71f, .94f, .83f, 1f);
            sr.sortingOrder = 8;
            visual.transform.localScale = Vector3.one * .42f;
            EditorUtility.SetDirty(node);
            return true;
        }

        private static void RestoreTooltipIfMissing(TreeJamFungalMushroomSystem system)
        {
            if (system.rootCanvas != null && system.fungalTooltip != null && system.tooltipTitle != null) return;
            TreeJamUI ui = UnityEngine.Object.FindFirstObjectByType<TreeJamUI>();
            if (ui == null || ui.rootCanvas == null || ui.tooltipPanel == null) return;
            Undo.RecordObject(system, "Repair mushroom tooltip references");
            if (system.rootCanvas == null) system.rootCanvas = ui.rootCanvas;
            if (system.fungalTooltip == null)
            {
                GameObject tooltip = UnityEngine.Object.Instantiate(ui.tooltipPanel.gameObject, ui.tooltipPanel.parent);
                tooltip.name = "Fungal Tooltip - edit layout here";
                Undo.RegisterCreatedObjectUndo(tooltip, "Restore fungal tooltip");
                tooltip.SetActive(false);
                foreach (Graphic graphic in tooltip.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
                system.fungalTooltip = tooltip.GetComponent<RectTransform>();
            }
            if (system.fungalTooltip != null)
            {
                if (system.tooltipTitle == null) system.tooltipTitle = FindText(system.fungalTooltip, "Upgrade Name");
                if (system.tooltipDescription == null) system.tooltipDescription = FindText(system.fungalTooltip, "Description");
                if (system.tooltipCosts == null) system.tooltipCosts = FindText(system.fungalTooltip, "Costs");
                if (system.tooltipHint == null) system.tooltipHint = FindText(system.fungalTooltip, "Purchase Hint");
            }
            EditorUtility.SetDirty(system);
        }

        private static Text FindText(Transform root, string name)
        {
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
                if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        private static GameObject NewChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Restore mushroom objects");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite GetSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
