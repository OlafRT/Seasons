using UnityEngine;

namespace TreeJam
{
    /// <summary>An individual fruiting body. Set its position, sprite and collider in the scene.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class TreeJamTradeMushroom : MonoBehaviour
    {
        public bool leftSide = true;
        [Tooltip("Left mushroom uses energy and returns nutrients; right uses nutrients for energy.")]
        public bool payEnergy = true;
        [Tooltip("Requires Spore Colony to appear.")]
        public bool extraColony;
        public float xOffsetFromTree = 2.75f;
        public Vector3 visualScale = Vector3.one;
        [HideInInspector] public float nextGrowthAt = -1f;
        [HideInInspector] public float growingSince = -1f;
        [HideInInspector] public bool established;
        [HideInInspector] public bool ripe;
        [HideInInspector] public Collider2D clickCollider;

        private void Awake() => clickCollider = GetComponent<Collider2D>();
    }
}
