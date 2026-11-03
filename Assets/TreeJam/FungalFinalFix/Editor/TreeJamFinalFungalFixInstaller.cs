using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    public static class TreeJamFinalFungalFixInstaller
    {
        [MenuItem("Tools/Tree Jam/Final Fungal Fix - Underground + Upgrade Growth")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Exit Play Mode before installing the fungal fix.", "OK");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            TreeJamFungalMushroomSystem system = Object.FindFirstObjectByType<TreeJamFungalMushroomSystem>();
            TreeJamMyceliumNetwork network = Object.FindFirstObjectByType<TreeJamMyceliumNetwork>();
            TreeJamSoilSurface soil = Object.FindFirstObjectByType<TreeJamSoilSurface>();
            TreeJamMyceliumPolish polish = Object.FindFirstObjectByType<TreeJamMyceliumPolish>();
            TreeJamFungalRoute route = Object.FindFirstObjectByType<TreeJamFungalRoute>();
            if (!scene.IsValid() || system == null || network == null || soil == null || soil.soilOutline == null || route == null)
            {
                EditorUtility.DisplayDialog("Scene setup incomplete",
                    "Open your gameplay scene and check the FUNGAL MUSHROOM TRADING object, fungal route, network, and Soil Surface PolygonCollider2D. No changes were made.", "OK");
                return;
            }

            GameObject root = system.gameObject;
            TreeJamFungalUndergroundGuard guard = root.GetComponent<TreeJamFungalUndergroundGuard>();
            if (guard == null) guard = Undo.AddComponent<TreeJamFungalUndergroundGuard>(root);
            TreeJamFungalUpgradeGrowth growth = root.GetComponent<TreeJamFungalUpgradeGrowth>();
            if (growth == null) growth = Undo.AddComponent<TreeJamFungalUpgradeGrowth>(root);
            Undo.RecordObject(guard, "Configure fungal underground guard");
            guard.soil = soil;
            guard.network = network;
            guard.decorativeNetwork = polish;
            guard.upgradeGrowth = growth;
            guard.surfaceClearance = 0.14f;
            guard.maxSegmentLength = 0.10f;
            EditorUtility.SetDirty(guard);

            Undo.RecordObject(growth, "Configure fungal upgrade growth");
            growth.mushroomSystem = system;
            growth.soil = soil;
            growth.network = network;
            growth.fungalRoute = route;
            growth.groundGuard = guard;
            growth.secondsToGrow = 5.5f;
            if (polish != null) growth.grassSprite = polish.grassSprite;
            EditorUtility.SetDirty(growth);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("Fungal fixes installed",
                "All non-mushroom fungal strands will be held underground. Each of the four existing fungal upgrade purchases now grows a pale branch toward its orb, with smaller side filaments.\n\nNo existing scripts or upgrade references were replaced. Save the scene (Ctrl+S), then test in Play Mode.", "OK");
        }
    }
}
