using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TreeJam.Editor
{
    /// <summary>Rewires only existing fungal visuals/market and hides the redundant season text.</summary>
    public static class TreeJamFungalMailInstaller
    {
        [MenuItem("Tools/Tree Jam/Apply Fungal Mail + Visual Fixes (Current Scene)")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Exit Play Mode first and save your scene.", "OK");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            TreeJamFungiMarket market = Object.FindFirstObjectByType<TreeJamFungiMarket>();
            TreeJamMyceliumPolish polish = Object.FindFirstObjectByType<TreeJamMyceliumPolish>();
            if (!scene.IsValid() || market == null || polish == null)
            {
                EditorUtility.DisplayDialog("Missing fungal systems",
                    "Open your gameplay scene. The existing TreeJamFungiMarket and TreeJamMyceliumPolish components are required. No changes made.", "OK");
                return;
            }

            Undo.RecordObject(market, "Fungal mail trade balance and timing");
            market.spendEnergy = 22f;
            market.receiveNutrients = 9f;
            market.spendNutrients = 18f;
            market.receiveEnergy = 7f;
            market.secondsOutbound = 5f;
            market.secondsAtDestination = 1.5f;
            market.secondsInbound = 5f;
            market.cooldownAfterDelivery = 18f;
            market.alternateSides = true;
            market.RefreshTradeLabels();
            EditorUtility.SetDirty(market);
            if (market.closeButton != null)
            {
                Undo.RecordObject(market.closeButton.gameObject, "Hide redundant close button");
                market.closeButton.gameObject.SetActive(false);
                EditorUtility.SetDirty(market.closeButton.gameObject);
            }

            Undo.RecordObject(polish, "Smooth and layer mycelium");
            polish.mainStrandWidth = .145f;
            polish.smallStrandWidth = .038f;
            SpriteRenderer grass = FindGrassSprite();
            if (grass != null)
            {
                polish.grassSprite = grass;
                // The starter art put Soil at -7 and Grass at -6. Threads need
                // an order BETWEEN them; otherwise they disappear into the soil.
                SpriteRenderer soil = polish.surface != null && polish.surface.soilOutline != null
                    ? polish.surface.soilOutline.GetComponent<SpriteRenderer>() : null;
                if (soil != null && soil.sortingLayerID == grass.sortingLayerID &&
                    grass.sortingOrder < soil.sortingOrder + 2)
                {
                    Undo.RecordObject(grass, "Give underground strands a render layer between soil and grass");
                    grass.sortingOrder = soil.sortingOrder + 2;
                    EditorUtility.SetDirty(grass);
                }
            }
            EditorUtility.SetDirty(polish);

            TreeJamUI ui = Object.FindFirstObjectByType<TreeJamUI>();
            if (ui != null)
            {
                // Text-only change: disable the season/year Text renderer, not the
                // GameObject or resource panel. Existing resource bars remain live.
                foreach (Text label in ui.GetComponentsInChildren<Text>(true))
                {
                    if (!label.name.Equals("Subtitle", System.StringComparison.OrdinalIgnoreCase) &&
                        !label.name.Contains("Season label")) continue;
                    Undo.RecordObject(label, "Hide season/year caption");
                    label.enabled = false;
                    EditorUtility.SetDirty(label);
                    Behaviour sync = label.GetComponent("TreeJamSeasonHeaderSync") as Behaviour;
                    if (sync != null)
                    {
                        Undo.RecordObject(sync, "Disable redundant season header sync");
                        sync.enabled = false;
                        EditorUtility.SetDirty(sync);
                    }
                }
                if (ui.leavesText != null)
                {
                    Undo.RecordObject(ui.leavesText, "Hide leaf count");
                    ui.leavesText.enabled = false;
                    EditorUtility.SetDirty(ui.leavesText);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = market.gameObject;
            EditorUtility.DisplayDialog("Fungal mail installed",
                "Trade buttons now send timed resource parcels; one delivery runs at a time. " +
                "The main mycelium stays thicker than its offshoots, and fungal threads can render under the assigned grass sprite. " +
                "The redundant season/year text was hidden without deleting the UI.\n\n" +
                (grass == null ? "GRASS NOT FOUND: Assign your ground/grass SpriteRenderer to Tree Jam Mycelium Polish > Grass Sprite.\n\n" : "Grass sprite assigned: " + grass.name + "\n\n") +
                "Save your scene (Ctrl+S).", "OK");
        }

        private static SpriteRenderer FindGrassSprite()
        {
            SpriteRenderer best = null;
            int bestScore = -1;
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (sr == null || sr.sprite == null || !sr.gameObject.activeInHierarchy) continue;
                string n = sr.name.ToLowerInvariant();
                if (n.Contains("soil") || n.Contains("winter") || n.Contains("night") || n.Contains("sun")) continue;
                int score = -1;
                if (n.Contains("ground surface")) score = 100;
                else if (n.Contains("grass")) score = 90;
                else if (n.Contains("foreground")) score = 30;
                if (score > bestScore) { best = sr; bestScore = score; }
            }
            return best;
        }
    }
}
