#if UNITY_EDITOR
using System.IO;
using TreeJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Safe add-on: creates only new season and bloom objects/assets.
/// Does not move/replace TreeGameState, BonsaiGrowthLink, UpgradeOrb,
/// UpgradeDefinition or any existing MonoScript (and their .meta files).
/// </summary>
public static class TreeJamSeasonsBloomInstaller
{
    private const string MaterialFolder = "Assets/TreeJam/Materials";
    private const string OrbMaterialPath = MaterialFolder + "/TJ_Orb_HDR_Bloom.mat";
    private const string VolumeProfilePath = MaterialFolder + "/TJ_Orb_Bloom_Profile.asset";
    private const string SeasonObjectName = "SEASONS - 2 NIGHTS EACH (EDIT HERE)";
    private const string VolumeObjectName = "TREE JAM - ORB BLOOM VOLUME";

    [MenuItem("Tools/Tree Jam/Install Seasons + Orb Bloom (Current Scene)")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Stop Play Mode", "Exit Play Mode before editing the scene.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        TreeJamDayNightCycle cycle = Object.FindFirstObjectByType<TreeJamDayNightCycle>();
        TreeGameState gameState = Object.FindFirstObjectByType<TreeGameState>();
        OrganicBonsaiTree bonsai = Object.FindFirstObjectByType<OrganicBonsaiTree>();
        if (!scene.IsValid() || cycle == null || gameState == null)
        {
            EditorUtility.DisplayDialog("Missing Day/Night setup",
                "Open your playable Tree Jam scene and ensure that its DAY & NIGHT and Tree Game State components are present. No changes were made.", "OK");
            return;
        }

        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh();
        Shader orbShader = Shader.Find("TreeJam/2D Orb HDR Glow");
        if (orbShader == null)
        {
            EditorUtility.DisplayDialog("Missing shader",
                "The TreeJam/2D Orb HDR Glow shader could not be found. Check the Console for shader compilation errors. No scene changes were made.", "OK");
            return;
        }

        Material orbMat = AssetDatabase.LoadAssetAtPath<Material>(OrbMaterialPath);
        if (orbMat == null)
        {
            orbMat = new Material(orbShader) { name = "TJ Orb HDR Bloom" };
            orbMat.SetColor("_HDRColor", Color.white);
            orbMat.SetFloat("_Brightness", 2.6f);
            AssetDatabase.CreateAsset(orbMat, OrbMaterialPath);
        }

        TreeJamSeasonCycle seasons = Object.FindFirstObjectByType<TreeJamSeasonCycle>();
        if (seasons == null)
        {
            GameObject seasonObject = new GameObject(SeasonObjectName);
            Undo.RegisterCreatedObjectUndo(seasonObject, "Install Tree Jam seasons");
            seasonObject.transform.SetParent(cycle.transform, false);
            seasons = seasonObject.AddComponent<TreeJamSeasonCycle>();
            seasons.dayNight = cycle;
            seasons.resources = gameState;
            seasons.bonsai = bonsai;
            seasons.secondsPerDay = 45f;
            seasons.nightsPerSeason = 2;
            seasons.startingSeason = TreeSeason.Spring;

            if (cycle.nightOverlay != null)
            {
                GameObject tintedWorld = new GameObject("Seasonal world tint - edit sprite color on season controller");
                Undo.RegisterCreatedObjectUndo(tintedWorld, "Create Tree Jam seasonal tint");
                tintedWorld.transform.SetParent(seasonObject.transform, false);
                SpriteRenderer overlay = tintedWorld.AddComponent<SpriteRenderer>();
                overlay.sprite = cycle.nightOverlay.sprite;
                overlay.sharedMaterial = cycle.nightOverlay.sharedMaterial;
                overlay.sortingLayerID = cycle.nightOverlay.sortingLayerID;
                overlay.sortingOrder = 29999;
                overlay.color = Color.clear;
                overlay.transform.position = cycle.nightOverlay.transform.position;
                overlay.transform.localScale = cycle.nightOverlay.transform.localScale;
                seasons.seasonTintOverlay = overlay;
            }
            Canvas hud = Object.FindFirstObjectByType<TreeJamUI>()?.GetComponentInParent<Canvas>();
            if (hud == null)
            {
                Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas c in canvases) if (c.renderMode == RenderMode.ScreenSpaceOverlay) { hud = c; break; }
            }
            if (hud != null)
            {
                GameObject labelObject = new GameObject("Season label - EDIT IN CANVAS", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                Undo.RegisterCreatedObjectUndo(labelObject, "Create editable season label");
                labelObject.transform.SetParent(hud.transform, false);
                RectTransform rect = labelObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-24f, -109f);
                rect.sizeDelta = new Vector2(490f, 34f);
                Text label = labelObject.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.fontSize = 16;
                label.alignment = TextAnchor.UpperRight;
                label.color = new Color(.22f, .31f, .35f, .98f);
                label.raycastTarget = false;
                seasons.seasonLabel = label;
            }
        }

        Camera mainCam = cycle.worldCamera != null ? cycle.worldCamera : Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Enable HDR for orb bloom");
            mainCam.allowHDR = true;
            UniversalAdditionalCameraData data = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(mainCam.gameObject);
            Undo.RecordObject(data, "Enable post-processing for orb bloom");
            data.renderPostProcessing = true;
            data.volumeLayerMask |= (1 << 0); // Bloom volume on Default layer.
        }

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }
        Bloom bloom;
        if (!profile.TryGet(out bloom)) bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(.40f);
        bloom.scatter.Override(.70f);
        EditorUtility.SetDirty(profile);

        Volume volume = null;
        GameObject volumeObject = GameObject.Find(VolumeObjectName);
        if (volumeObject != null) volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
        {
            volumeObject = new GameObject(VolumeObjectName);
            Undo.RegisterCreatedObjectUndo(volumeObject, "Create orb bloom Volume");
            volume = volumeObject.AddComponent<Volume>();
        }
        volume.gameObject.layer = 0;
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        // Prefer V2 if it's installed. Otherwise fix only the active legacy orbs.
        GameObject v2Group = GameObject.Find("UPGRADE ORBS V2 - DRAG TO SHAPE TREE");
        UpgradeOrb[] orbs = v2Group != null
            ? v2Group.GetComponentsInChildren<UpgradeOrb>(true)
            : Object.FindObjectsByType<UpgradeOrb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int touched = 0;
        foreach (UpgradeOrb orb in orbs)
        {
            if (orb == null || orb.orbGraphics == null) continue;
            SpriteRenderer[] visuals = orb.orbGraphics.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in visuals)
            {
                Undo.RecordObject(renderer, "Assign HDR glow to orb");
                bool inner = renderer.gameObject.name.ToLowerInvariant().Contains("inner");
                if (inner) renderer.sharedMaterial = orbMat;
                // Night tint is sorting order 30000: orbs must be above it to shine.
                renderer.sortingOrder = inner ? 30012 : 30011;
                EditorUtility.SetDirty(renderer);
            }
            touched++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = seasons.gameObject;
        EditorUtility.DisplayDialog("Tree Jam: Seasons + Bloom",
            "Added a season controller (2 complete nights per season), set day length to 45s, and configured a Bloom volume.\n\n" +
            "Prepared " + touched + " orbs with HDR-bright inner sprites.\n\n" +
            "IMPORTANT: Ensure HDR is enabled on your active URP Asset (Project Settings > Graphics / Quality).\n\n" +
            "Existing scripts, upgrade assets, orb references, environment and UI were not replaced. Save your scene (Ctrl+S).", "OK");
    }
}
#endif
