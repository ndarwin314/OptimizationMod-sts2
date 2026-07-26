using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace testMod.testModCode.Fixes;

[HarmonyPatch(typeof(HardenedShellPower), nameof(HardenedShellPower.ModifyHpLostBeforeOstyLate))]
public class HardenedShellFix
{
    // should prevent Skulking colony from healing in the edge case where the boot causes it to take more than
    // HardenedShellPower.Amount on a turn
    [HarmonyPostfix]
    public static void Prefix(ref Decimal __result)
    {
        
        __result = Math.Max(0M, __result);
    }
}