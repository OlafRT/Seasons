using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// An optional late-game reward that sprouts many small decorative feeder roots
    /// from the already-grown woody roots. It does not modify resource accounting.
    /// Rootlets share their parent's procedural-tree transform, so they follow
    /// the tree's gradual yearly scale growth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamRootletCrownBurst : MonoBehaviour
    {
        public TreeGameState gameState;
        public OrganicBonsaiTree tree;
        public UpgradeDefinition triggerUpgrade;
        [Min(.1f)] public float waitForParentGrowth = 7.5f;
        [Min(.2f)] public float rootletGrowthSeconds = 3.5f;
        [Range(1, 3)] public int rootletsPerExistingRoot = 2;
        [Range(.01f, .10f)] public float rootletWidth = .035f;

        private bool started;
        private readonly List<LineRenderer> generated = new List<LineRenderer>();

        private void Awake()
        {
            if (gameState == null) gameState = FindFirstObjectByType<TreeGameState>();
            if (tree == null) tree = FindFirstObjectByType<OrganicBonsaiTree>();
        }

        private void Update()
        {
            if (started || gameState == null || triggerUpgrade == null) return;
            if (!gameState.IsPurchased(triggerUpgrade)) return;
            started = true;
            StartCoroutine(GrowAfterParent());
        }

        private IEnumerator GrowAfterParent()
        {
            yield return new WaitForSeconds(waitForParentGrowth);
            if (tree == null) yield break;
            var existing = tree.GetComponentsInChildren<LineRenderer>(true);
            int seed = 0;
            foreach (LineRenderer source in existing)
            {
                if (source == null || source.sortingOrder != 2 || source.positionCount < 8 ||
                    !source.name.Contains("(curved wood)")) continue;
                // Do not recursively generate rootlets from generated rootlets.
                for (int j = 0; j < rootletsPerExistingRoot; j++)
                {
                    float at = j == 0 ? .35f : j == 1 ? .70f : .85f;
                    int i = Mathf.Clamp(Mathf.RoundToInt((source.positionCount-1) * at), 1, source.positionCount-2);
                    Vector3 origin = source.GetPosition(i);
                    Vector3 delta = source.GetPosition(i+1) - source.GetPosition(i-1);
                    Vector2 tangent = new Vector2(delta.x,delta.y).normalized;
                    if (tangent.sqrMagnitude < .1f) continue;
                    Vector2 normal = new Vector2(-tangent.y,tangent.x) * ((seed++ % 2 == 0) ? 1f : -1f);
                    Vector2 offset = normal * (.20f + .06f*j) + Vector2.down * (.10f+.03f*j);
                    Vector3 end = origin + new Vector3(offset.x,offset.y,0);
                    GameObject go = new GameObject("Fine feeder rootlet (late-game)");
                    go.transform.SetParent(source.transform.parent,false);
                    LineRenderer line = go.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.alignment = LineAlignment.View;
                    line.numCapVertices = 6;
                    line.numCornerVertices = 4;
                    line.sharedMaterial = source.sharedMaterial;
                    line.sortingLayerID = source.sortingLayerID;
                    line.sortingOrder = source.sortingOrder;
                    line.widthMultiplier = rootletWidth;
                    line.widthCurve = new AnimationCurve(new Keyframe(0f,1f), new Keyframe(.7f,.6f), new Keyframe(1f,.06f));
                    line.startColor = source.startColor;
                    line.endColor = source.endColor;
                    generated.Add(line);
                    StartCoroutine(AnimateRootlet(line,origin,end));
                    yield return new WaitForSeconds(.12f);
                }
            }
        }

        private IEnumerator AnimateRootlet(LineRenderer line,Vector3 origin,Vector3 end)
        {
            const int divisions = 14;
            float timer = 0f;
            while (timer < rootletGrowthSeconds && line != null)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f,1f,Mathf.Clamp01(timer/rootletGrowthSeconds));
                int count = Mathf.Max(2,Mathf.CeilToInt(divisions*t)+1);
                line.positionCount = count;
                for(int i=0;i<count;i++)
                {
                    float u = t * i/(count-1f);
                    Vector3 p = Vector3.Lerp(origin,end,u);
                    p.y -= Mathf.Sin(u*Mathf.PI) * .07f;
                    line.SetPosition(i,p);
                }
                yield return null;
            }
        }
    }
}
