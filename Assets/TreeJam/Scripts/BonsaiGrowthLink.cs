using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Geometry instructions for one orb. When Grow To Orb is enabled, the orb's
    /// scene position is the actual tip of the branch/root. Move the orb to design
    /// the tree. Parent Orb makes the growth follow a purchased parent branch.
    /// </summary>
    [DisallowMultipleComponent]
    public class BonsaiGrowthLink : MonoBehaviour
    {
        public UpgradeDefinition upgrade;
        public bool isRoot;
        [Tooltip("Use the position of THIS orb as the exact growth endpoint, rather than End.")]
        public bool growToOrb = false; // preserve old links' behavior until explicitly upgraded
        [Tooltip("When set, grow FROM the branch unlocked by this orb (not an arbitrary nearest limb).")]
        public UpgradeOrb parentOrb;
        [Tooltip("Starting point in 'The Tree' local coordinates, used for independent starting branches.")]
        public Vector2 start;
        [Tooltip("Used by legacy orbs when Grow To Orb is off.")]
        public Vector2 end;
        [Range(-0.8f, 0.8f)] public float bend = .13f;
        [Min(.015f)] public float baseThickness = .12f;
        [Range(0, 3)] public int foliageClusters = 2;

        private void OnDrawGizmosSelected()
        {
            OrganicBonsaiTree tree = FindFirstObjectByType<OrganicBonsaiTree>();
            if (tree == null) return;
            Vector3 pointA = parentOrb != null ? parentOrb.transform.position :
                tree.transform.TransformPoint(new Vector3(start.x, start.y, 0));
            Vector3 pointD = growToOrb ? transform.position :
                tree.transform.TransformPoint(new Vector3(end.x, end.y, 0));
            Vector3 delta = pointD - pointA;
            Vector3 normal = new Vector3(-delta.y, delta.x, 0).normalized;
            Vector3 pointB = pointA + delta * .3f + normal * bend;
            Vector3 pointC = pointA + delta * .67f + normal * bend * .65f;
            Gizmos.color = isRoot ? new Color(.42f,.82f,1f) : new Color(.55f,1f,.45f);
            Vector3 previous = pointA;
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f, u = 1f - t;
                Vector3 p = u*u*u*pointA + 3*u*u*t*pointB + 3*u*t*t*pointC + t*t*t*pointD;
                Gizmos.DrawLine(previous, p);
                previous = p;
            }
            Gizmos.DrawWireSphere(pointD, .09f);
        }
    }
}
