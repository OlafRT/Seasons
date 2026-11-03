using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreeJam
{
    public class TreeGameState : MonoBehaviour
    {
        [Header("Starting resources")]
        [SerializeField, Min(0)] private float startingEnergy = 32;
        [SerializeField, Min(0)] private float startingNutrients = 18;
        [SerializeField, Min(1)] private float startingEnergyCapacity = 100;
        [SerializeField, Min(1)] private float startingNutrientCapacity = 100;

        [Header("Prototype production rates")]
        [SerializeField, Min(0)] private int startingLeaves = 5;
        [SerializeField, Min(0)] private float energyPerLeafPerSecond = 0.22f;
        [SerializeField, Min(0)] private float baseEnergyPerSecond = 0.6f;
        [SerializeField, Min(0)] private float baseNutrientsPerSecond = 0.5f;

        public float Energy { get; private set; }
        public float Nutrients { get; private set; }
        public float EnergyCapacity { get; private set; }
        public float NutrientCapacity { get; private set; }
        public int LeafCount { get; private set; }
        // All energy generation is photosynthesis in the current prototype.
        // Set by TreeJamDayNightCycle; defaults to full sunlight when no cycle is installed.
        public float SunlightFactor { get; private set; } = 1f;
        public float EnergyPerSecond => (baseEnergyPerSecond + LeafCount * energyPerLeafPerSecond + bonusEnergyPerSecond) * SunlightFactor;
        public float NutrientsPerSecond => baseNutrientsPerSecond + bonusNutrientsPerSecond;

        public event Action<UpgradeDefinition> UpgradePurchased;
        private readonly HashSet<UpgradeDefinition> purchased = new HashSet<UpgradeDefinition>();
        private float bonusEnergyPerSecond;
        private float bonusNutrientsPerSecond;

        private void Awake()
        {
            EnergyCapacity = startingEnergyCapacity;
            NutrientCapacity = startingNutrientCapacity;
            Energy = Mathf.Clamp(startingEnergy, 0, EnergyCapacity);
            Nutrients = Mathf.Clamp(startingNutrients, 0, NutrientCapacity);
            LeafCount = startingLeaves;
        }

        /// <summary>Set the amount of available daylight (0 = night, 1 = full sun).</summary>
        public void SetSunlightFactor(float amount)
        {
            SunlightFactor = Mathf.Clamp01(amount);
        }

        private void Update()
        {
            // Day/night modulates EnergyPerSecond; nutrient production is unchanged.
            Energy = Mathf.Min(EnergyCapacity, Energy + EnergyPerSecond * Time.deltaTime);
            Nutrients = Mathf.Min(NutrientCapacity, Nutrients + NutrientsPerSecond * Time.deltaTime);
        }

        public bool IsPurchased(UpgradeDefinition upgrade)
        {
            return upgrade != null && purchased.Contains(upgrade);
        }

        public bool IsUnlocked(UpgradeDefinition upgrade)
        {
            if (upgrade == null || IsPurchased(upgrade)) return false;
            if (IsExcludedByChoice(upgrade)) return false;
            if (upgrade.prerequisites == null) return true;

            foreach (UpgradeDefinition requirement in upgrade.prerequisites)
            {
                if (requirement != null && !IsPurchased(requirement)) return false;
            }
            return true;
        }

        /// <summary>True if a different upgrade in the same exclusiveGroup was purchased.</summary>
        public bool IsExcludedByChoice(UpgradeDefinition upgrade)
        {
            if (upgrade == null || string.IsNullOrEmpty(upgrade.exclusiveGroup)) return false;
            foreach (UpgradeDefinition selected in purchased)
                if (selected != upgrade && selected != null &&
                    !string.IsNullOrEmpty(selected.exclusiveGroup) &&
                    string.Equals(selected.exclusiveGroup, upgrade.exclusiveGroup,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        public bool CanAfford(UpgradeDefinition upgrade)
        {
            return upgrade != null && Energy >= upgrade.energyCost && Nutrients >= upgrade.nutrientCost;
        }

        public bool TryPurchase(UpgradeDefinition upgrade)
        {
            if (!IsUnlocked(upgrade)) return false;
            if (!CanAfford(upgrade))
            {
                TreeJamHUD.Notify("Not enough resources yet.");
                return false;
            }

            Energy -= upgrade.energyCost;
            Nutrients -= upgrade.nutrientCost;
            purchased.Add(upgrade);

            if (upgrade.effects != null)
            {
                foreach (UpgradeEffect effect in upgrade.effects)
                    Apply(effect);
            }

            TreeJamHUD.Notify("Grew: " + upgrade.displayName);
            UpgradePurchased?.Invoke(upgrade);
            return true;
        }

        private void Apply(UpgradeEffect effect)
        {
            switch (effect.type)
            {
                case UpgradeEffectType.AddLeaves:
                    LeafCount = Mathf.Max(0, LeafCount + Mathf.RoundToInt(effect.amount));
                    break;
                case UpgradeEffectType.AddEnergyPerSecond:
                    bonusEnergyPerSecond += effect.amount;
                    break;
                case UpgradeEffectType.AddNutrientsPerSecond:
                    bonusNutrientsPerSecond += effect.amount;
                    break;
                case UpgradeEffectType.AddEnergyCapacity:
                    EnergyCapacity = Mathf.Max(1, EnergyCapacity + effect.amount);
                    break;
                case UpgradeEffectType.AddNutrientCapacity:
                    NutrientCapacity = Mathf.Max(1, NutrientCapacity + effect.amount);
                    break;
            }
        }
    }
}
