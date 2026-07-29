using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;


namespace testMod.testModCode.Optimization;

// this class gets its name from my reaction to it
// for some reason, having this patch, which does not change any behavior, prevents some null pointer exception
// if you can figure out why then please tell me, until then im just gonna leave this here
// something something tf2 load supporting coconut
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.CheckWinCondition))]
public class WTF
{
    
    public static async Task<bool> CheckWinCondition(CombatManager combatManager)
    {
        if (combatManager._pendingLoss != null)
        {
            combatManager.ProcessPendingLoss();
            return true;
        }
        if (!combatManager.IsEnding)
            return false;
        await combatManager.EndCombatInternal();
        return true;
    }

    [HarmonyPrefix]
    public static bool Prefix(CombatManager __instance, ref Task<bool> __result)
    {
        __result = CheckWinCondition(__instance);
        return false;
    }
}