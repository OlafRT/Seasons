#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TreeJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Non-destructively extends the existing 21-node V2 bonsai graph to 36 nodes.
/// Changes only UpgradeDefinition assets and adds new orb GameObjects/assets.
/// No existing gameplay MonoScript or .meta file is moved or replaced.
/// </summary>
public static class TreeJamGrowthExpansionV3
{
    private const string GroupName = "UPGRADE ORBS V2 - DRAG TO SHAPE TREE";
    private const string MarkerName = "GROWTH EXPANSION V3 - installed (36 orbs)";
    private const string DataFolder = "Assets/TreeJam/DataV3";
    private const string BackupFolder = "Assets/TreeJam/UpgradeBackups/Before36";

    private struct EffectSpec
    {
        public UpgradeEffectType Type;
        public float Amount;
        public EffectSpec(UpgradeEffectType type, float amount) { Type = type; Amount = amount; }
    }

    private sealed class Balance
    {
        public string Id, Description;
        public EffectSpec[] Effects;
        public Balance(string id, string description, params EffectSpec[] effects)
        {
            Id = id; Description = description; Effects = effects;
        }
    }

    private sealed class NewNode
    {
        public string Id, Name, Description, Parent;
        public Vector2 Offset;
        public float Energy, Nutrients, Bend, Thickness;
        public int FoliageClusters;
        public bool IsRoot;
        public EffectSpec[] Effects;
        public NewNode(string id, string name, string desc, string parent,
            float dx, float dy, float energy, float nutrients, bool isRoot,
            float bend, float thickness, int clusters, params EffectSpec[] effects)
        {
            Id=id; Name=name; Description=desc; Parent=parent; Offset=new Vector2(dx,dy);
            Energy=energy; Nutrients=nutrients; IsRoot=isRoot;
            Bend=bend; Thickness=thickness; FoliageClusters=clusters; Effects=effects;
        }
    }

    private static EffectSpec L(float v) => new EffectSpec(UpgradeEffectType.AddLeaves, v);
    private static EffectSpec E(float v) => new EffectSpec(UpgradeEffectType.AddEnergyPerSecond, v);
    private static EffectSpec N(float v) => new EffectSpec(UpgradeEffectType.AddNutrientsPerSecond, v);
    private static EffectSpec EC(float v) => new EffectSpec(UpgradeEffectType.AddEnergyCapacity, v);
    private static EffectSpec NC(float v) => new EffectSpec(UpgradeEffectType.AddNutrientCapacity, v);

    // These descriptions describe what the PURCHASE does, not what it might unlock later.
    // An exclusive choice is called out because it is an immediate, irreversible consequence.
    private static readonly Balance[] Existing =
    {
        new Balance("C01", "Grow a western bough. Add 2 leaves.", L(2)),
        new Balance("C02", "Grow an upward shoot. Add 2 leaves.", L(2)),
        new Balance("C03", "Grow an eastern bough. Add 2 leaves.", L(2)),
        new Balance("C04", "Add 6 leaves. Exclusive with Open Canopy.", L(6)),
        new Balance("C05", "Increase peak sunlight energy by 0.85/s. Exclusive with Leaf Factory.", E(.85f)),
        new Balance("C06", "Add 5 leaves. Exclusive with Sap Storage.", L(5)),
        new Balance("C07", "Increase maximum energy by 30. Exclusive with Fine Shoots.", EC(30)),
        new Balance("C08", "Add 6 leaves. Exclusive with Light Catcher.", L(6)),
        new Balance("C09", "Increase peak sunlight energy by 0.85/s. Exclusive with Rich Crown.", E(.85f)),
        new Balance("C10", "Grow the western crown. Add 5 leaves.", L(5)),
        new Balance("C11", "Increase peak sunlight energy by 0.9/s.", E(.9f)),
        new Balance("C12", "Grow the upper crown. Add 5 leaves.", L(5)),
        new Balance("C13", "Increase maximum energy by 30 and peak sunlight energy by 0.25/s.", EC(30), E(.25f)),
        new Balance("C14", "Grow the eastern crown. Add 5 leaves.", L(5)),
        new Balance("C15", "Increase peak sunlight energy by 0.9/s.", E(.9f)),
        new Balance("R01", "Grow shallow root hairs. Increase nutrients by 0.45/s.", N(.45f)),
        new Balance("R02", "Grow deeper roots. Increase maximum nutrients by 25 and energy by 12.", NC(25), EC(12)),
        new Balance("R03", "Increase nutrients by 0.65/s. Exclusive with Wide Root Net.", N(.65f)),
        new Balance("R04", "Increase nutrients by 0.25/s and maximum nutrients by 30. Exclusive with Shallow Feeders.", N(.25f), NC(30)),
        new Balance("R05", "Increase nutrients by 0.5/s and maximum nutrients by 15. Exclusive with Root Vault.", N(.5f), NC(15)),
        new Balance("R06", "Increase maximum nutrients by 40 and maximum energy by 15. Exclusive with Deep Tap.", NC(40), EC(15)),
    };

    // Positions are OFFSETS from their already-positioned parent orb. Reposition any new
    // orb in Scene view: BonsaiGrowthLink.growToOrb makes the branch follow that orb.
    // Children of exclusive nodes remain appropriately locked by their parent prerequisite.
    private static readonly NewNode[] Added =
    {
        new NewNode("C16", "Western Leaflets", "Extend the western canopy. Add 4 leaves.",
            "C10", -.62f,.55f, 84,52,false,-.12f,.08f,2, L(4)),
        new NewNode("C17", "Brightwood Tips", "Increase peak sunlight energy by 0.45/s.",
            "C11", -.37f,.58f, 80,49,false,.13f,.08f,1, E(.45f)),
        new NewNode("C18", "High Buds", "Grow the highest shoots. Add 4 leaves.",
            "C12", -.10f,.56f, 82,50,false,-.11f,.08f,2, L(4)),
        new NewNode("C19", "Sapwood Chambers", "Increase maximum energy by 30.",
            "C13", .37f,.57f, 76,46,false,.10f,.08f,1, EC(30)),
        new NewNode("C20", "Eastern Leaflets", "Extend the eastern canopy. Add 4 leaves.",
            "C14", .62f,.55f, 84,52,false,.12f,.08f,2, L(4)),
        new NewNode("C21", "High-Light Shoots", "Increase peak sunlight energy by 0.45/s.",
            "C15", .37f,.58f, 80,49,false,-.13f,.08f,1, E(.45f)),

        // Optional side branches remain available whether the player chooses leaves or
        // efficiency on the main specialization path. This prevents linear stepping stones.
        new NewNode("C22", "West Side Twig", "Grow a side twig. Add 3 leaves.",
            "C01", -.76f,.32f, 39,19,false,-.18f,.075f,2, L(3)),
        new NewNode("C23", "Center Side Twig", "Grow a small central twig. Add 3 leaves.",
            "C02", .11f,.62f, 39,19,false,.10f,.075f,2, L(3)),
        new NewNode("C24", "East Side Twig", "Grow a side twig. Add 3 leaves.",
            "C03", .76f,.32f, 39,19,false,.18f,.075f,2, L(3)),

        new NewNode("R07", "Rootlet Tips", "Increase nutrient production by 0.35/s.",
            "R03", -.69f,-.67f, 78,45,true,-.12f,.075f,0, N(.35f)),
        new NewNode("R08", "Soil Pockets", "Increase maximum nutrients by 25.",
            "R04", -.38f,-.73f, 75,44,true,.12f,.075f,0, NC(25)),
        new NewNode("R09", "Mineral Rootlets", "Increase nutrient production by 0.35/s.",
            "R05", .22f,-.71f, 78,45,true,-.12f,.075f,0, N(.35f)),
        new NewNode("R10", "Deep Root Vault", "Increase maximum nutrients by 35.",
            "R06", .67f,-.69f, 75,43,true,.12f,.075f,0, NC(35)),
        new NewNode("R11", "Lateral Rootlets", "Grow sideways roots. Increase nutrients by 0.25/s.",
            "R01", -.73f,-.39f, 38,21,true,-.14f,.08f,0, N(.25f)),
        new NewNode("R12", "Anchoring Roots", "Grow thicker roots. Increase maximum nutrients by 15 and energy by 10.",
            "R02", .73f,-.39f, 39,22,true,.14f,.08f,0, NC(15), EC(10)),
    };

    [MenuItem("Tools/Tree Jam/Expand to 36 Growth Orbs (Current Scene)")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Stop Play Mode", "Exit Play Mode before expanding the orb graph.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("No scene", "Open your playable Tree Jam scene first.", "OK");
            return;
        }

        Transform group = FindGroup(scene);
        if (group == null)
        {
            EditorUtility.DisplayDialog("V2 orbs not found",
                "This add-on needs 'UPGRADE ORBS V2 - DRAG TO SHAPE TREE'. Run the V2 installer first. Nothing changed.", "OK");
            return;
        }
        if (!group.gameObject.activeInHierarchy)
        {
            EditorUtility.DisplayDialog("V2 orbs are inactive",
                "Activate the existing V2 orb group first. Nothing changed.", "OK");
            return;
        }
        if (group.Find(MarkerName) != null || group.Find("C16 | Western Leaflets") != null)
        {
            EditorUtility.DisplayDialog("Already expanded",
                "The 36-orb expansion is already present. No objects or upgrade settings were changed.", "OK");
            return;
        }

        // Preflight ALL references BEFORE modifying any asset or scene object.
        Dictionary<string, UpgradeOrb> existing = new Dictionary<string, UpgradeOrb>();
        foreach (Transform child in group)
        {
            string id = child.name.Split('|')[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            UpgradeOrb orb = child.GetComponent<UpgradeOrb>();
            if (orb != null && !existing.ContainsKey(id)) existing.Add(id, orb);
        }
        List<string> problems = new List<string>();
        foreach (Balance b in Existing)
        {
            UpgradeOrb orb;
            if (!existing.TryGetValue(b.Id, out orb) || orb == null)
            {
                problems.Add(b.Id + " orb missing"); continue;
            }
            if (orb.definition == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(orb.definition)))
                problems.Add(b.Id + " UpgradeOrb.Definition missing or not a saved asset");
            if (orb.GetComponent<BonsaiGrowthLink>() == null)
                problems.Add(b.Id + " BonsaiGrowthLink missing");
            if (orb.orbGraphics == null)
                problems.Add(b.Id + " Orb Graphics missing");
            if (orb.gameState == null)
                problems.Add(b.Id + " Game State missing");
        }
        foreach (NewNode spec in Added)
        {
            if (!existing.ContainsKey(spec.Parent))
                problems.Add(spec.Id + " parent " + spec.Parent + " missing");
            if (existing.ContainsKey(spec.Id))
                problems.Add(spec.Id + " already exists (partial installation)");
        }
        if (problems.Count > 0)
        {
            EditorUtility.DisplayDialog("Repair V2 before expanding",
                "No changes were made. These references need attention:\n\n" +
                string.Join("\n", problems.ToArray()), "OK");
            return;
        }
        bool confirmed = EditorUtility.DisplayDialog("Expand the tree to 36 orbs?",
            "This will:\n" +
            "• Keep all 21 existing orbs in their current positions.\n" +
            "• Shorten their descriptions to explain immediate effects only.\n" +
            "• Reduce the strongest existing bonuses to slow progression.\n" +
            "• Create 15 NEW orbs, each growing toward its own position.\n" +
            "• Back up all 21 original upgrade assets automatically.\n\n" +
            "No gameplay scripts, shaders, camera, season system, UI, or artwork will be replaced.",
            "Expand to 36", "Cancel");
        if (!confirmed) return;

        EnsureFolder(DataFolder);
        EnsureFolder(BackupFolder);
        foreach (Balance b in Existing)
        {
            UpgradeDefinition def = existing[b.Id].definition;
            string assetPath = AssetDatabase.GetAssetPath(def);
            string backup = BackupFolder + "/Before36_" + b.Id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(backup) == null && !AssetDatabase.CopyAsset(assetPath, backup))
            {
                EditorUtility.DisplayDialog("Backup failed",
                    "Couldn't back up " + assetPath + ". No descriptions or effects have been changed.", "OK");
                return;
            }
        }
        AssetDatabase.SaveAssets();

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Expand Tree Jam to 36 orbs");
        foreach (Balance b in Existing)
        {
            UpgradeDefinition def = existing[b.Id].definition;
            Undo.RecordObject(def, "Rebalance " + def.displayName);
            def.description = b.Description;
            def.effects = ConvertEffects(b.Effects);
            EditorUtility.SetDirty(def);
        }

        int created = 0;
        foreach (NewNode spec in Added)
        {
            UpgradeOrb parentOrb = existing[spec.Parent];
            // Cloning the appropriate existing orb PRESERVES the user's glowing material,
            // collider, sprite styling, and the V2 UpgradeOrb/BonsaiGrowthLink setup.
            GameObject clone = UnityEngine.Object.Instantiate(parentOrb.gameObject, group);
            clone.name = spec.Id + " | " + spec.Name;
            clone.SetActive(true);
            clone.transform.localPosition = parentOrb.transform.localPosition +
                new Vector3(spec.Offset.x, spec.Offset.y, 0f);
            Undo.RegisterCreatedObjectUndo(clone, "Add " + spec.Name + " orb");

            UpgradeOrb orb = clone.GetComponent<UpgradeOrb>();
            BonsaiGrowthLink link = clone.GetComponent<BonsaiGrowthLink>();
            string path = DataFolder + "/V3_" + spec.Id + ".asset";
            UpgradeDefinition asset = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (asset == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    Debug.LogError("TreeJam: An unrelated asset occupies " + path + ". Stop and inspect this path.");
                    continue;
                }
                asset = ScriptableObject.CreateInstance<UpgradeDefinition>();
                asset.displayName = spec.Name;
                asset.description = spec.Description;
                asset.energyCost = spec.Energy;
                asset.nutrientCost = spec.Nutrients;
                asset.prerequisites = new[] { parentOrb.definition };
                asset.exclusiveGroup = "";
                asset.effects = ConvertEffects(spec.Effects);
                AssetDatabase.CreateAsset(asset, path);
            }

            orb.definition = asset;
            orb.gameState = parentOrb.gameState;
            orb.growthVisual = null; // procedural bonsai draws the growth, not old geometry
            link.upgrade = asset;
            link.parentOrb = parentOrb;
            link.isRoot = spec.IsRoot;
            link.growToOrb = true;
            link.start = Vector2.zero;
            link.end = Vector2.zero;
            link.bend = spec.Bend;
            link.baseThickness = spec.Thickness;
            link.foliageClusters = spec.FoliageClusters;
            EditorUtility.SetDirty(orb);
            EditorUtility.SetDirty(link);
            created++;
        }
        GameObject marker = new GameObject(MarkerName);
        marker.transform.SetParent(group, false);
        Undo.RegisterCreatedObjectUndo(marker, "Mark 36-orb expansion installed");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = group.gameObject;
        Debug.Log("Tree Jam: rebalanced 21 existing V2 orb assets and added " + created +
                  " new orbs. Total expected: " + (21 + created) + ". Original assets backed up at " + BackupFolder);
        EditorUtility.DisplayDialog("Growth expansion installed",
            "Updated 21 existing upgrade descriptions and effects. Added " + created +
            " orbs without moving the original ones.\n\n" +
            "Your original UpgradeDefinition assets were backed up at:\n" + BackupFolder +
            "\n\nSave your scene (Ctrl+S). Expand the V2 orb group to position the new orbs.", "OK");
    }

    private static UpgradeEffect[] ConvertEffects(EffectSpec[] effects)
    {
        UpgradeEffect[] result = new UpgradeEffect[effects.Length];
        for (int i=0;i<effects.Length;i++)
            result[i] = new UpgradeEffect { type=effects[i].Type, amount=effects[i].Amount };
        return result;
    }

    private static Transform FindGroup(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursively(root.transform, GroupName);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindRecursively(Transform parent, string wanted)
    {
        if (parent.name == wanted) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindRecursively(child, wanted);
            if (found != null) return found;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i=1;i<parts.Length;i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
