#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TreeJam.Editor
{
    /// <summary>
    /// Additive, idempotent scene installer. Does not write/replace/move any .cs or
    /// .meta file. Undo is supported. Custom art, orb positions, and UI styling stay.
    /// </summary>
    public static class TreeJamEcosystemCleanupInstaller
    {
        private const string RootName = "ECOSYSTEM CLEANUP - SURFACE, LEAVES, WINTER (EDIT HERE)";

        [MenuItem("Tools/Tree Jam/Apply Ecosystem Cleanup (Current Scene)")]
        private static void Apply()
        {
            if (EditorApplication.isPlaying)
            { EditorUtility.DisplayDialog("Stop Play Mode", "Exit Play Mode, save a scene backup, then run the installer.", "OK"); return; }
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            TreeGameState state = UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
            OrganicBonsaiTree tree = UnityEngine.Object.FindFirstObjectByType<OrganicBonsaiTree>();
            TreeJamSeasonCycle seasons = UnityEngine.Object.FindFirstObjectByType<TreeJamSeasonCycle>();
            TreeJamUI ui = UnityEngine.Object.FindFirstObjectByType<TreeJamUI>();
            if (state == null || tree == null || seasons == null)
            {
                EditorUtility.DisplayDialog("Missing Tree Jam systems",
                    "Open your playable scene with TreeGameState, OrganicBonsaiTree and TreeJamSeasonCycle. Nothing was changed.", "OK");
                return;
            }
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create ecosystem cleanup root");
            }

            TreeJamSoilSurface surface = GetOrAdd<TreeJamSoilSurface>(root);
            SpriteRenderer soilArt = FindSoilSprite();
            if (soilArt != null)
            {
                PolygonCollider2D collider = soilArt.GetComponent<PolygonCollider2D>();
                if (collider == null) collider = Undo.AddComponent<PolygonCollider2D>(soilArt.gameObject);
                Undo.RecordObject(collider, "Configure soil outline");
                collider.isTrigger = true; // used only for sampling, not physical collision
                Undo.RecordObject(surface, "Assign soil outline");
                surface.soilOutline = collider;
                EditorUtility.SetDirty(collider);
                EditorUtility.SetDirty(surface);
            }

            var snow = GetOrAdd<TreeJamSnowOnSoil>(Child(root.transform,"SNOW - follows painted soil"));
            Undo.RecordObject(snow,"Wire grounded snow");
            snow.ground = surface;
            snow.seasons = seasons;
            snow.sceneCamera = Camera.main;
            TreeJamNordicSnow oldSnow = UnityEngine.Object.FindFirstObjectByType<TreeJamNordicSnow>();
            if (oldSnow != null && oldSnow.enabled)
            {
                Undo.RecordObject(oldSnow, "Disable old flat snow renderer");
                snow.maximumThickness = oldSnow.maximumSnowThickness;
                snow.snowflakesPerSecond = oldSnow.flakesPerSecond;
                snow.snowColor = oldSnow.snowColor;
                oldSnow.enabled = false;  // keep component for reversible recovery
                EditorUtility.SetDirty(oldSnow);
            }
            EditorUtility.SetDirty(snow);

            var leaf = GetOrAdd<TreeJamLeafGroundAndSeasons>(Child(root.transform,"LEAVES - soil landing + active photosynthesis"));
            Undo.RecordObject(leaf,"Wire leaf landing");
            leaf.ground = surface;
            leaf.seasons = seasons;
            leaf.bonsai = tree;
            leaf.resources = state;
            leaf.fungiMarket = UnityEngine.Object.FindFirstObjectByType<TreeJamFungiMarket>();
            EditorUtility.SetDirty(leaf);

            var mountains = GetOrAdd<TreeJamWinterMountainOverlay>(Child(root.transform,"WINTER MOUNTAINS - opacity only"));
            Undo.RecordObject(mountains,"Wire winter mountain overlay");
            mountains.seasons = seasons;
            if (mountains.winterMountains == null) mountains.winterMountains = FindWinterMountain();
            if (mountains.winterMountains != null)
            {
                SpriteRenderer winter = mountains.winterMountains;
                Undo.RecordObject(winter, "Hide snowy mountains until winter");
                Color c = winter.color;
                c.a = 0f;
                winter.color = c;
                EditorUtility.SetDirty(winter);
            }
            EditorUtility.SetDirty(mountains);

            TreeJamMyceliumNetwork existingNetwork = UnityEngine.Object.FindFirstObjectByType<TreeJamMyceliumNetwork>();
            if (existingNetwork != null)
            {
                var fungi = GetOrAdd<TreeJamMyceliumPolish>(Child(root.transform,"MYCELIUM - density, surface fruits, extended edges"));
                Undo.RecordObject(fungi, "Wire mycelium polish");
                fungi.network = existingNetwork;
                fungi.surface = surface;
                fungi.seasons = seasons;
                // The original main roots start within the camera, making a visible
                // hard cutoff after zoom-out. Start the original geometry farther away
                // as well, *before* its runtime path is generated.
                if (existingNetwork.worldCamera != null && surface.soilOutline != null)
                {
                    Undo.RecordObject(existingNetwork, "Extend mycelium start beyond soil");
                    float centerX = existingNetwork.treeRoot != null ? existingNetwork.treeRoot.position.x : 0f;
                    Bounds soil = surface.WorldBounds;
                    float reach = Mathf.Max(Mathf.Abs(centerX - soil.min.x), Mathf.Abs(soil.max.x - centerX));
                    existingNetwork.spawnOffsetFromTree = Mathf.Max(existingNetwork.spawnOffsetFromTree, reach + 1f);
                    existingNetwork.mainThreadWidth = Mathf.Max(existingNetwork.mainThreadWidth, .075f);
                    EditorUtility.SetDirty(existingNetwork);
                }
                EditorUtility.SetDirty(fungi);
            }

            var words = GetOrAdd<TreeJamResourceWording>(Child(root.transform,"TEXT - energy terminology"));
            Undo.RecordObject(words, "Wire terminology");
            words.market = UnityEngine.Object.FindFirstObjectByType<TreeJamFungiMarket>();
            words.ui = ui;
            EditorUtility.SetDirty(words);
            SimplifyUI(ui, seasons);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            string warnings = soilArt == null ? "\n\nIMPORTANT: Soil sprite was not found. Assign a PolygonCollider2D on your soil artwork to Ecosystem Cleanup > Tree Jam Soil Surface." :
                surface.soilOutline == null || surface.soilOutline.pathCount == 0 ? "\n\nIMPORTANT: Soil collider has no outline. Generate a Sprite Custom Physics Shape in Sprite Editor." : "";
            if (mountains.winterMountains == null) warnings += "\n\nDrag your snowy mountain SpriteRenderer into WINTER MOUNTAINS > Winter Mountains.";
            EditorUtility.DisplayDialog("Ecosystem cleanup installed",
                "Added ground-following leaf landings, winter snow, winter-mountain opacity, mycelium refinements and UI cleanup.\n"+
                "Preserved your existing scripts, orb positions and custom artwork.\n"+
                "Old snow is disabled, not deleted. Run this installer again safely if needed."+
                warnings + "\n\nSave scene with Ctrl+S.", "OK");
        }

        private static T GetOrAdd<T>(GameObject go) where T:Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }
        private static GameObject Child(Transform parent,string name)
        {
            Transform found = parent.Find(name);
            if (found != null) return found.gameObject;
            GameObject go=new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go,"Create "+name);
            go.transform.SetParent(parent,false);
            return go;
        }

        private static SpriteRenderer FindSoilSprite()
        {
            SpriteRenderer best=null;
            int highest=-10000;
            foreach (SpriteRenderer sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if (sr.sprite == null) continue;
                string name = sr.name.ToLowerInvariant();
                string parent = sr.transform.parent != null ? sr.transform.parent.name.ToLowerInvariant() : "";
                if (!name.Contains("soil") && !name.Contains("ground")) continue;
                if (name.Contains("grass") || name.Contains("sun") || name.Contains("snow")) continue;
                int score=0;
                if(sr.gameObject.activeInHierarchy)score+=60;
                if(name.Contains("soil"))score+=30;
                if(name.Contains("texture"))score+=15;
                if(name.Contains("03"))score+=20;
                if(parent.Contains("art") || parent.Contains("sprite"))score+=15;
                if(score>highest){highest=score;best=sr;}
            }
            return best;
        }

        private static SpriteRenderer FindWinterMountain()
        {
            SpriteRenderer best=null;
            int highest=-1;
            foreach(SpriteRenderer sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(sr.sprite==null)continue;
                string n=(sr.name+" "+(sr.sprite!=null?sr.sprite.name:"")).ToLowerInvariant();
                string p=sr.transform.parent != null?sr.transform.parent.name.ToLowerInvariant():"";
                if (!(n.Contains("mountain") || p.Contains("mountain") || n.Contains("landscape"))) continue;
                if (!(n.Contains("winter")||n.Contains("snow")||p.Contains("winter")||p.Contains("snow")))continue;
                int score=(sr.gameObject.activeInHierarchy?15:0)+(n.Contains("winter")?20:0)+(n.Contains("mountain")?10:0);
                if(score>highest){highest=score;best=sr;}
            }
            return best;
        }

        private static void SimplifyUI(TreeJamUI ui,TreeJamSeasonCycle seasons)
        {
            if (ui != null)
            {
                // The TreeJamUI component is responsible for resource bars and
                // tooltips. It doesn't own the season. Attach a separate text
                // adapter to the existing editable Subtitle instead.
                if (ui.leavesText != null) HideObject(ui.leavesText.gameObject);
                Text subtitle = null;
                Text fallback = null;
                foreach (Text t in ui.GetComponentsInChildren<Text>(true))
                {
                    if (t == null) continue;
                    string n = t.gameObject.name.ToLowerInvariant();
                    string content = (t.text ?? string.Empty).ToLowerInvariant();
                    if (n.Contains("energy caption") ||
                        (content.Trim() == "sunlight" && !n.Contains("cost")))
                    {
                        Undo.RecordObject(t, "Rename Sunlight to Energy");
                        t.text = "ENERGY";
                        EditorUtility.SetDirty(t);
                    }
                    if (n == "subtitle" || n.Contains("season/year - bottom left"))
                        subtitle = t;
                    else if (fallback == null && content.Contains("year") &&
                             !content.Contains("night") && !n.Contains("clock"))
                        fallback = t;
                    if (n.Contains("header") && content.Contains("a living tree"))
                        HideObject(t.gameObject);
                }
                if (subtitle == null) subtitle = fallback;
                if (subtitle != null)
                {
                    var sync = subtitle.GetComponent<TreeJamSeasonHeaderSync>();
                    if (sync == null) sync = Undo.AddComponent<TreeJamSeasonHeaderSync>(subtitle.gameObject);
                    Undo.RecordObject(sync, "Bind lower-left season label");
                    sync.seasons = seasons;
                    sync.label = subtitle;
                    EditorUtility.SetDirty(sync);
                    // Correct the static 'SUMMER / YEAR ONE' editor preview too.
                    Undo.RecordObject(subtitle, "Set season header preview");
                    subtitle.text = seasons.startingSeason.ToString().ToUpperInvariant() + " / YEAR ONE";
                    EditorUtility.SetDirty(subtitle);
                }
                else
                {
                    Debug.LogWarning("Tree Jam cleanup: Couldn't find the editable bottom-left Subtitle. " +
                        "Add TreeJamSeasonHeaderSync to that UI Text and assign the Season Controller.", ui);
                }
            }
            // Hide the duplicate season label at the top right, but never hide
            // the lower-left subtitle (in case it was reused in a custom scene).
            if (seasons.seasonLabel != null)
            {
                Text topRight = seasons.seasonLabel;
                TreeJamSeasonHeaderSync sync = topRight.GetComponent<TreeJamSeasonHeaderSync>();
                if (sync == null || sync.label != topRight)
                {
                    Undo.RecordObject(topRight, "Hide duplicate season text");
                    topRight.enabled = false;
                    EditorUtility.SetDirty(topRight);
                }
            }
            foreach (TreeJamDayNightClockUI clock in UnityEngine.Object.FindObjectsByType<TreeJamDayNightClockUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Undo.RecordObject(clock,"Hide clock text");
                clock.enabled = false;
                EditorUtility.SetDirty(clock);
                if (clock.label != null)
                {
                    Undo.RecordObject(clock.label,"Hide clock label");
                    clock.label.enabled = false;
                    EditorUtility.SetDirty(clock.label);
                }
            }
            TreeJamFungiMarket market = UnityEngine.Object.FindFirstObjectByType<TreeJamFungiMarket>();
            if (market != null && market.panel != null)
            {
                foreach (Text t in market.panel.GetComponentsInChildren<Text>(true))
                {
                    if (string.IsNullOrEmpty(t.text) || !t.text.ToLowerInvariant().Contains("sunlight")) continue;
                    Undo.RecordObject(t,"Rename market Sunlight to Energy");
                    t.text = t.text.Replace("SUNLIGHT","ENERGY").Replace("Sunlight","Energy").Replace("sunlight","energy");
                    EditorUtility.SetDirty(t);
                }
            }
        }
        private static void HideObject(GameObject go)
        {
            if(go!=null && go.activeSelf)
            {
                Undo.RecordObject(go,"Hide redundant UI element");
                go.SetActive(false);
                EditorUtility.SetDirty(go);
            }
        }
    }
}
#endif
