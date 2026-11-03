#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    /// <summary>
    /// Repairs serialized values on BonsaiGrowthLink after a script was removed
    /// and reattached. This is a REPAIR ONLY: no assets, orbs or components are
    /// created/deleted, and all orb GameObject positions are preserved.
    /// </summary>
    public static class RepairBonsaiGrowthLinks
    {
        private const string V2Group = "UPGRADE ORBS V2 - DRAG TO SHAPE TREE";

        private struct Shape
        {
            public Vector2 start, end;
            public float bend, thickness;
            public int clusters;
            public bool root;
            public string parentId;
            public Shape(bool root, Vector2 start, Vector2 end, float bend, float thickness, int clusters, string parentId = null)
            {
                this.root = root;
                this.start = start;
                this.end = end;
                this.bend = bend;
                this.thickness = thickness;
                this.clusters = clusters;
                this.parentId = parentId;
            }
        }

        // Legacy 12-orb configuration, reconstructed from the original scene
        // generator and SparseCanopyChoicesInstaller. These had fixed endpoints.
        private static readonly Dictionary<string, Shape> legacy = new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase)
        {
            { "Young Branch", S(false, -.54f, 1.18f, -1.40f, 2.02f, .14f, .125f, 2) },
            { "Broad Leaves", S(false, -1.40f, 2.02f, -2.48f, 2.85f, .14f, .125f, 2) },
            { "Fine Root Hairs", S(true, -.50f, -1.62f, -1.42f, -2.42f, -.12f, .11f, 0) },
            { "Deep Reserves", S(true, -1.42f, -2.42f, -2.51f, -3.23f, -.12f, .11f, 0) },
            { "Leaf Buds", S(false, -1.32f, 1.96f, -2.14f, 2.82f, -.15f, .115f, 2) },
            { "Sunward Twigs", S(false, -1.14f, 1.84f, -.35f, 2.55f, .24f, .115f, 2) },
            { "Dense Crown", S(false, -2.10f, 2.78f, -3.14f, 3.55f, -.20f, .12f, 3) },
            { "Low-Light Foliage", S(false, -.37f, 2.52f, .79f, 3.31f, .18f, .115f, 2) },
            { "Summer Reserves", S(false, -.33f, 2.52f, -.73f, 3.52f, -.13f, .11f, 1) },
            { "Crown Harmony", S(false, -3.08f, 3.51f, -2.26f, 4.11f, -.16f, .10f, 2) },
            { "Feeder Roots", S(true, -1.36f, -2.40f, -3.22f, -3.53f, -.15f, .105f, 0) },
            { "Root Reservoir", S(true, -2.42f, -3.20f, -1.75f, -4.14f, .12f, .115f, 0) }
        };

        // Orb-guided V2 graph, reconstructed from BranchingChoicesV2Installer.
        // The END field isn't used when growToOrb is true; the live orb position
        // controls the destination, so repositioned orbs are NOT moved.
        private static readonly Dictionary<string, Shape> v2 = new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase)
        {
            { "C01", S(false, -.57f, 1.23f, -1.65f, 2.08f, -.17f, .13f, 2) },
            { "C02", S(false, .12f, 1.54f, 0f, 2.45f, .10f, .125f, 2) },
            { "C03", S(false, .72f, 1.34f, 1.65f, 2.08f, .16f, .13f, 2) },
            { "C04", S(false, 0, 0, -2.73f, 3.06f, -.21f, .11f, 3, "C01") },
            { "C05", S(false, 0, 0, -1.45f, 3.20f, .22f, .11f, 1, "C01") },
            { "C06", S(false, 0, 0, -.68f, 3.35f, -.18f, .10f, 3, "C02") },
            { "C07", S(false, 0, 0, .61f, 3.35f, .18f, .11f, 1, "C02") },
            { "C08", S(false, 0, 0, 2.72f, 3.06f, .21f, .11f, 3, "C03") },
            { "C09", S(false, 0, 0, 1.47f, 3.20f, -.20f, .11f, 1, "C03") },
            { "C10", S(false, 0, 0, -3.44f, 3.94f, -.12f, .09f, 2, "C04") },
            { "C11", S(false, 0, 0, -1.99f, 4.12f, .18f, .09f, 1, "C05") },
            { "C12", S(false, 0, 0, -.86f, 4.22f, -.12f, .09f, 3, "C06") },
            { "C13", S(false, 0, 0, .54f, 4.24f, .17f, .09f, 1, "C07") },
            { "C14", S(false, 0, 0, 3.43f, 3.94f, .12f, .09f, 2, "C08") },
            { "C15", S(false, 0, 0, 2.03f, 4.12f, -.15f, .09f, 1, "C09") },
            { "R01", S(true, -.56f, -1.72f, -1.17f, -2.36f, -.13f, .12f, 0) },
            { "R02", S(true, .56f, -1.65f, 1.15f, -2.36f, .10f, .13f, 0) },
            { "R03", S(true, 0, 0, -2.48f, -3.37f, -.13f, .09f, 0, "R01") },
            { "R04", S(true, 0, 0, -.89f, -3.48f, .18f, .09f, 0, "R01") },
            { "R05", S(true, 0, 0, .74f, -3.47f, -.15f, .09f, 0, "R02") },
            { "R06", S(true, 0, 0, 2.43f, -3.36f, .16f, .09f, 0, "R02") }
        };

        private static Shape S(bool root, float sx, float sy, float ex, float ey, float bend, float thickness, int clusters, string parentId = null)
            => new Shape(root, new Vector2(sx, sy), new Vector2(ex, ey), bend, thickness, clusters, parentId);

        [MenuItem("Tools/Tree Jam/Repair Bonsai Orb References (Current Scene)")]
        public static void Repair()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Tree Jam", "Stop Play Mode before repairing references.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            UpgradeOrb[] all = UnityEngine.Object.FindObjectsByType<UpgradeOrb>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<UpgradeOrb> orbs = new List<UpgradeOrb>();
            Dictionary<string, UpgradeOrb> v2Orbs = new Dictionary<string, UpgradeOrb>(StringComparer.OrdinalIgnoreCase);
            foreach (UpgradeOrb orb in all)
            {
                if (orb.gameObject.scene != scene) continue;
                orbs.Add(orb);
                if (IsInsideV2(orb.transform) && TryV2Id(orb.name, out string id))
                    v2Orbs[id] = orb;
            }

            if (orbs.Count == 0)
            {
                EditorUtility.DisplayDialog("Tree Jam", "No UpgradeOrb objects were found in the open scene.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Repair existing bonsai orb links?",
                "This ONLY restores missing BonsaiGrowthLink values and references on existing orbs, including inactive legacy orbs. " +
                "It will not add/delete objects, move orbs, change ScriptableObject upgrades, or rebuild the scene. " +
                "Existing custom geometry settings are preserved unless the component appears to have been reset.",
                "Repair Links", "Cancel")) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Repair BonsaiGrowthLink references");
            int repaired = 0, missingLink = 0, unknown = 0, noDefinition = 0;
            foreach (UpgradeOrb orb in orbs)
            {
                BonsaiGrowthLink link = orb.GetComponent<BonsaiGrowthLink>();
                if (link == null) { missingLink++; continue; }
                if (orb.definition == null) { noDefinition++; continue; }

                string nodeId = null;
                bool isV2 = IsInsideV2(orb.transform) && TryV2Id(orb.name, out nodeId);
                Shape shape;
                if (isV2)
                {
                    if (!v2.TryGetValue(nodeId, out shape)) { unknown++; continue; }
                }
                else
                {
                    if (!legacy.TryGetValue(orb.definition.displayName, out shape)) { unknown++; continue; }
                }

                // Reset detection: a recreated component has a null upgrade and
                // default start/end/parent. Those defaults are what the user
                // reported after the deleted script was restored.
                bool completelyReset = link.upgrade == null && link.start == Vector2.zero &&
                                       link.end == Vector2.zero && link.parentOrb == null;
                bool changed = false;
                Undo.RecordObject(link, "Reconnect " + orb.name);
                if (link.upgrade != orb.definition)
                {
                    link.upgrade = orb.definition;
                    changed = true;
                }

                if (isV2)
                {
                    if (!link.growToOrb) { link.growToOrb = true; changed = true; }
                    if (shape.parentId != null)
                    {
                        if (v2Orbs.TryGetValue(shape.parentId, out UpgradeOrb parent))
                        {
                            if (link.parentOrb == null) { link.parentOrb = parent; changed = true; }
                        }
                        else Debug.LogWarning("Tree Jam repair: parent " + shape.parentId + " not found for " + orb.name, orb);
                    }
                    // The original V2 starter starts are nonzero; child starts
                    // intentionally remain zero because their Parent Orb defines them.
                    if (link.start == Vector2.zero && shape.parentId == null)
                    {
                        link.start = shape.start;
                        changed = true;
                    }
                    if (link.end == Vector2.zero)
                    {
                        // For V2, the end field is informational/fallback only;
                        // actual growth uses orb.transform.position.
                        link.end = shape.end;
                        changed = true;
                    }
                }
                else
                {
                    // The original 12 use their stored local endpoints.
                    // Keep any custom start/end edits if already populated.
                    if (link.start == Vector2.zero) { link.start = shape.start; changed = true; }
                    if (link.end == Vector2.zero) { link.end = shape.end; changed = true; }
                }

                if (shape.root && !link.isRoot) { link.isRoot = true; changed = true; }
                if (completelyReset)
                {
                    if (!Mathf.Approximately(link.bend, shape.bend)) { link.bend = shape.bend; changed = true; }
                    if (!Mathf.Approximately(link.baseThickness, shape.thickness)) { link.baseThickness = shape.thickness; changed = true; }
                    if (link.foliageClusters != shape.clusters) { link.foliageClusters = shape.clusters; changed = true; }
                }
                else if (shape.root && link.foliageClusters != 0)
                {
                    // A restored root link may retain the script's default
                    // foliageClusters=2; roots should never generate leaves.
                    link.foliageClusters = 0;
                    changed = true;
                }
                if (changed) { EditorUtility.SetDirty(link); repaired++; }
            }

            Undo.CollapseUndoOperations(undoGroup);
            if (repaired > 0) EditorSceneManager.MarkSceneDirty(scene);
            string report = "Repaired " + repaired + " bonsai orb link(s). " +
                            "Missing BonsaiGrowthLink component: " + missingLink + ". " +
                            "No UpgradeOrb definition: " + noDefinition + ". " +
                            "Unrecognized orb: " + unknown + ".";
            Debug.Log("Tree Jam: " + report);
            EditorUtility.DisplayDialog("Bonsai orb repair finished", report +
                "\n\nYour orb positions and upgrade assets were not changed. Inspect a canopy and root orb, save the scene (Ctrl+S), then test growth.", "OK");
        }

        private static bool TryV2Id(string name, out string id)
        {
            id = null;
            int pipe = name.IndexOf('|');
            if (pipe < 0) return false;
            string candidate = name.Substring(0, pipe).Trim();
            if (!v2.ContainsKey(candidate)) return false;
            id = candidate;
            return true;
        }

        private static bool IsInsideV2(Transform current)
        {
            for (Transform t = current; t != null; t = t.parent)
                if (t.name == V2Group) return true;
            return false;
        }
    }
}
#endif
