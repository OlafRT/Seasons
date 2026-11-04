using System.Collections.Generic;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Shapes the ORIGINAL main fungal LineRenderers into two submerged courier roads.
    /// Waypoints stay fixed as the tip grows, so there are no segment jumps.
    /// Does not change TreeJamMyceliumNetwork's timer or Connected state.
    /// </summary>
    [DefaultExecutionOrder(4000)]
    [DisallowMultipleComponent]
    public sealed class TreeJamFungalRoute : MonoBehaviour
    {
        public TreeJamMyceliumNetwork network;
        public TreeJamSoilSurface surface;
        [Min(.15f)] public float centerDepth = .23f;
        [Min(.15f)] public float edgeDepth = .52f;
        [Min(0f)] public float middleSag = .32f;
        [Min(.1f)] public float belowSurfaceMargin = .23f;
        [Min(.1f)] public float extendBeyondSoil = 1.2f;
        [Range(0f,.6f)] public float centerContactDistance = 0f;
        [Min(0.04f)] public float mainStrandWidth = .15f;
        private LineRenderer left, right;
        private const int Steps = 42;

        private void Awake()
        {
            if (network == null) network = FindFirstObjectByType<TreeJamMyceliumNetwork>();
            if (surface == null) surface = FindFirstObjectByType<TreeJamSoilSurface>();
        }

        private void LateUpdate()
        {
            if (network == null || surface == null || surface.soilOutline == null) return;
            if (left == null || right == null)
            {
                foreach (LineRenderer line in network.GetComponentsInChildren<LineRenderer>(true))
                {
                    if (line.name == "Main mycelium LEFT") left = line;
                    if (line.name == "Main mycelium RIGHT") right = line;
                }
                if (left == null || right == null) return;
            }
            UpdateLine(left, -1);
            UpdateLine(right, 1);
        }

        public Vector3 ConnectionPoint
        {
            get
            {
                float x = network != null && network.treeRoot != null ? network.treeRoot.position.x : 0f;
                float y = surface != null ? Ground(x) - centerDepth : -2f;
                return new Vector3(x, y, 0f);
            }
        }

        private float Ground(float x)
        {
            if (surface == null || surface.soilOutline == null) return -1.5f;
            Bounds b = surface.WorldBounds;
            float sampleX = Mathf.Clamp(x, b.min.x + .01f, b.max.x - .01f);
            return surface.HeightOr(sampleX, b.center.y + b.extents.y * .5f);
        }

        private Vector3 Sample(bool leftSide, float t)
        {
            float cx = ConnectionCenterX();
            Bounds b = surface.WorldBounds;
            float edgeX = leftSide ? b.min.x - extendBeyondSoil : b.max.x + extendBeyondSoil;
            float x = Mathf.Lerp(edgeX, cx + (leftSide ? -1f : 1f) * centerContactDistance, t);
            float yFromEdge = Ground(edgeX) - edgeDepth;
            float yAtTree = Ground(cx) - centerDepth;
            float shaped = Mathf.SmoothStep(0f, 1f, t);
            float y = Mathf.Lerp(yFromEdge, yAtTree, shaped) - Mathf.Sin(t * Mathf.PI) * middleSag;
            y = Mathf.Min(y, Ground(x) - belowSurfaceMargin);
            return new Vector3(x, y, 0f);
        }

        private float ConnectionCenterX()
        {
            return network != null && network.treeRoot != null ? network.treeRoot.position.x : 0f;
        }

        private void UpdateLine(LineRenderer line, int side)
        {
            float progress = Mathf.Clamp01(network.Progress01);
            if (progress <= .0001f) { line.positionCount = 0; return; }
            float traveled = progress * (Steps - 1);
            int whole = Mathf.Min(Steps - 2, Mathf.FloorToInt(traveled));
            float remaining = traveled - whole;
            if (progress >= 1f) { whole = Steps - 2; remaining = 1f; }
            line.positionCount = whole + 2;
            for (int i=0; i<=whole; i++) line.SetPosition(i, Sample(side < 0, i/(float)(Steps-1)));
            Vector3 a = Sample(side < 0, whole/(float)(Steps-1));
            Vector3 c = Sample(side < 0, (whole+1f)/(Steps-1));
            line.SetPosition(whole+1, Vector3.Lerp(a,c,remaining));
            line.widthMultiplier = mainStrandWidth;
            line.widthCurve = AnimationCurve.Constant(0f,1f,1f);
            line.numCapVertices = 8;
            line.numCornerVertices = 8;
        }

        /// <summary>World-space, continuous outbound path from tree base to a distant end.</summary>
        public List<Vector3> BuildCourierRoute(bool sendLeft)
        {
            if (network == null || !network.Connected || surface == null || surface.soilOutline == null) return null;
            var path = new List<Vector3>(Steps+1) { ConnectionPoint };
            for (int i=Steps-1;i>=0;i--) path.Add(Sample(sendLeft, i/(float)(Steps-1)));
            return path;
        }
    }
}
