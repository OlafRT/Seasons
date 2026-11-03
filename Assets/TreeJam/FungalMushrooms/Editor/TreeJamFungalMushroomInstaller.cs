using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TreeJam;

namespace TreeJam.Editor
{
    public static class TreeJamFungalMushroomInstaller
    {
        private const string Folder = "Assets/TreeJam/FungalMushrooms/Art/";
        private const string RootName = "FUNGAL MUSHROOM TRADING - EDIT ME";

        [MenuItem("Tools/Tree Jam/Install Clickable Mushroom Trading (Current Scene)")]
        public static void Install()
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("Stop Play Mode","Exit Play Mode before installing.","OK");return; }
            Scene scene=SceneManager.GetActiveScene();
            TreeJamFungiMarket market=UnityEngine.Object.FindFirstObjectByType<TreeJamFungiMarket>();
            TreeJamMyceliumNetwork network=UnityEngine.Object.FindFirstObjectByType<TreeJamMyceliumNetwork>();
            TreeJamSoilSurface ground=UnityEngine.Object.FindFirstObjectByType<TreeJamSoilSurface>();
            TreeGameState resources=UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
            if (!scene.IsValid() || market==null || network==null || ground==null || resources==null)
            {
                EditorUtility.DisplayDialog("Scene references missing", "Open the Tree Jam gameplay scene and make sure it contains TreeJamFungiMarket, TreeJamMyceliumNetwork, TreeJamSoilSurface and TreeGameState. No changes made.","OK");
                return;
            }
            GameObject previous=GameObject.Find(RootName);
            if(previous!=null)
            {
                Selection.activeGameObject=previous;
                EditorUtility.DisplayDialog("Already installed", "The fungal mushroom setup exists. It was selected in the Hierarchy. Nothing was recreated or overwritten.","OK");
                return;
            }
            Sprite mushroom=LoadSprite(Folder+"MushroomTrade.png");
            Sprite upgrade=LoadSprite(Folder+"FungalUpgradeOrb.png");
            if(mushroom==null || upgrade==null)
            {
                EditorUtility.DisplayDialog("Artwork missing", "Import the entire patch folder, including the PNG assets, before running the installer.","OK");return;
            }
            Shader unlit=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlit==null) unlit=Shader.Find("Sprites/Default");
            Material mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"TJ_FungalObjects_Unlit.mat");
            if(mat==null && unlit!=null)
            {
                mat=new Material(unlit){name="TJ Fungal Objects Unlit"};
                AssetDatabase.CreateAsset(mat, Folder+"TJ_FungalObjects_Unlit.mat");
            }
            GameObject root=new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root,"Create mushroom trading system");
            TreeJamFungalRoute route=root.AddComponent<TreeJamFungalRoute>();
            route.network=network; route.surface=ground;
            TreeJamFungalMushroomSystem system=root.AddComponent<TreeJamFungalMushroomSystem>();
            system.market=market; system.network=network;system.ground=ground;system.resources=resources;
            system.worldCamera=network.worldCamera!=null?network.worldCamera:Camera.main;
            system.seasons=UnityEngine.Object.FindFirstObjectByType<TreeJamSeasonCycle>();
            system.mushroomRegrowSeconds=48f;
            int groundOrder=3;
            TreeJamMyceliumPolish polish=UnityEngine.Object.FindFirstObjectByType<TreeJamMyceliumPolish>();
            if (polish!=null)
            {
                Undo.RecordObject(polish,"Prefer interactive mushrooms");
                polish.showDecorativeFruits=false;
                polish.hideLegacyFloatingMushrooms=true;
                polish.mainStrandWidth=.15f;
                if (polish.grassSprite!=null)
                {
                    SpriteRenderer soilSprite = ground.soilOutline != null
                        ? ground.soilOutline.GetComponent<SpriteRenderer>() : null;
                    if (soilSprite!=null && soilSprite.sortingLayerID==polish.grassSprite.sortingLayerID &&
                        polish.grassSprite.sortingOrder <= soilSprite.sortingOrder+1)
                    {
                        Undo.RecordObject(polish.grassSprite,"Make room for underground fungal strands");
                        polish.grassSprite.sortingOrder=soilSprite.sortingOrder+2;
                        EditorUtility.SetDirty(polish.grassSprite);
                    }
                    groundOrder=polish.grassSprite.sortingOrder+2;
                }
                EditorUtility.SetDirty(polish);
            }
            string mushroomParent="MUSHROOMS - drag X offset to reposition";
            GameObject holder=NewChild(root.transform,mushroomParent);
            AddMushroom(holder.transform,"LEFT - Energy to Nutrients",true,true,false,2.75f,mushroom,mat,groundOrder, ground);
            AddMushroom(holder.transform,"RIGHT - Nutrients to Energy",false,false,false,2.75f,mushroom,mat,groundOrder, ground);
            AddMushroom(holder.transform,"BONUS LEFT - Spore Colony",true,true,true,3.9f,mushroom,mat,groundOrder, ground);
            AddMushroom(holder.transform,"BONUS RIGHT - Spore Colony",false,false,true,3.9f,mushroom,mat,groundOrder, ground);

            GameObject upgrades=NewChild(root.transform,"MYCELIUM UPGRADES - editable nodes");
            AddUpgrade(upgrades.transform, "F01 - Rapid Fruiting", FungalUpgradeKind.RapidFruiting,
                "Mushrooms regrow 30% faster.",36,18,false,FungalUpgradeKind.RapidFruiting,-3.1f,1.4f,ground,upgrade,mat);
            AddUpgrade(upgrades.transform,"F02 - Courier Hyphae",FungalUpgradeKind.CourierHyphae,
                "Resource packets travel 30% faster.",39,22,false,FungalUpgradeKind.CourierHyphae,3.0f,1.4f,ground,upgrade,mat);
            AddUpgrade(upgrades.transform,"F03 - Better Returns",FungalUpgradeKind.BetterReturns,
                "Receive 20% more resources from each trade.",55,30,true,FungalUpgradeKind.RapidFruiting,-4.2f,2.1f,ground,upgrade,mat);
            AddUpgrade(upgrades.transform,"F04 - Spore Colony",FungalUpgradeKind.SporeColony,
                "Grow one extra trading mushroom on each side.",60,34,true,FungalUpgradeKind.CourierHyphae,4.15f,2.1f,ground,upgrade,mat);

            TreeJamUI ui=UnityEngine.Object.FindFirstObjectByType<TreeJamUI>();
            if(ui!=null && ui.tooltipPanel!=null && ui.rootCanvas!=null)
            {
                GameObject duplicate=UnityEngine.Object.Instantiate(ui.tooltipPanel.gameObject,ui.tooltipPanel.parent);
                duplicate.name="Fungal Tooltip - edit layout here";
                Undo.RegisterCreatedObjectUndo(duplicate,"Create fungal tooltip");
                duplicate.SetActive(false);
                RectTransform rect=duplicate.GetComponent<RectTransform>();
                system.fungalTooltip=rect;
                system.rootCanvas=ui.rootCanvas;
                system.tooltipTitle=FindText(duplicate,"Upgrade Name");
                system.tooltipDescription=FindText(duplicate,"Description");
                system.tooltipCosts=FindText(duplicate,"Costs");
                system.tooltipHint=FindText(duplicate,"Purchase Hint");
                foreach(Graphic graphic in duplicate.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget=false;
            }
            Undo.RecordObject(market,"Enable mushroom interaction mode");
            market.mushroomTradingMode=true;
            market.fungalRoute=route;
            market.cooldownAfterDelivery=0f;
            market.courierScale=.35f;
            if(market.openButton!=null)
            {
                Undo.RecordObject(market.openButton.gameObject,"Hide obsolete market button");
                market.openButton.gameObject.SetActive(false);
            }
            if(market.panel!=null)
            {
                Undo.RecordObject(market.panel,"Hide obsolete market window");
                market.panel.SetActive(false);
            }
            EditorUtility.SetDirty(market);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject=root;
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Mushroom trading installed",
                "Two interactive mushrooms, four mycelium upgrades and two unlockable bonus mushrooms were added. The old market UI is hidden, not deleted. The fungus routes are reshaped below the soil.\n\nSave your scene (Ctrl+S). Test a whole delivery after the network connects. Move mushrooms by editing their X Offset From Tree fields.","OK");
        }

        private static GameObject NewChild(Transform parent,string name)
        {
            GameObject go=new GameObject(name); go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Create fungal object");return go;
        }
        private static Sprite LoadSprite(string path)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null) return null;
            if(importer.textureType!=TextureImporterType.Sprite || importer.spriteImportMode!=SpriteImportMode.Single || importer.mipmapEnabled)
            {
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false;
                importer.alphaIsTransparency=true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void AddMushroom(Transform parent,string name,bool left,bool pay,bool extra,float offset,
            Sprite art,Material mat,int order,TreeJamSoilSurface ground)
        {
            GameObject go=NewChild(parent,name);
            TreeJamTradeMushroom fruit=go.AddComponent<TreeJamTradeMushroom>();
            fruit.leftSide=left;fruit.payEnergy=pay;fruit.extraColony=extra;fruit.xOffsetFromTree=offset;
            fruit.visualScale=new Vector3(.32f,.32f,1f);
            CircleCollider2D collider=go.GetComponent<CircleCollider2D>();
            if(collider==null) collider=go.AddComponent<CircleCollider2D>();
            collider.isTrigger=true;collider.radius=.75f;collider.offset=new Vector2(0,.6f);
            GameObject graphic=NewChild(go.transform,"Mushroom art - replace sprite here");
            SpriteRenderer sr=graphic.AddComponent<SpriteRenderer>();
            sr.sprite=art;sr.sharedMaterial=mat;sr.sortingOrder=order;
            graphic.transform.localPosition=new Vector3(0f,.70f,0f); // Bring the stem base to the painted soil surface.
            // Initial editor positioning; the runtime repositions using the actual soil outline.
            float x=(left?-1f:1f)*offset;
            float y=ground.HeightOr(x,-1.3f);
            go.transform.position=new Vector3(x,y,0f);
        }
        private static void AddUpgrade(Transform parent,string title,FungalUpgradeKind kind,string description,
            float energy,float nutrients,bool requires,FungalUpgradeKind prerequisite,float x,float depth,
            TreeJamSoilSurface ground,Sprite art,Material mat)
        {
            GameObject go=NewChild(parent,title);
            float y=ground.HeightOr(x,-1.3f)-depth;
            go.transform.position=new Vector3(x,y,0f);
            CircleCollider2D col=go.AddComponent<CircleCollider2D>();col.isTrigger=true;col.radius=.26f;
            TreeJamFungalUpgradeNode node=go.AddComponent<TreeJamFungalUpgradeNode>();
            node.upgrade=kind;node.displayName=title.Substring(6);node.description=description;
            node.energyCost=energy;node.nutrientCost=nutrients;node.hasPrerequisite=requires;node.prerequisite=prerequisite;
            GameObject icon=NewChild(go.transform,"Glowing mycelium upgrade orb");
            SpriteRenderer sr=icon.AddComponent<SpriteRenderer>();sr.sprite=art;sr.sharedMaterial=mat;
            sr.color=new Color(.71f,.94f,.83f,1f);sr.sortingOrder=8;
            icon.transform.localScale=Vector3.one*.42f;
        }
        private static Text FindText(GameObject root,string name)
        {
            foreach(Text t in root.GetComponentsInChildren<Text>(true))
                if(t.name.Equals(name,StringComparison.OrdinalIgnoreCase)) return t;
            return root.GetComponentInChildren<Text>(true);
        }
    }
}
