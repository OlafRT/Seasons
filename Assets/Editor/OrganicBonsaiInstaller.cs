#if UNITY_EDITOR
using System.IO;
using TreeJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds procedural bonsai growth to the current Tree Jam scene without editing the
/// TreeGameState, UpgradeOrb, upgrade assets, environment, or Canvas.
/// </summary>
public static class OrganicBonsaiInstaller
{
    [MenuItem("Tools/Tree Jam/Install Organic Bonsai (Current Scene)")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Exit Play Mode", "Stop Play Mode before installing the bonsai.", "OK");
            return;
        }
        GameObject tree = GameObject.Find("The Tree");
        if (tree == null)
        {
            EditorUtility.DisplayDialog("Tree missing", "Open your existing Tree Jam scene (containing 'The Tree') first.", "OK");
            return;
        }
        bool proceed = EditorUtility.DisplayDialog("Install Organic Bonsai?",
            "This will add a procedural tree renderer and DISABLE the old tree SpriteRenderers " +
            "to hide the square placeholders. It does not delete objects, change your environment, " +
            "change your UI, or affect upgrade costs/effects. All changes support Undo. Continue?",
            "Install", "Cancel");
        if (!proceed) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Organic Bonsai");

        OrganicBonsaiTree bonsai = tree.GetComponent<OrganicBonsaiTree>();
        if (bonsai == null) bonsai = Undo.AddComponent<OrganicBonsaiTree>(tree);

        string materialPath = "Assets/TreeJam/Art/OrganicBonsaiWood.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null)
        {
            if (!Directory.Exists("Assets/TreeJam/Art")) Directory.CreateDirectory("Assets/TreeJam/Art");
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                mat = new Material(shader) { name = "OrganicBonsaiWood" };
                AssetDatabase.CreateAsset(mat, materialPath);
            }
        }
        if (mat != null && bonsai.woodMaterial == null)
        {
            Undo.RecordObject(bonsai, "Assign bonsai material");
            bonsai.woodMaterial = mat;
        }

        // Disable *renderers*, not their parents: existing UpgradeOrb logic still
        // activates growthVisual and animates it harmlessly in the background.
        int disabled = 0;
        SpriteRenderer[] old = tree.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in old)
        {
            if (!sr.enabled) continue;
            Undo.RecordObject(sr, "Hide old tree placeholder");
            sr.enabled = false;
            disabled++;
        }

        int linked = 0;
        UpgradeOrb[] orbs = Object.FindObjectsByType<UpgradeOrb>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UpgradeOrb orb in orbs)
        {
            if (orb.gameObject.scene != tree.scene || orb.growthVisual == null) continue;
            BonsaiGrowthLink link = orb.GetComponent<BonsaiGrowthLink>();
            if (link != null) continue; // preserve user's positions on subsequent installs
            link = Undo.AddComponent<BonsaiGrowthLink>(orb.gameObject);
            Vector2 start = tree.transform.InverseTransformPoint(orb.growthVisual.transform.position);
            Vector2 orbPos = tree.transform.InverseTransformPoint(orb.transform.position);
            Vector2 direction = orbPos - start;
            Vector2 target = orbPos - direction.normalized * Mathf.Min(.25f, direction.magnitude * .23f);
            link.upgrade = orb.definition;
            link.start = start;
            link.end = target;
            link.isRoot = start.y < -1.05f || orb.name.ToLowerInvariant().Contains("root");
            link.baseThickness = link.isRoot ? .11f : .125f;
            link.bend = link.isRoot ? -.12f : .14f;
            link.foliageClusters = link.isRoot ? 0 : 2;
            EditorUtility.SetDirty(link);
            linked++;
        }
        Undo.CollapseUndoOperations(group);
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = tree;
        EditorGUIUtility.PingObject(tree);
        Debug.Log($"Tree Jam: Organic Bonsai installed. Linked {linked} upgrade orbs and hid {disabled} old SpriteRenderers. Press Play to see the growth. Save the scene.");
        EditorUtility.DisplayDialog("Organic Bonsai Installed",
            $"Linked {linked} upgrade orbs; hid {disabled} old tree renderers. " +
            "Select 'The Tree' to edit bark, foliage, growth duration and growth per year. " +
            "Press Play. Every orb has a BonsaiGrowthLink with editable start/end coordinates.", "OK");
    }
}
#endif
