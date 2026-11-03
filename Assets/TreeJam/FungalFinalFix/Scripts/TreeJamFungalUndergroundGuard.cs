using System.Collections.Generic;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Final visual safety pass for ALL underground hyphae (including decorative offshoots).
    /// Runs after the original network, route and polish scripts update their lines.
    /// This does not move fruiting mushroom sprites or change any resource logic.
    /// </summary>
    [DefaultExecutionOrder(30000)]
    [DisallowMultipleComponent]
    public sealed class TreeJamFungalUndergroundGuard : MonoBehaviour
    {
        public TreeJamSoilSurface soil;
        public TreeJamMyceliumNetwork network;
        public TreeJamMyceliumPolish decorativeNetwork;
        public TreeJamFungalUpgradeGrowth upgradeGrowth;
        [Tooltip("Clearance BELOW the soil outline, on top of half the line thickness.")]
        [Min(0.04f)] public float surfaceClearance = 0.14f;
        [Tooltip("Subdivide long segments so a bend cannot cross above a hill between vertices.")]
        [Range(0.04f, 0.3f)] public float maxSegmentLength = 0.10f;

        private readonly HashSet<LineRenderer> processed = new HashSet<LineRenderer>();
        private readonly List<Vector3> vertices = new List<Vector3>(256);
        private bool warned;

        private void Awake()
        {
            if (soil == null) soil = FindFirstObjectByType<TreeJamSoilSurface>();
            if (network == null) network = FindFirstObjectByType<TreeJamMyceliumNetwork>();
            if (decorativeNetwork == null) decorativeNetwork = FindFirstObjectByType<TreeJamMyceliumPolish>();
            if (upgradeGrowth == null) upgradeGrowth = FindFirstObjectByType<TreeJamFungalUpgradeGrowth>();
        }

        /// <summary>Reusable ground clamp for the upgraded fungal branches.</summary>
        public Vector3 ClampUnderSoil(Vector3 point, float strandWidth = 0f)
        {
            if (soil == null || soil.soilOutline == null) return point;
            Bounds bounds = soil.WorldBounds;
            if (bounds.size.x < 0.01f) return point;
            // Mycelium deliberately starts beyond the artwork. Use the nearest terrain
            // edge's height there instead of letting those portions escape the ground.
            float x = Mathf.Clamp(point.x, bounds.min.x + 0.002f, bounds.max.x - 0.002f);
            if (!soil.TryGetHeight(x, out float groundY)) return point;
            point.y = Mathf.Min(point.y, groundY - surfaceClearance - strandWidth * 0.5f);
            return point;
        }

        private void LateUpdate()
        {
            if (soil == null || soil.soilOutline == null)
            {
                if (!warned)
                {
                    Debug.LogWarning("Tree Jam: fungal underground guard needs the Soil Surface PolygonCollider2D.", this);
                    warned = true;
                }
                return;
            }
            processed.Clear();
            if (network != null) ProcessChildren(network.transform);
            if (decorativeNetwork != null) ProcessChildren(decorativeNetwork.transform);
            if (upgradeGrowth != null) ProcessChildren(upgradeGrowth.transform, true);
        }

        private void ProcessChildren(Transform root, bool onlyUpgradeLines = false)
        {
            foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
            {
                if (line == null || line.positionCount < 2) continue;
                if (!processed.Add(line)) continue;
                if (onlyUpgradeLines && !line.name.StartsWith("Fungal growth ") &&
                    !line.name.StartsWith("Fungal filament ")) continue;
                // Only actual mushroom stems/caps may rise above soil. The old fruiting
                // bodies are intentionally left alone, though normally disabled.
                if (line.name == "Stem" || line.name == "Cap" ||
                    HasAncestorNamed(line.transform, "Fungal fruiting body") ||
                    HasAncestorNamed(line.transform, "Mushroom cap & stem")) continue;
                ClampLine(line);
            }
        }

        private static bool HasAncestorNamed(Transform t, string name)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.name.StartsWith(name)) return true;
            return false;
        }

        private void ClampLine(LineRenderer line)
        {
            vertices.Clear();
            int count = line.positionCount;
            float width = Mathf.Max(0.01f, line.widthMultiplier);
            AnimationCurve curve = line.widthCurve;
            if (curve != null)
            {
                // Account for varying stroke width and any width-curve bumps.
                for (int k = 0; k <= 8; k++)
                    width = Mathf.Max(width, line.widthMultiplier * curve.Evaluate(k / 8f));
            }
            float step = Mathf.Max(0.04f, maxSegmentLength);
            Vector3 last = line.GetPosition(0);
            if (!line.useWorldSpace) last = line.transform.TransformPoint(last);
            vertices.Add(ClampUnderSoil(last, width));
            for (int i = 1; i < count; i++)
            {
                Vector3 next = line.GetPosition(i);
                if (!line.useWorldSpace) next = line.transform.TransformPoint(next);
                int subdivisions = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(last, next) / step), 1, 64);
                for (int j = 1; j <= subdivisions; j++)
                    vertices.Add(ClampUnderSoil(Vector3.Lerp(last, next, j / (float)subdivisions), width));
                last = next;
            }
            line.positionCount = vertices.Count;
            for (int i = 0; i < vertices.Count; i++)
                line.SetPosition(i, line.useWorldSpace ? vertices[i] : line.transform.InverseTransformPoint(vertices[i]));
        }
    }
}
