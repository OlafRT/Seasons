#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    public static class TreeJamLumberjackEndingInstaller
    {
        private const string ArtPath = "Assets/TreeJam/LumberjackEnding/Art/";
        private const string MaterialPath = "Assets/TreeJam/LumberjackEnding/LumberjackUnlit.mat";
        private const string RootName = "LUMBERJACK ENDING - EDIT HERE";

        [MenuItem("Tools/Tree Jam/Install Lumberjack Ending + Rebirth (Current Scene)")]
        public static void Install()
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("Stop Play Mode", "Stop Play Mode first.", "OK"); return; }
            Scene scene = SceneManager.GetActiveScene();
            TreeGameState resources = UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
            OrganicBonsaiTree bonsai = UnityEngine.Object.FindFirstObjectByType<OrganicBonsaiTree>();
            if (resources == null || bonsai == null || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Scene not ready", "Open and SAVE your existing gameplay scene. It must contain TreeGameState and OrganicBonsaiTree. Nothing was changed.", "OK");
                return;
            }
            GameObject previous = GameObject.Find(RootName);
            if (previous != null)
            {
                Selection.activeGameObject = previous;
                EditorUtility.DisplayDialog("Already installed", "The existing ending is selected. This installer does not overwrite your adjusted puppet or settings.", "OK");
                return;
            }
            string[] required = { "Torso", "Head", "UpperArm", "LowerArm", "Hand", "Axe", "UpperLeg", "LowerLeg", "Boot" };
            foreach (string part in required)
            {
                string path = ArtPath + "LumberJack_" + part + ".png";
                if (!File.Exists(path))
                {
                    EditorUtility.DisplayDialog("Missing art", "Could not find " + path + ". Reimport the whole package. Nothing changed.", "OK");
                    return;
                }
            }
            if (!EditorUtility.DisplayDialog("Install lumberjack endgame?",
                "Add an editable nine-image cutout puppet, completion detector, animated felling, blackout, and repeating rebirth. Existing tree, UI, mushrooms and upgrade assets remain unchanged.\n\nThe scene will be included in Build Profiles so it can reload after the fade.",
                "Install", "Cancel")) return;

            Material artMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (artMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                artMat = new Material(shader) { name = "Lumberjack Unlit" };
                AssetDatabase.CreateAsset(artMat,MaterialPath);
            }
            GameObject ending = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(ending,"Create Lumberjack Ending");
            TreeJamEndgameLoop director = Undo.AddComponent<TreeJamEndgameLoop>(ending);
            director.resources = resources;
            director.tree = bonsai;
            director.soil = UnityEngine.Object.FindFirstObjectByType<TreeJamSoilSurface>();
            director.worldCamera = Camera.main;
            director.firstTrunkThickness = bonsai.trunkThickness;
            GameObject actor = new GameObject("LUMBERJACK PUPPET - move joint pivots here");
            actor.transform.SetParent(ending.transform, false);
            actor.transform.localScale = Vector3.one * .68f;
            TreeJamLumberjackPuppet puppet = actor.AddComponent<TreeJamLumberjackPuppet>();
            director.lumberjack = puppet;
            BuildRig(actor.transform,puppet,artMat);
            actor.SetActive(false);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(puppet);
            AddSceneToBuild(scene.path);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = actor;
            Debug.Log("Tree Jam: Lumberjack rig and ending installed. Select the parent to adjust timings. Use the ending component's TEST menu in Play Mode.");
            EditorUtility.DisplayDialog("Endgame ready to test",
                "Lumberjack puppet created with editable joints.\n\nIn Play Mode select 'LUMBERJACK ENDING - EDIT HERE' and use 'TEST - Trigger lumberjack ending'. After the blackout the same scene reloads with a thicker trunk.\n\nSaving the scene is required.", "OK");
        }

        private static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == path))
                scenes.Add(new EditorBuildSettingsScene(path,true));
            else
                for (int i=0;i<scenes.Count;i++)
                    if (scenes[i].path == path && !scenes[i].enabled)
                        scenes[i] = new EditorBuildSettingsScene(path,true);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Transform Joint(Transform parent, string name, Vector2 at)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition = new Vector3(at.x,at.y,0f);
            return go.transform;
        }

        private static void SpritePart(Transform parent, string name, string png, Vector2 offset,
            Vector2 scale, Material material, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition = new Vector3(offset.x,offset.y,0f);
            go.transform.localScale = new Vector3(scale.x,scale.y,1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(png);
            sr.sharedMaterial = material;
            sr.sortingOrder = order;
        }

        private static Sprite LoadSprite(string part)
        {
            string path = ArtPath + "LumberJack_" + part + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 512f))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 512f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BuildRig(Transform root, TreeJamLumberjackPuppet rig, Material mat)
        {
            rig.torso = Joint(root,"Body pivot",new Vector2(0f,1.47f));
            SpritePart(rig.torso,"Torso", "Torso",Vector2.zero,new Vector2(.95f,1.03f),mat,22);
            rig.head = Joint(rig.torso,"Neck / head pivot",new Vector2(0f,.78f));
            SpritePart(rig.head,"Head","Head",new Vector2(0f,.18f),new Vector2(.86f,.85f),mat,26);

            rig.leftShoulder = Joint(rig.torso,"Left shoulder",new Vector2(-.31f,.46f));
            SpritePart(rig.leftShoulder,"Left upper arm","UpperArm",new Vector2(-.01f,-.26f),new Vector2(.66f,.72f),mat,20);
            rig.leftElbow = Joint(rig.leftShoulder,"Left elbow",new Vector2(-.04f,-.58f));
            SpritePart(rig.leftElbow,"Left lower arm","LowerArm",new Vector2(0f,-.23f),new Vector2(.64f,.62f),mat,20);
            Transform lh = Joint(rig.leftElbow,"Left wrist",new Vector2(0f,-.47f));
            SpritePart(lh,"Left hand","Hand",new Vector2(0f,-.04f),new Vector2(.48f,.48f),mat,21);

            rig.rightShoulder = Joint(rig.torso,"Right shoulder",new Vector2(.31f,.47f));
            SpritePart(rig.rightShoulder,"Right upper arm","UpperArm",new Vector2(0f,-.26f),new Vector2(-.66f,.72f),mat,25);
            rig.rightElbow = Joint(rig.rightShoulder,"Right elbow",new Vector2(.04f,-.58f));
            SpritePart(rig.rightElbow,"Right lower arm","LowerArm",new Vector2(0f,-.23f),new Vector2(-.64f,.62f),mat,25);
            Transform rh = Joint(rig.rightElbow,"Right wrist",new Vector2(0f,-.47f));
            SpritePart(rh,"Right hand","Hand",new Vector2(0f,-.04f),new Vector2(-.48f,.48f),mat,26);
            rig.axeGrip = Joint(rh,"Axe grip - adjust in Scene",new Vector2(.16f,-.18f));
            rig.axeGrip.localRotation = Quaternion.Euler(0f,0f,-25f);
            SpritePart(rig.axeGrip,"Axe","Axe",new Vector2(0f,.34f),new Vector2(.76f,.76f),mat,27);

            rig.leftHip = Joint(root,"Left hip",new Vector2(-.20f,1.06f));
            SpritePart(rig.leftHip,"Left upper leg","UpperLeg",new Vector2(0f,-.26f),new Vector2(.64f,.68f),mat,18);
            rig.leftKnee = Joint(rig.leftHip,"Left knee",new Vector2(0f,-.54f));
            SpritePart(rig.leftKnee,"Left lower leg","LowerLeg",new Vector2(0f,-.20f),new Vector2(.66f,.62f),mat,18);
            Transform lf = Joint(rig.leftKnee,"Left ankle",new Vector2(0f,-.43f));
            SpritePart(lf,"Left boot","Boot",new Vector2(0f,-.06f),new Vector2(.60f,.48f),mat,19);

            rig.rightHip = Joint(root,"Right hip",new Vector2(.20f,1.06f));
            SpritePart(rig.rightHip,"Right upper leg","UpperLeg",new Vector2(0f,-.26f),new Vector2(-.64f,.68f),mat,17);
            rig.rightKnee = Joint(rig.rightHip,"Right knee",new Vector2(0f,-.54f));
            SpritePart(rig.rightKnee,"Right lower leg","LowerLeg",new Vector2(0f,-.20f),new Vector2(-.66f,.62f),mat,17);
            Transform rf = Joint(rig.rightKnee,"Right ankle",new Vector2(0f,-.43f));
            SpritePart(rf,"Right boot","Boot",new Vector2(0f,-.06f),new Vector2(-.60f,.48f),mat,18);
            rig.CaptureNeutralPose();
        }
    }
}
#endif
