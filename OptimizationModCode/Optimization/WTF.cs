using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace OptimizationMod.OptimizationModCode.Optimization;

// this class gets its name from my reaction to it
// for some reason, having this patch, which does not change any behavior, prevents some null pointer exception
// my friend doombubbles says this is likely because Harmony prevents the error from propagating up
// and whatever the null pointer getting dereferenced isn't actually causing an issue
// something something tf2 load supporting coconut
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.EndCombatInternal), typeof(CombatTurnState))]
public class WTF
{
    
    [HarmonyPrefix]
    public static bool Prefix(CombatManager __instance)
    {
        return true;
    }
}