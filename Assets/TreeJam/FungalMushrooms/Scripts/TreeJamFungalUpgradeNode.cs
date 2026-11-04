using UnityEngine;

namespace TreeJam
{
    public enum FungalUpgradeKind { RapidFruiting, CourierHyphae, BetterReturns, SporeColony, FruitingFans, ExpressHyphae, GenerousSymbiosis, WinterWeb }

    [RequireComponent(typeof(Collider2D))]
    public sealed class TreeJamFungalUpgradeNode : MonoBehaviour
    {
        public FungalUpgradeKind upgrade;
        public string displayName = "Fungal Growth";
        [TextArea(2,3)] public string description = "Improves your fungal network.";
        [Min(0f)] public float energyCost = 25f;
        [Min(0f)] public float nutrientCost = 15f;
        public bool hasPrerequisite;
        public FungalUpgradeKind prerequisite;
        [HideInInspector] public bool purchased;
        [HideInInspector] public bool visible;
    }
}
