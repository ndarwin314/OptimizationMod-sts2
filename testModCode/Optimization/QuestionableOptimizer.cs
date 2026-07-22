using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace testMod.testModCode.Optimization;

[HarmonyPatch]
public class QuestionableOptimizer
{
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
    public static class CostModifier
    {
        
        // this is hacky but as far as i can tell, there are no card models in the base game that modify cost
        // so we can safely skip them
        [HarmonyPrefix]
        public static bool Helper(
            ICombatState combatState, 
            CardModel card, 
            Decimal originalCost, 
            ref Decimal __result)
        {
            if (combatState is not CombatState state)
                return true;
            if (originalCost < 0M)
                return false;
            Decimal modifiedCost = originalCost;
            foreach (var combatHookListener in CombatStateOptimizer.Helper(state, true))
                combatHookListener.TryModifyEnergyCostInCombat(card, modifiedCost, out modifiedCost);
            foreach (var combatHookListener in CombatStateOptimizer.Helper(state, true))
                combatHookListener.TryModifyEnergyCostInCombatLate(card, modifiedCost, out modifiedCost);
            __result = modifiedCost;
            return false;
        }
    }
}