#if UNITY_EDITOR
using TreeJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Non-destructively adds editable SpriteRenderer slots to the EXISTING tree.
/// Does not modify any upgrade data, orb script, environment, or Canvas.
/// </summary>
public static class TreeJamTreeArtInstaller
{
    private const string CustomArtName = "TREE ART - YOUR SPRITES";
    private const string Trunk = "01 - TRUNK (permanent)";
    private const string BaseWood = "02 - STARTING BRANCHES (wood)";
    private const string BaseLeaves = "03 - STARTING LEAVES (seasonal)";
    private const string BaseRoots = "04 - STARTING ROOTS (wood)";
    private const string GrowthArt = "CUSTOM GROWTH ART - your sprites";
    private const string BranchWood = "01 - New branch WOOD";
    private const string BranchLeaves = "02 - New branch LEAVES (seasonal)";
    private const string RootWood = "01 - New ROOT";

    [MenuItem("Tools/Tree Jam/Prepare Editable Tree Art (Current Scene)")]
    public static void Prepare()
    {
        if (!CheckEditMode()) return;
        GameObject tree = GameObject.Find("The Tree");
        if (tree == null)
        {
            EditorUtility.DisplayDialog("Tree Jam: no tree found",
                "Open your existing Tree Jam scene first (it should contain an object named 'The Tree').", "OK");
            return;
        }

        Transform art = tree.transform.Find(CustomArtName);
        if (art == null)
        {
            GameObject artObject = NewChild(CustomArtName, tree.transform);
            art = artObject.transform;
        }
        Slot(Trunk, art, 2);
        Slot(BaseWood, art, 3);
        Slot(BaseLeaves, art, 4);
        Slot(BaseRoots, art, 2);

        UpgradeOrb[] orbs = Object.FindObjectsByType<UpgradeOrb>(FindObjectsSortMode.None);
        int prepared = 0;
        foreach (UpgradeOrb orb in orbs)
        {
            if (orb == null || orb.growthVisual == null || orb.gameObject.scene != tree.scene) continue;
            Transform parent = orb.growthVisual.transform;
            Transform artGroup = parent.Find(GrowthArt);
            if (artGroup == null) artGroup = NewChild(GrowthArt, parent).transform;
            // Start these empty sprites at the branch/root attachment pivot.
            bool root = orb.transform.position.y < -1.05f ||
                        orb.growthVisual.name.ToLowerInvariant().Contains("root");
            if (root)
            {
                Slot(RootWood, artGroup, 3);
            }
            else
            {
                Slot(BranchWood, artGroup, 3);
                Slot(BranchLeaves, artGroup, 4);
            }
            prepared++;
        }

        Selection.activeGameObject = art.gameObject;
        EditorGUIUtility.PingObject(art.gameObject);
        MarkDirty();
        Debug.Log("Tree Jam: Created editable tree sprite slots and prepared " + prepared +
                  " upgrade growth groups. Nothing was deleted or hidden. " +
                  "Assign sprites, then use Tools > Tree Jam > Hide Replaced Tree Placeholders.");
        EditorUtility.DisplayDialog("Editable Tree Art Ready",
            "Your tree and upgrade visuals now have empty SpriteRenderer slots. " +
            "Assign your PNGs to the slots in the Hierarchy. Once they look right, use " +
            "'Hide Replaced Tree Placeholders' to hide only the matching old shapes. " +
            "Your existing orbs, UI, and environment are unchanged.", "OK");
    }

    [MenuItem("Tools/Tree Jam/Hide Replaced Tree Placeholders (Current Scene)")]
    public static void HideReplaced()
    {
        if (!CheckEditMode()) return;
        GameObject tree = GameObject.Find("The Tree");
        if (tree == null) return;
        Transform art = tree.transform.Find(CustomArtName);
        if (art == null)
        {
            EditorUtility.DisplayDialog("No custom tree slots", "First use 'Prepare Editable Tree Art'.", "OK");
            return;
        }

        int changed = 0;
        if (HasSprite(art, Trunk)) changed += HideChild(tree.transform, "Trunk");
        if (HasSprite(art, BaseWood))
        {
            changed += HideChild(tree.transform, "Small Left Branch");
            changed += HideChild(tree.transform, "Small Right Branch");
        }
        if (HasSprite(art, BaseLeaves))
        {
            foreach (Transform child in tree.transform)
                if (child.name == "Leaf") changed += HideObject(child.gameObject);
        }
        if (HasSprite(art, BaseRoots))
        {
            changed += HideChild(tree.transform, "Root Left");
            changed += HideChild(tree.transform, "Root Right");
        }

        UpgradeOrb[] orbs = Object.FindObjectsByType<UpgradeOrb>(FindObjectsSortMode.None);
        foreach (UpgradeOrb orb in orbs)
        {
            if (orb == null || orb.growthVisual == null || orb.gameObject.scene != tree.scene) continue;
            Transform growth = orb.growthVisual.transform;
            Transform artGroup = growth.Find(GrowthArt);
            if (artGroup == null) continue;
            bool hasWood = HasSprite(artGroup, BranchWood) || HasSprite(artGroup, RootWood);
            bool hasLeaves = HasSprite(artGroup, BranchLeaves);
            foreach (Transform child in growth)
            {
                if (child == artGroup) continue;
                if (hasWood && (child.name.StartsWith("New Branch") ||
                                child.name.StartsWith("New Root") || child.name == "Rootlet"))
                    changed += HideObject(child.gameObject);
                if (hasLeaves && child.name == "Leaf") changed += HideObject(child.gameObject);
            }
        }
        MarkDirty();
        Debug.Log("Tree Jam: Hidden " + changed + " original placeholder objects. " +
                  "All objects are still there (inactive) and can be restored using the Hierarchy checkbox or Undo.");
        EditorUtility.DisplayDialog("Placeholders Updated",
            "Hidden " + changed + " old placeholder objects where your replacement sprites are assigned. " +
            "Nothing was deleted. Press Ctrl+S to save. Undo works too.", "OK");
    }

    private static bool CheckEditMode()
    {
        if (!Application.isPlaying) return true;
        EditorUtility.DisplayDialog("Exit Play Mode", "Stop Play Mode before editing tree art.", "OK");
        return false;
    }

    private static GameObject NewChild(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Prepare Tree Jam sprite art");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static SpriteRenderer Slot(string name, Transform parent, int order)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            SpriteRenderer existingRenderer = existing.GetComponent<SpriteRenderer>();
            if (existingRenderer != null) return existingRenderer;
            return Undo.AddComponent<SpriteRenderer>(existing.gameObject);
        }
        GameObject go = NewChild(name, parent);
        SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(go);
        renderer.sortingOrder = order;
        renderer.color = Color.white;
        // Keep all slot positions and rotations editable. No sprite or scale is forced.
        return renderer;
    }

    private static bool HasSprite(Transform parent, string slotName)
    {
        Transform slot = parent.Find(slotName);
        SpriteRenderer sr = slot != null ? slot.GetComponent<SpriteRenderer>() : null;
        return sr != null && sr.sprite != null;
    }

    private static int HideChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? HideObject(child.gameObject) : 0;
    }

    private static int HideObject(GameObject obj)
    {
        if (!obj.activeSelf) return 0;
        Undo.RecordObject(obj, "Hide replaced placeholder tree sprite");
        obj.SetActive(false);
        return 1;
    }

    private static void MarkDirty()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
