using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace testMod.testModCode.Optimization;

public class NetOptimizer
{
    //[HarmonyPatch(typeof(NetCombatCardDb), nameof(NetCombatCardDb.GetCardId))]
    class ID
    {
        public static bool Prefix(CardModel card, NetCombatCardDb __instance)
        {
            if (__instance._cardToId.ContainsKey(card))
                return false;
            if (card.Owner == null)
            {
                Log.Error($"Tried to ID combat card {card} without an owner! __instance is not allowed");
            }
            else
            {
                Log.LogMessage(LogLevel.Debug, LogType.Network,
                    $"ID card {card} owned by {card.Owner.NetId} in some pile with id: {__instance._nextId}");
                __instance._cardToId[card] = __instance._nextId;
                __instance._idToCard[__instance._nextId] = card;
                ++__instance._nextId;
            }

            return false;
        }
    }
}