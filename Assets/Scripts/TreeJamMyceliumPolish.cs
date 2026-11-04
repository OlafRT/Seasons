using System.Collections.Generic;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Visual extension of the existing mycelium/market system. Does not touch
    /// the market connection logic. Thickens aliased threads, extends them beyond
    /// the zoomed-out soil edges, adds offshoots, and creates surface fruiting bodies
    /// attached to visible mycelium strands. Underground network survives winter.
    /// </summary>
    [DefaultExecutionOrder(4100)]
    [DisallowMultipleComponent]
    public sealed class TreeJamMyceliumPolish : MonoBehaviour
    {
        public TreeJamMyceliumNetwork network;
        public TreeJamSoilSurface surface;
        public TreeJamSeasonCycle seasons;
        [Header("Foreground layering")]
        [Tooltip("Assign the grass/ground-surface sprite. Underground hyphae render behind it; mushroom caps in front.")]
        public SpriteRenderer grassSprite;
        [Tooltip("How many sorting-order steps in front of grass to render mushroom fruits.")]
        [Range(1, 10)] public int fruitSortingOffset = 2;
        [Range(.05f,.25f)] public float mainStrandWidth = .145f;
        [Range(.025f,.12f)] public float smallStrandWidth = .038f;
        [Range(0,20)] public int extraBranchCountPerSide = 12;
        [Range(0f,2f)] public float offscreenExtension = .6f;
        public Color hyphaColor = new Color(.92f, .96f, .85f, 1f);
        public Color fruitColor = new Color(.96f,.88f,.71f,1f);
        public bool hideLegacyFloatingMushrooms = true;
        public bool hideMushroomFruitsInWinter = true;
        public bool showDecorativeFruits = true;

        private LineRenderer leftMain, rightMain;
        private LineRenderer leftExtension, rightExtension;
        private readonly List<LineRenderer> extra = new List<LineRenderer>();
        private readonly List<GameObject> sprouts = new List<GameObject>();
        private readonly List<LineRenderer> sproutThreads = new List<LineRenderer>();
        private Material fallbackMaterial;
        private bool built;
        private float retry;
        private static readonly AnimationCurve MainWidthCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(.75f, .97f), new Keyframe(1f, .88f));
        private static readonly AnimationCurve OffshootWidthCurve = new AnimationCurve(
            new Keyframe(0f, .90f), new Keyframe(1f, .35f));
        private static Vector3 V(float x, float y) => new Vector3(x,y,0f);

        private void Start()
        {
            if (network == null) network = FindFirstObjectByType<TreeJamMyceliumNetwork>();
            if (surface == null) surface = FindFirstObjectByType<TreeJamSoilSurface>();
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (grassSprite == null)
            {
                // Optional fallback. Assign manually if your sprite has a custom name.
                foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (sr.sprite != null && (sr.name.ToLowerInvariant().Contains("ground surface") ||
                                              sr.name.ToLowerInvariant().Contains("grass")))
                    { grassSprite = sr; break; }
            }
        }

        private void LateUpdate()
        {
            if (network == null || surface == null || surface.soilOutline == null) return;
            if (!built)
            {
                retry -= Time.deltaTime;
                if (retry <= 0f) { retry = .5f; Build(); }
                if (!built) return;
            }
            // Original controller updates its LineRenderers in Update(); modifying
            // materials and paths here is safe and doesn't change gameplay.
            foreach (LineRenderer lr in network.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr == null) continue;
                string n = lr.gameObject.name;
                if (n.StartsWith("Main mycelium"))
                {
                    // Previous width curve dwindled to 8% at the centre, making
                    // the main filament thinner than its own decorative branches.
                    lr.widthMultiplier = mainStrandWidth;
                    lr.widthCurve = MainWidthCurve;
                }
                else if (n.StartsWith("Fine hypha"))
                {
                    lr.widthMultiplier = smallStrandWidth;
                    lr.widthCurve = OffshootWidthCurve;
                }
                ApplySorting(lr, false);
                lr.numCornerVertices = 6;
                lr.numCapVertices = 6;
                lr.startColor = lr.endColor = hyphaColor;
            }
            if (hideLegacyFloatingMushrooms)
                foreach (Transform child in network.transform)
                    if (child.name.StartsWith("Fungal fruiting body")) child.localScale = Vector3.zero;

            if (FindFirstObjectByType<TreeJamFungalRoute>() == null)
            {
                DrawExtension(leftMain,leftExtension,-1);
                DrawExtension(rightMain,rightExtension,1);
            }
            else
            {
                if (leftExtension != null) leftExtension.positionCount = 0;
                if (rightExtension != null) rightExtension.positionCount = 0;
            }
            int perSide = extra.Count / 2;
            for (int i = 0; i < extra.Count; i++)
            {
                bool left = i < perSide;
                int index = left ? i : i-perSide;
                LineRenderer main = left ? leftMain : rightMain;
                LineRenderer lr = extra[i];
                // Anchor to the FULL main path, rather than a fraction of its
                // currently visible points. Otherwise every new point makes this
                // branch slide along the parent and "jump" forward.
                float fraction = (index + 1f) / (perSide + 1f);
                const int MainWaypoints = 42; // TreeJamMyceliumNetwork.Points
                int anchor = Mathf.Clamp(Mathf.RoundToInt(fraction * (MainWaypoints - 1)), 1, MainWaypoints - 2);
                float startAt = (anchor + 1f) / (MainWaypoints - 1) + .015f;
                if (main == null || main.positionCount <= anchor || network.Progress01 <= startAt)
                { lr.positionCount = 0; continue; }

                Vector3 origin = main.GetPosition(anchor);
                const int segments = 7;
                Vector3[] path = new Vector3[segments];
                for(int j=0;j<segments;j++)
                {
                    float t = j/(float)(segments-1);
                    float direction = index%2==0 ? 1f : -1f;
                    float angle = left ? 1f : -1f;
                    float dx = angle * t * (.27f + (index%4)*.11f);
                    float dy = direction * t * (.24f+(index%3)*.13f);
                    float jitter = .045f * (Mathf.Sin(t*9f + index*2.7f) - Mathf.Sin(index*2.7f));
                    path[j] = V(origin.x + dx, origin.y + dy + jitter);
                }
                float branchProgress = Mathf.Clamp01((network.Progress01 - startAt) / .14f);
                DrawSmoothGrowingPath(lr, path, branchProgress);
            }
            DrawSprouts();
            if (grassSprite != null)
            {
                ApplySorting(leftExtension, false);
                ApplySorting(rightExtension, false);
                foreach (LineRenderer strand in extra) ApplySorting(strand, false);
                foreach (LineRenderer strand in sproutThreads) ApplySorting(strand, false);
                foreach (GameObject fruitRoot in sprouts)
                {
                    if (fruitRoot == null) continue;
                    foreach (LineRenderer part in fruitRoot.GetComponentsInChildren<LineRenderer>(true))
                    {
                        bool fruit = part.name == "Stem" || part.name == "Cap";
                        ApplySorting(part, fruit);
                    }
                }
            }
        }

        private void ApplySorting(LineRenderer line, bool aboveGrass)
        {
            if (line == null || grassSprite == null) return;
            line.sortingLayerID = grassSprite.sortingLayerID;
            line.sortingOrder = grassSprite.sortingOrder + (aboveGrass ? fruitSortingOffset : -1);
        }

        private void Build()
        {
            foreach (LineRenderer lr in network.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr.name == "Main mycelium LEFT") leftMain = lr;
                else if (lr.name == "Main mycelium RIGHT") rightMain = lr;
            }
            if (leftMain == null || rightMain == null) return;
            Material material = leftMain.sharedMaterial;
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                fallbackMaterial = new Material(shader);
                material = fallbackMaterial;
            }
            leftExtension = MakeLine("OFFSCREEN mycelium LEFT",material,mainStrandWidth,2);
            rightExtension = MakeLine("OFFSCREEN mycelium RIGHT",material,mainStrandWidth,2);
            leftExtension.widthCurve = MainWidthCurve;
            rightExtension.widthCurve = MainWidthCurve;
            for(int side=0;side<2;side++)
                for(int i=0;i<extraBranchCountPerSide;i++)
                    extra.Add(MakeLine("Dense hypha "+(side==0?"L":"R")+" #"+i, material,smallStrandWidth,4));
            for (int i=0;i<4;i++)
            {
                GameObject sprout = new GameObject("Surface hypha + editable mushroom " + (i+1));
                sprout.transform.SetParent(transform,false);
                sprouts.Add(sprout);
                sproutThreads.Add(MakeLine("Upward mycelium to soil",material,smallStrandWidth,5,sprout.transform));
                Transform fruit = new GameObject("Mushroom cap & stem").transform;
                fruit.SetParent(sprout.transform,false);
                LineRenderer stem=MakeLine("Stem",material,.055f,13,fruit);
                stem.useWorldSpace=false;
                stem.positionCount=2;
                stem.SetPosition(0,V(0,0));stem.SetPosition(1,V(.012f,.17f));
                LineRenderer cap=MakeLine("Cap",material,.075f,14,fruit);
                cap.useWorldSpace=false;
                cap.positionCount=11;
                for(int p=0;p<11;p++)
                {
                    float t=p/10f;
                    cap.SetPosition(p,V(-.14f+.28f*t,.16f+.08f*Mathf.Sin(t*Mathf.PI)));
                }
                stem.startColor=stem.endColor=fruitColor;
                cap.startColor=cap.endColor=fruitColor;
            }
            built = true;
        }

        private LineRenderer MakeLine(string n, Material material,float width,int order,Transform parent=null)
        {
            GameObject go=new GameObject(n);
            go.transform.SetParent(parent!=null?parent:transform,false);
            LineRenderer lr=go.AddComponent<LineRenderer>();
            lr.sharedMaterial=material;
            lr.useWorldSpace=true;
            lr.alignment=LineAlignment.View;
            lr.numCapVertices=6;
            lr.numCornerVertices=6;
            lr.sortingOrder=order;
            lr.widthMultiplier=width;
            lr.startColor=lr.endColor=hyphaColor;
            lr.positionCount=0;
            return lr;
        }

        private void DrawExtension(LineRenderer original,LineRenderer extension,int side)
        {
            if (extension == null || network == null) return;
            // The offscreen portion already exists beyond the terrain. Draw it
            // from the start so it does not suddenly pop into view when the
            // first main-line waypoint appears.
            float centerX = network.treeRoot != null ? network.treeRoot.position.x : 0f;
            Camera camera = network.worldCamera != null ? network.worldCamera : Camera.main;
            float radius = network.spawnOffsetFromTree;
            if (camera != null) radius = Mathf.Max(radius, camera.orthographicSize * camera.aspect * .88f);
            Vector3 first = V(centerX + side * radius,
                network.soilCenterY + (side < 0 ? -.25f : .07f));
            Bounds b=surface.WorldBounds;
            float edge=side<0?b.min.x-offscreenExtension:b.max.x+offscreenExtension;
            extension.positionCount=5;
            for(int i=0;i<5;i++)
            {
                float t=i/4f;
                float x=Mathf.Lerp(edge,first.x,t);
                float y=first.y+.10f*Mathf.Sin(t*Mathf.PI*2f)*(1f-t);
                extension.SetPosition(i,V(x,y));
            }
        }

        private void DrawSprouts()
        {
            // The interactive mushroom system supplies the actual fruits. When its
            // predecessor is disabled, hide BOTH the old caps and their bare stalks.
            // Previously the caps were hidden but orphan white shoots remained.
            if (!showDecorativeFruits)
            {
                for (int i = 0; i < sprouts.Count; i++)
                {
                    if (i < sproutThreads.Count && sproutThreads[i] != null)
                        sproutThreads[i].positionCount = 0;
                    if (sprouts[i] != null)
                    {
                        Transform oldCap = sprouts[i].transform.Find("Mushroom cap & stem");
                        if (oldCap != null) oldCap.gameObject.SetActive(false);
                    }
                }
                return;
            }
            TreeSeason season=seasons!=null?seasons.CurrentSeason:TreeSeason.Summer;
            bool fruitAllowed=showDecorativeFruits && !(hideMushroomFruitsInWinter&&season==TreeSeason.Winter);
            for(int i=0;i<sprouts.Count;i++)
            {
                bool left=i<2;
                LineRenderer main=left?leftMain:rightMain;
                GameObject sprout=sprouts[i];
                LineRenderer up=sproutThreads[i];
                float needed=.50f+i*.10f;
                if(main==null||main.positionCount<5||network.Progress01<needed)
                {up.positionCount=0;sprout.transform.Find("Mushroom cap & stem").gameObject.SetActive(false);continue;}
                // Use the real attachment waypoint, not a percentage of the
                // growing pointCount (which caused the stems to move abruptly).
                const int MainWaypoints = 42;
                int index = Mathf.Clamp(Mathf.RoundToInt((i%2==0?.25f:.48f)*(MainWaypoints-1)),0,MainWaypoints-2);
                if (main.positionCount <= index)
                { up.positionCount=0;sprout.transform.Find("Mushroom cap & stem").gameObject.SetActive(false);continue; }
                Vector3 origin=main.GetPosition(index);
                float x=origin.x;
                if(!surface.TryGetHeight(x,out float y))
                {up.positionCount=0;sprout.transform.Find("Mushroom cap & stem").gameObject.SetActive(false);continue;}
                Vector3[] stemPath = new Vector3[7];
                for(int j=0;j<stemPath.Length;j++)
                {
                    float t=j/(float)(stemPath.Length-1);
                    stemPath[j]=V(origin.x + .04f*(Mathf.Sin(t*5f+i)-Mathf.Sin(i)), Mathf.Lerp(origin.y,y,t));
                }
                float stemProgress = Mathf.Clamp01((network.Progress01-needed)/.12f);
                DrawSmoothGrowingPath(up, stemPath, stemProgress);
                Transform cap=sprout.transform.Find("Mushroom cap & stem");
                cap.position=V(x,y);
                float grown=Mathf.Clamp01((network.Progress01-needed-.10f)/.13f);
                cap.gameObject.SetActive(fruitAllowed && grown>0f);
                cap.localScale=Vector3.one*Mathf.SmoothStep(0,1,grown)*(.85f + .14f*(i%2));
            }
        }
        // Complete waypoints are kept, but the leading tip moves continuously
        // between them instead of revealing a full extra segment in one frame.
        private static void DrawSmoothGrowingPath(LineRenderer line, Vector3[] path, float progress)
        {
            if (line == null || path == null || path.Length < 2 || progress <= 0f)
            {
                if (line != null) line.positionCount = 0;
                return;
            }
            if (progress >= 1f)
            {
                line.positionCount = path.Length;
                line.SetPositions(path);
                return;
            }
            float total = 0f;
            for (int j = 1; j < path.Length; j++) total += Vector3.Distance(path[j-1], path[j]);
            float left = total * Mathf.Clamp01(progress);
            for (int j = 1; j < path.Length; j++)
            {
                float segmentLength = Vector3.Distance(path[j-1], path[j]);
                if (left <= segmentLength || j == path.Length-1)
                {
                    line.positionCount = j+1;
                    for (int k = 0; k < j; k++) line.SetPosition(k, path[k]);
                    float t = segmentLength > .00001f ? Mathf.Clamp01(left / segmentLength) : 1f;
                    line.SetPosition(j, Vector3.Lerp(path[j-1], path[j], t));
                    return;
                }
                left -= segmentLength;
            }
        }

        private void OnDestroy(){if(fallbackMaterial!=null)Destroy(fallbackMaterial);}
    }
}
