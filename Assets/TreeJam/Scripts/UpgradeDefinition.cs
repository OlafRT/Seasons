using System;
using UnityEngine;

namespace TreeJam
{
    // More effects can be added as the seasonal and fungal systems are implemented.
    public enum UpgradeEffectType
    {
        AddLeaves,
        AddEnergyPerSecond,
        AddNutrientsPerSecond,
        AddEnergyCapacity,
        AddNutrientCapacity
    }

    [Serializable]
    public struct UpgradeEffect
    {
        public UpgradeEffectType type;
        public float amount;
    }

    [CreateAssetMenu(fileName = "Upgrade", menuName = "Tree Jam/Upgrade")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string displayName = "New Growth";
        [TextArea(2, 5)] public string description;
        [Min(0)] public float energyCost = 10;
        [Min(0)] public float nutrientCost = 5;
        public UpgradeDefinition[] prerequisites;
        [Tooltip("Optional: choosing one upgrade in this group permanently locks the others in this playthrough. Leave blank for normal non-exclusive upgrades.")]
        public string exclusiveGroup;
        public UpgradeEffect[] effects;
    }
}
