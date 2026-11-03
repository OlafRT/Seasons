#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TreeJam;

/// <summary>
/// Non-destructive upgrade graph replacement. Keeps the existing environment,
/// Canvas, bonsai, legacy asset definitions and legacy orbs (inactive).
/// All V2 branches use orbiting endpoints and explicit parent relationships.
/// </summary>
public static class BranchingChoicesV2Installer
{
    private const string DataFolder = "Assets/TreeJam/DataV2";
    private const string OrbFolder = "Assets/TreeJam/Art";
    private const string OrbTexturePath = OrbFolder + "/TJ_Circle.png";
    private const string NewCirclePath = OrbFolder + "/TJ_BranchingCircle.png";
    private const string MaterialPath = OrbFolder + "/TJ_BranchingUnlit.mat";
    private const string GroupName = "UPGRADE ORBS V2 - DRAG TO SHAPE TREE";

    private sealed class Node
    {
        public string id, name, desc, parent, choice;
        public float energy, nutrients, bend, thickness;
        public int clusters;
        public Vector2 pos, start;
        public bool root;
        public UpgradeEffect[] effects;
        public UpgradeDefinition definition;
        public UpgradeOrb orb;
    }

    private static readonly List<Node> nodes = new List<Node>();
    private static UpgradeEffect E(UpgradeEffectType type, float amount)
        => new UpgradeEffect { type = type, amount = amount };

    private static void Add(string id, string name, string description,
        float energy, float nutrients, string parent, string choice,
        bool root, Vector2 position, Vector2 start, float bend, float thickness,
        int clusters, params UpgradeEffect[] effects)
    {
        nodes.Add(new Node { id=id, name=name, desc=description,
            energy=energy, nutrients=nutrients, parent=parent, choice=choice,
            root=root, pos=position, start=start, bend=bend, thickness=thickness,
            clusters=clusters, effects=effects });
    }

    [MenuItem("Tools/Tree Jam/Install Branching Choices V2 (Current Scene)")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Stop Play Mode", "Stop Play Mode first; this changes the current scene.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        OrganicBonsaiTree tree = UnityEngine.Object.FindFirstObjectByType<OrganicBonsaiTree>();
        TreeGameState state = UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
        if (tree == null || state == null || !scene.IsValid())
        {
            EditorUtility.DisplayDialog("Tree Jam", "Open the current Tree Jam scene first. It must contain The Tree (OrganicBonsaiTree) and Game Systems (TreeGameState).", "OK");
            return;
        }
        GameObject existing = GameObject.Find(GroupName);
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Already installed", "Branching Choices V2 is already present. Its upgrade orbs are yours to reposition: move them in the Scene view. Running the installer again would overwrite your layout, so nothing was changed.", "OK");
            Selection.activeGameObject = existing;
            return;
        }
        if (!EditorUtility.DisplayDialog("Install branching upgrades?",
            "Replace the linear PLAYABLE orb layout with 21 independent/branching upgrades?\n\n" +
            "The old orbs will be DISABLED but preserved. Your Photoshop art, existing tree and editable UI will not be changed.\n\n" +
            "Within each colored path, a pair of upgrades is a PERMANENT choice: buying one locks its sibling for this playthrough.",
            "Install V2", "Cancel")) return;

        nodes.Clear();
        DefineNodes();
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(OrbFolder);
        AssetDatabase.Refresh();
        Sprite circle = EnsureCircle();
        Material material = EnsureMaterial();
        if (circle == null)
        {
            EditorUtility.DisplayDialog("Missing orb sprite", "Could not import a usable orb Sprite. No scene objects were changed.", "OK");
            return;
        }

        Dictionary<string, Node> byId = new Dictionary<string, Node>();
        foreach (Node node in nodes) byId.Add(node.id, node);
        foreach (Node node in nodes)
        {
            string path = DataFolder + "/V2_" + node.id + ".asset";
            UpgradeDefinition definition = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
                definition.displayName = node.name;
                definition.description = node.desc;
                definition.energyCost = node.energy;
                definition.nutrientCost = node.nutrients;
                definition.exclusiveGroup = node.choice;
                definition.effects = node.effects;
                definition.prerequisites = node.parent == null ? new UpgradeDefinition[0] :
                    new[] { byId[node.parent].definition };
                AssetDatabase.CreateAsset(definition, path);
            }
            node.definition = definition;
        }

        GameObject group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Create Branching Upgrade Orbs");
        Color canopy = new Color(.65f, .98f, .47f);
        Color roots = new Color(.43f, .81f, 1f);
        foreach (Node node in nodes)
        {
            GameObject go = new GameObject(node.id + " | " + node.name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + node.name);
            go.transform.SetParent(group.transform, false);
            go.transform.localPosition = new Vector3(node.pos.x, node.pos.y, 0f);
            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = .32f;
            collider.isTrigger = true;

            GameObject visuals = new GameObject("Glowing Orb - change visuals here");
            visuals.transform.SetParent(go.transform, false);
            AddDisk(visuals.transform, "Outer glow", circle, material,
                node.root ? new Color(.43f,.81f,1f,.23f) : new Color(.65f,.98f,.47f,.22f), .72f, 7);
            AddDisk(visuals.transform, "Inner orb", circle, material,
                node.root ? roots : canopy, .28f, 8);
            UpgradeOrb orb = go.AddComponent<UpgradeOrb>();
            orb.definition = node.definition;
            orb.gameState = state;
            orb.orbGraphics = visuals;
            node.orb = orb;
            BonsaiGrowthLink link = go.AddComponent<BonsaiGrowthLink>();
            link.upgrade = node.definition;
            link.isRoot = node.root;
            link.growToOrb = true;
            link.start = node.start;
            link.end = node.pos;
            link.bend = node.bend;
            link.baseThickness = node.thickness;
            link.foliageClusters = node.clusters;
        }
        // Resolve parent links once every orb exists. Nodes are in parent-before-child order.
        foreach (Node node in nodes)
            if (node.parent != null)
                node.orb.GetComponent<BonsaiGrowthLink>().parentOrb = byId[node.parent].orb;

        GameObject oldGroup = GameObject.Find("Upgrade Orbs");
        if (oldGroup != null && oldGroup != group)
        {
            Undo.RecordObject(oldGroup, "Disable old linear orb group");
            oldGroup.SetActive(false);  // preserved, not destroyed
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = group;
        EditorUtility.DisplayDialog("Branching upgrades installed",
            "Created " + nodes.Count + " upgrades: 3 canopy starters, 2 root starters, with actual splits and 10 exclusive choices.\n\n" +
            "The old linear orbs are inactive, not deleted. Move the NEW orbs in the Scene view to design the tree. Select an orb to see the green/blue growth path gizmo.\n\n" +
            "Save the scene (Ctrl+S) and press Play.", "OK");
    }

    private static void DefineNodes()
    {
        // Three separate canopy starters, all visible immediately. Each feeds a
        // two-way specialization. Each specialization grows a distinct final tip.
        // A player can grow all three initial limbs, but can only choose ONE
        // specialization on each limb in a given playthrough.
        Add("C01", "Westward Bough", "Grow the first left branch. +5 leaves. Opens a choice between lots of leaves and higher energy yield.",
            23, 11, null, null, false, new Vector2(-1.65f,2.08f), new Vector2(-.57f,1.23f), -.17f,.13f,2,
            E(UpgradeEffectType.AddLeaves,5));
        Add("C02", "Skyward Shoot", "Grow upward toward the sun. +5 leaves. Opens a choice between leafy growth and storing energy.",
            23, 11, null, null, false, new Vector2(0f,2.45f), new Vector2(.12f,1.54f), .10f,.125f,2,
            E(UpgradeEffectType.AddLeaves,5));
        Add("C03", "Eastward Bough", "Grow a branch to the right. +5 leaves. Opens a choice between a dense crown and efficient photosynthesis.",
            23, 11, null, null, false, new Vector2(1.65f,2.08f), new Vector2(.72f,1.34f), .16f,.13f,2,
            E(UpgradeEffectType.AddLeaves,5));

        Add("C04", "Leaf Factory", "WEST CHOICE: +14 actual leaves. More sunlight now and more potential leaf litter later. Selecting this locks Open Canopy.",
            47, 28, "C01", "West Choice", false, new Vector2(-2.73f,3.06f), Vector2.zero, -.21f,.11f,3,
            E(UpgradeEffectType.AddLeaves,14));
        Add("C05", "Open Canopy", "WEST CHOICE: fewer leaves, but +2.1 energy per second. Selecting this locks Leaf Factory.",
            41, 25, "C01", "West Choice", false, new Vector2(-1.45f,3.20f), Vector2.zero,.22f,.11f,1,
            E(UpgradeEffectType.AddEnergyPerSecond,2.1f));
        Add("C06", "Fine Shoots", "CENTER CHOICE: +12 leaves and a lush central canopy. Selecting this locks Sap Storage.",
            49, 30, "C02", "Center Choice", false, new Vector2(-.68f,3.35f), Vector2.zero,-.18f,.10f,3,
            E(UpgradeEffectType.AddLeaves,12));
        Add("C07", "Sap Storage", "CENTER CHOICE: +65 maximum energy, letting you save sunshine for costly upgrades. Selecting this locks Fine Shoots.",
            42, 27, "C02", "Center Choice", false, new Vector2(.61f,3.35f), Vector2.zero,.18f,.11f,1,
            E(UpgradeEffectType.AddEnergyCapacity,65));
        Add("C08", "Rich Crown", "EAST CHOICE: +14 leaves. A dense canopy for immediate photosynthesis and future autumn litter. Selecting this locks Light Catcher.",
            48, 29, "C03", "East Choice", false, new Vector2(2.72f,3.06f), Vector2.zero,.21f,.11f,3,
            E(UpgradeEffectType.AddLeaves,14));
        Add("C09", "Light Catcher", "EAST CHOICE: +2.1 energy per second from more effective sunlight use. Selecting this locks Rich Crown.",
            42, 25, "C03", "East Choice", false, new Vector2(1.47f,3.20f), Vector2.zero,-.20f,.11f,1,
            E(UpgradeEffectType.AddEnergyPerSecond,2.1f));

        // Each mutually exclusive choice has its own visually distinct third tier.
        Add("C10", "Western Leaf Shelf", "Expand your leafy western silhouette: +10 leaves.",
            72, 53, "C04", null, false, new Vector2(-3.44f,3.94f), Vector2.zero,-.12f,.09f,2,
            E(UpgradeEffectType.AddLeaves,10));
        Add("C11", "Sunward Ridge", "Improve leaf efficiency on exposed wood: +2.0 energy per second.",
            72, 49, "C05", null, false, new Vector2(-1.99f,4.12f), Vector2.zero,.18f,.09f,1,
            E(UpgradeEffectType.AddEnergyPerSecond,2f));
        Add("C12", "Central Canopy", "Fill the upper crown with +10 leaves.",
            70, 52, "C06", null, false, new Vector2(-.86f,4.22f), Vector2.zero,-.12f,.09f,3,
            E(UpgradeEffectType.AddLeaves,10));
        Add("C13", "Living Reservoir", "Grow your sapwood storage. +45 maximum energy and +0.8 energy per second.",
            68, 49, "C07", null, false, new Vector2(.54f,4.24f), Vector2.zero,.17f,.09f,1,
            E(UpgradeEffectType.AddEnergyCapacity,45), E(UpgradeEffectType.AddEnergyPerSecond,.8f));
        Add("C14", "Eastern Leaf Shelf", "Extend the eastern crown with +10 individual leaves.",
            72, 53, "C08", null, false, new Vector2(3.43f,3.94f), Vector2.zero,.12f,.09f,2,
            E(UpgradeEffectType.AddLeaves,10));
        Add("C15", "High Exposure", "Push into sunlight: +2.0 energy per second.",
            72, 49, "C09", null, false, new Vector2(2.03f,4.12f), Vector2.zero,-.15f,.09f,1,
            E(UpgradeEffectType.AddEnergyPerSecond,2f));

        // Two independent root starters. Each has a second-tier trade-off.
        Add("R01", "Fine Root Hairs", "Spread fine roots into moist upper soil: +1 nutrient per second. Opens two different nutrient strategies.",
            19, 10, null, null, true, new Vector2(-1.17f,-2.36f), new Vector2(-.56f,-1.72f), -.13f,.12f,0,
            E(UpgradeEffectType.AddNutrientsPerSecond,1f));
        Add("R02", "Deep Reserves", "Reach deeper soil: +55 nutrient capacity, +30 energy capacity. Opens a choice between absorption and storage.",
            19, 10, null, null, true, new Vector2(1.15f,-2.36f), new Vector2(.56f,-1.65f), .10f,.13f,0,
            E(UpgradeEffectType.AddNutrientCapacity,55), E(UpgradeEffectType.AddEnergyCapacity,30));
        Add("R03", "Shallow Feeders", "LEFT ROOT CHOICE: +1.5 nutrients per second, but no extra storage. Locks Wide Root Net.",
            46, 28, "R01", "Left Root Choice", true, new Vector2(-2.48f,-3.37f), Vector2.zero,-.13f,.09f,0,
            E(UpgradeEffectType.AddNutrientsPerSecond,1.5f));
        Add("R04", "Wide Root Net", "LEFT ROOT CHOICE: +0.7 nutrients per second and +70 nutrient capacity. Locks Shallow Feeders.",
            43, 27, "R01", "Left Root Choice", true, new Vector2(-.89f,-3.48f), Vector2.zero,.18f,.09f,0,
            E(UpgradeEffectType.AddNutrientsPerSecond,.7f), E(UpgradeEffectType.AddNutrientCapacity,70));
        Add("R05", "Deep Tap", "RIGHT ROOT CHOICE: +1.2 nutrients per second and +35 nutrient capacity. Locks Root Vault.",
            45, 28, "R02", "Right Root Choice", true, new Vector2(.74f,-3.47f), Vector2.zero,-.15f,.09f,0,
            E(UpgradeEffectType.AddNutrientsPerSecond,1.2f), E(UpgradeEffectType.AddNutrientCapacity,35));
        Add("R06", "Root Vault", "RIGHT ROOT CHOICE: +90 nutrient capacity and +35 energy capacity. Locks Deep Tap.",
            42, 26, "R02", "Right Root Choice", true, new Vector2(2.43f,-3.36f), Vector2.zero,.16f,.09f,0,
            E(UpgradeEffectType.AddNutrientCapacity,90), E(UpgradeEffectType.AddEnergyCapacity,35));
    }

    private static void AddDisk(Transform parent, string name, Sprite sprite, Material mat,
        Color color, float size, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(size,size,1f);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        if (mat != null) renderer.sharedMaterial = mat;
    }

    private static Sprite EnsureCircle()
    {
        Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(OrbTexturePath);
        if (circle != null) return circle;
        circle = AssetDatabase.LoadAssetAtPath<Sprite>(NewCirclePath);
        if (circle != null) return circle;
        const int size = 64;
        Texture2D tex = new Texture2D(size,size,TextureFormat.RGBA32,false);
        for (int y=0;y<size;y++) for (int x=0;x<size;x++)
        {
            float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(size*.5f,size*.5f));
            tex.SetPixel(x,y,new Color(1f,1f,1f,d<size*.47f?1f:0f));
        }
        tex.Apply();
        File.WriteAllBytes(NewCirclePath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(NewCirclePath,ImportAssetOptions.ForceUpdate);
        TextureImporter importer=AssetImporter.GetAtPath(NewCirclePath) as TextureImporter;
        if (importer!=null)
        {
            importer.textureType=TextureImporterType.Sprite;
            importer.spritePixelsPerUnit=64;
            importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(NewCirclePath);
    }
    private static Material EnsureMaterial()
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material!=null) return material;
        Shader shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader==null) shader=Shader.Find("Sprites/Default");
        if (shader==null) return null;
        material=new Material(shader) {name="TJ Branching Orbs Unlit"};
        AssetDatabase.CreateAsset(material,MaterialPath);
        return material;
    }
}
#endif
