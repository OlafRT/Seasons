using System;
using System.Reflection;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Non-invasive resource transactions for the current jam prototype.
    /// TreeGameState's properties currently have private setters, so this adapter
    /// invokes those setters without replacing TreeGameState.cs or its .meta GUID.
    /// Later, move TryTrade into TreeGameState itself and delete this adapter.
    /// </summary>
    public static class TreeJamEcoResources
    {
        private static readonly PropertyInfo EnergyProperty = typeof(TreeGameState).GetProperty("Energy");
        private static readonly PropertyInfo NutrientProperty = typeof(TreeGameState).GetProperty("Nutrients");
        private static bool warned;

        public static bool TryTrade(TreeGameState state, float energyChange, float nutrientChange)
        {
            if (state == null || EnergyProperty == null || NutrientProperty == null) return false;
            float energy = state.Energy + energyChange;
            float nutrients = state.Nutrients + nutrientChange;
            if (energy < -0.0001f || nutrients < -0.0001f ||
                energy > state.EnergyCapacity + .0001f ||
                nutrients > state.NutrientCapacity + .0001f) return false;
            return SetBoth(state, Mathf.Clamp(energy, 0f, state.EnergyCapacity),
                           Mathf.Clamp(nutrients, 0f, state.NutrientCapacity));
        }

        public static bool GiveNutrients(TreeGameState state, float amount)
        {
            if (state == null || amount <= 0f) return false;
            return TryTrade(state, 0f, Mathf.Min(amount, state.NutrientCapacity - state.Nutrients));
        }

        private static bool SetBoth(TreeGameState state, float energy, float nutrients)
        {
            try
            {
                MethodInfo e = EnergyProperty.GetSetMethod(true);
                MethodInfo n = NutrientProperty.GetSetMethod(true);
                if (e == null || n == null) throw new InvalidOperationException("Resource setters unavailable");
                e.Invoke(state, new object[] { energy });
                n.Invoke(state, new object[] { nutrients });
                return true;
            }
            catch (Exception ex)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogError("Tree Jam Ecosystem: resource transaction failed. " + ex.Message +
                                   ". No exchange made. Please report this error.", state);
                }
                return false;
            }
        }
    }
}
