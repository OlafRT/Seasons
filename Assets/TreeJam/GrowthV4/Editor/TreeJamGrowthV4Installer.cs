#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    public static class TreeJamGrowthV4Installer
    {
        const string OrbGroupName = "UPGRADE ORBS V2 - DRAG TO SHAPE TREE";
        const string AssetFolder = "Assets/TreeJam/DataV4";

        struct Spec
        {
            public string id,name,description,parent,extra;
            public float dx,dy,costE,costN,bend,width;
            public int clusters;
            public bool root;
            public UpgradeEffect[] effects;
            public Spec(string id,string name,string desc,string parent,string extra,
                float dx,float dy,float e,float n,bool root,int clusters,float bend,float width,params UpgradeEffect[] fx)
            {
                this.id=id;this.name=name;description=desc;this.parent=parent;this.extra=extra;
                this.dx=dx;this.dy=dy;costE=e;costN=n;this.root=root;
                this.clusters=clusters;this.bend=bend;this.width=width;effects=fx;
            }
        }
        static UpgradeEffect L(float v) => new UpgradeEffect{type=UpgradeEffectType.AddLeaves,amount=v};
        static UpgradeEffect E(float v) => new UpgradeEffect{type=UpgradeEffectType.AddEnergyPerSecond,amount=v};
        static UpgradeEffect N(float v) => new UpgradeEffect{type=UpgradeEffectType.AddNutrientsPerSecond,amount=v};
        static UpgradeEffect EC(float v) => new UpgradeEffect{type=UpgradeEffectType.AddEnergyCapacity,amount=v};
        static UpgradeEffect NC(float v) => new UpgradeEffect{type=UpgradeEffectType.AddNutrientCapacity,amount=v};

        static readonly Spec[] Tree = {
            // Six additions above ground. Every new leaf bonus creates individual
            // leaves that are eligible to fall in autumn and regrow in spring.
            new Spec("C25","West Bud Clusters","Grow four leaves on the west canopy.","C22",null,
                -.52f,.41f,63,37,false,2,-.13f,.075f,L(4)),
            new Spec("C26","Western Foliage Pads","Grow six leaves on the outer western crown.","C16",null,
                -.42f,.48f,103,67,false,3,-.13f,.085f,L(6)),
            new Spec("C27","Upper Leaf Fans","Grow six leaves high in the central canopy.","C18",null,
                .09f,.49f,106,64,false,3,.12f,.08f,L(6)),
            new Spec("C28","Eastern Foliage Pads","Grow six leaves on the outer eastern crown.","C20",null,
                .46f,.49f,104,65,false,3,.12f,.085f,L(6)),
            new Spec("C29","Dusk Leaflets","Grow four leaves and add 0.25 peak energy per second.","C21",null,
                .39f,.37f,100,59,false,2,-.11f,.075f,L(4),E(.25f)),
            // The renderer spreads this upgrade's leaf bonus across all canopy pads;
            // its new limb also has its own foliage slots to avoid a bare tip.
            new Spec("C30","Crown Abundance","Grow twelve additional leaves across the whole canopy.","C27","C03",
                .27f,.52f,156,103,false,2,.10f,.07f,L(12)),

            // Six additions underground: improved storage and nutrient supply.
            new Spec("R13","Fine Soil Threads","Extend small rootlets. Add 0.30 nutrients per second.","R11",null,
                -.43f,-.42f,64,40,true,0,-.11f,.067f,N(.30f)),
            new Spec("R14","Sap Reservoirs","Grow deeper storage roots. Increase maximum energy by 25.","R12",null,
                .47f,-.44f,70,42,true,0,.14f,.075f,EC(25)),
            new Spec("R15","Mineral Seekers","Extend roots deeper. Add 0.40 nutrients per second.","R07",null,
                -.46f,-.56f,108,72,true,0,-.12f,.065f,N(.40f)),
            new Spec("R16","Earth Pockets","Grow storage roots. Increase maximum nutrients by 40.","R08",null,
                -.31f,-.48f,104,63,true,0,.13f,.07f,NC(40)),
            new Spec("R17","Root Webbing","Branch underground. Add 0.20 nutrients per second and 20 energy storage.","R09",null,
                .34f,-.56f,111,72,true,0,-.10f,.07f,N(.20f),EC(20)),
            new Spec("R18","Feeder Root Crown","Sprout fine rootlets across existing roots. Increase nutrient storage by 45.","R10",null,
                .45f,-.51f,148,97,true,0,.11f,.075f,NC(45)),
        };

        struct FSpec
        {
            public string id,name,description;
            public FungalUpgradeKind kind,prerequisite;
            public float x,depth,e,n;
            public FSpec(string id,string name,string desc,FungalUpgradeKind kind,FungalUpgradeKind prereq,
                float x,float depth,float e,float n)
            {this.id=id;this.name=name;description=desc;this.kind=kind;prerequisite=prereq;
                this.x=x;this.depth=depth;this.e=e;this.n=n;}
        }
        static readonly FSpec[] Fungi = {
            new FSpec("F05","Fruiting Fans","Mushrooms regrow an additional 15% faster.",
                FungalUpgradeKind.FruitingFans,FungalUpgradeKind.RapidFruiting,-3.50f,2.65f,75,47),
            new FSpec("F06","Express Hyphae","Fungal courier deliveries travel an additional 20% faster.",
                FungalUpgradeKind.ExpressHyphae,FungalUpgradeKind.CourierHyphae,3.55f,2.68f,80,49),
            new FSpec("F07","Generous Symbionts","Receive an additional 10% resources from each fungal trade.",
                FungalUpgradeKind.GenerousSymbiosis,FungalUpgradeKind.BetterReturns,-4.62f,3.02f,99,68),
            new FSpec("F08","Winter Web","Mushroom regrowth no longer slows during winter.",
                FungalUpgradeKind.WinterWeb,FungalUpgradeKind.SporeColony,4.58f,3.03f,93,62),
        };

        [MenuItem("Tools/Tree Jam/Growth V4 - 16 Nodes + Branch Animation Fix")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode","Stop Play Mode before installing upgrades.","OK");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            Transform treeGroup = GameObject.Find(OrbGroupName)?.transform;
            TreeGameState state = UnityEngine.Object.FindFirstObjectByType<TreeGameState>();
            OrganicBonsaiTree bonsai = UnityEngine.Object.FindFirstObjectByType<OrganicBonsaiTree>();
            TreeJamFungalMushroomSystem fungiSystem = UnityEngine.Object.FindFirstObjectByType<TreeJamFungalMushroomSystem>();
            TreeJamSoilSurface soil = UnityEngine.Object.FindFirstObjectByType<TreeJamSoilSurface>();
            TreeJamFungalRoute route = UnityEngine.Object.FindFirstObjectByType<TreeJamFungalRoute>();
            if (!scene.IsValid() || treeGroup==null || state==null || bonsai==null || fungiSystem==null || soil==null || route==null)
            {
                EditorUtility.DisplayDialog("Scene not ready",
                    "Open your actual gameplay scene with V2/V3 orbs, OrganicBonsaiTree, soil, fungal mushrooms and route. Nothing changed.","OK");
                return;
            }
            Transform fungalGroup = FindChild(fungiSystem.transform,"MYCELIUM UPGRADES");
            var treeOrbs = new Dictionary<string,UpgradeOrb>();
            foreach (Transform item in treeGroup)
            {
                UpgradeOrb orb=item.GetComponent<UpgradeOrb>();
                if (orb==null) continue;
                string id=item.name.Split('|')[0].Trim();
                if (!treeOrbs.ContainsKey(id)) treeOrbs.Add(id,orb);
            }
            var fungalOrbs=new Dictionary<string,TreeJamFungalUpgradeNode>();
            if (fungalGroup!=null)
                foreach (Transform item in fungalGroup)
                {
                    TreeJamFungalUpgradeNode node=item.GetComponent<TreeJamFungalUpgradeNode>();
                    if (node==null) continue;
                    string id=item.name.Split('-')[0].Trim();
                    if (!fungalOrbs.ContainsKey(id)) fungalOrbs.Add(id,node);
                }
            // Validate prerequisites in creation order. A parent such as C27 may be
            // created earlier in this SAME installation, so it will not yet be in
            // treeOrbs during this preflight check. Do not reject it as missing.
            var missing=new List<string>();
            var availableIds=new HashSet<string>(treeOrbs.Keys);
            foreach(Spec spec in Tree)
            {
                UpgradeOrb existingParent;
                if (!availableIds.Contains(spec.parent) ||
                    (treeOrbs.TryGetValue(spec.parent,out existingParent) && existingParent.definition==null))
                    missing.Add(spec.id+" requires "+spec.parent);

                if (!string.IsNullOrEmpty(spec.extra))
                {
                    UpgradeOrb existingExtra;
                    if (!availableIds.Contains(spec.extra) ||
                        (treeOrbs.TryGetValue(spec.extra,out existingExtra) && existingExtra.definition==null))
                        missing.Add(spec.id+" also requires "+spec.extra);
                }

                UpgradeOrb existingNode;
                if (treeOrbs.TryGetValue(spec.id,out existingNode) && existingNode.definition==null)
                    missing.Add(spec.id+" exists but has no UpgradeDefinition");

                // The installer creates specs in this order (C27 precedes C30).
                availableIds.Add(spec.id);
            }
            if (fungalGroup==null || !fungalOrbs.ContainsKey("F01")) missing.Add("F01 fungal upgrade template");
            if (missing.Count>0)
            {
                EditorUtility.DisplayDialog("Missing prerequisites",string.Join("\n",missing.ToArray())+
                    "\n\nNo scene or asset changes were made.","OK");
                return;
            }
            if(!EditorUtility.DisplayDialog("Expand the tree and fungal network?",
                "Add up to 6 canopy nodes, 6 root nodes and 4 fungal nodes.\n"+
                "Existing orb locations, upgrade assets, mushrooms, artwork and UI stay intact.\n"+
                "This tool is safe to rerun; it adds only missing V4 nodes.","Install V4","Cancel")) return;

            EnsureFolder(AssetFolder);
            int canopy=0,roots=0,fungal=0;
            UpgradeDefinition rootCrown=null;
            foreach(Spec spec in Tree)
            {
                if(treeOrbs.TryGetValue(spec.id,out UpgradeOrb already))
                {
                    if(spec.id=="R18") rootCrown=already.definition;
                    continue;
                }
                UpgradeOrb parent=treeOrbs[spec.parent];
                GameObject go=UnityEngine.Object.Instantiate(parent.gameObject,treeGroup);
                go.name=spec.id+" | "+spec.name;
                go.SetActive(true);
                go.transform.localPosition=parent.transform.localPosition+new Vector3(spec.dx,spec.dy,0);
                Undo.RegisterCreatedObjectUndo(go,"Create "+spec.name);
                string path=AssetFolder+"/V4_"+spec.id+".asset";
                UpgradeDefinition def=AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
                if(def==null)
                {
                    def=ScriptableObject.CreateInstance<UpgradeDefinition>();
                    def.displayName=spec.name;
                    def.description=spec.description;
                    def.energyCost=spec.costE;
                    def.nutrientCost=spec.costN;
                    def.exclusiveGroup="";
                    var req=new List<UpgradeDefinition>{parent.definition};
                    if (!string.IsNullOrEmpty(spec.extra)) req.Add(treeOrbs[spec.extra].definition);
                    def.prerequisites=req.ToArray();
                    def.effects=spec.effects;
                    AssetDatabase.CreateAsset(def,path);
                }
                UpgradeOrb newOrb=go.GetComponent<UpgradeOrb>();
                BonsaiGrowthLink link=go.GetComponent<BonsaiGrowthLink>();
                if(newOrb==null || link==null) {Debug.LogError("V4: Orb template has no UpgradeOrb or BonsaiGrowthLink: "+parent.name);continue;}
                newOrb.definition=def;
                newOrb.gameState=state;
                newOrb.growthVisual=null;
                link.upgrade=def;
                link.parentOrb=parent;
                link.isRoot=spec.root;
                link.growToOrb=true;
                link.start=Vector2.zero;
                link.end=Vector2.zero;
                link.foliageClusters=spec.clusters;
                link.bend=spec.bend;
                link.baseThickness=spec.width;
                EditorUtility.SetDirty(newOrb);
                EditorUtility.SetDirty(link);
                treeOrbs[spec.id]=newOrb;
                if(spec.root) roots++; else canopy++;
                if(spec.id=="R18") rootCrown=def;
            }

            TreeJamFungalUpgradeNode template=fungalOrbs["F01"];
            foreach(FSpec spec in Fungi)
            {
                if(fungalOrbs.ContainsKey(spec.id)) continue;
                GameObject go=UnityEngine.Object.Instantiate(template.gameObject,fungalGroup);
                go.name=spec.id+" - "+spec.name;
                go.SetActive(true);
                Undo.RegisterCreatedObjectUndo(go,"Create "+spec.name);
                TreeJamFungalUpgradeNode node=go.GetComponent<TreeJamFungalUpgradeNode>();
                node.upgrade=spec.kind;
                node.displayName=spec.name;
                node.description=spec.description;
                node.energyCost=spec.e;
                node.nutrientCost=spec.n;
                node.hasPrerequisite=true;
                node.prerequisite=spec.prerequisite;
                node.purchased=false;
                node.visible=false;
                go.transform.position=new Vector3(spec.x,soil.HeightOr(spec.x,-1.5f)-spec.depth,0f);
                EditorUtility.SetDirty(node);
                fungalOrbs[spec.id]=node;
                fungal++;
            }

            if(rootCrown!=null)
            {
                TreeJamRootletCrownBurst bonus=bonsai.GetComponent<TreeJamRootletCrownBurst>();
                if(bonus==null) bonus=Undo.AddComponent<TreeJamRootletCrownBurst>(bonsai.gameObject);
                Undo.RecordObject(bonus,"Assign late-game rootlets");
                bonus.gameState=state;bonus.tree=bonsai;bonus.triggerUpgrade=rootCrown;
                EditorUtility.SetDirty(bonus);
            }
            Undo.RecordObject(route,"Join mycelium at tree base");
            route.centerContactDistance=0f;
            route.centerDepth=.23f;
            route.belowSurfaceMargin=.23f;
            EditorUtility.SetDirty(route);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject=treeGroup.gameObject;
            Debug.Log("Tree Jam V4: added "+canopy+" canopy, "+roots+" roots, "+fungal+
                " fungal upgrade nodes. Closed central fungal gap; preserved existing positions.");
            EditorUtility.DisplayDialog("V4 installed",
                "Added "+canopy+" canopy, "+roots+" root and "+fungal+" fungal nodes.\n"+
                "Moved neither the original orbs nor your artwork.\n"+
                "Mycelium now joins at tree center. Save the scene and test branch growth.","OK");
        }

        static Transform FindChild(Transform root,string prefix)
        {
            foreach(Transform child in root)
                if(child.name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }
        static void EnsureFolder(string path)
        {
            string[] parts=path.Split('/');
            string current=parts[0];
            for(int i=1;i<parts.Length;i++)
            {
                string next=current+"/"+parts[i];
                if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]);
                current=next;
            }
        }
    }
}
#endif
