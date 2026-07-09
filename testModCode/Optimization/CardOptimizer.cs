using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace testMod.testModCode.Optimization;

public class CardOptimizer
{
    public static Dictionary<CardModel, CardPile?> CardPileMap = new (10000);

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.Pile), MethodType.Getter)]
    class CardPileGetter
    {
        [HarmonyPrefix]
        static bool Prefix(CardModel __instance, ref CardPile? __result)
        {
            var temp = CardPileMap.GetValueOrDefault(__instance);
            if (temp == null)
                CardPileMap[__instance] = null;
            
            __result = temp;
            
            return false;
        }
    }
    
    
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    static class AddInternal
    {
        [HarmonyPostfix]
        static void Postfix(CardModel card, CardPile __instance)
        {
            CardPileMap[card] = __instance;
        }
    }
    
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
    static class RemoveInternal
    {
        [HarmonyPostfix]
        static void Postfix(CardModel card, CardPile __instance)
        {
            CardPileMap[card] = null;
        }
    }

    [HarmonyPatch(typeof(CombatState), nameof(CombatState.AddCard), [typeof(CardModel)])]
    static class AddCard
    {
        [HarmonyPostfix]
        static void Postfix(CardModel card)
        {
            CardPileMap[card] = null;
        }
    }
    
    [HarmonyPatch(typeof(CombatState), nameof(CombatState.RemoveCard), [typeof(CardModel)])]
    static class RemoveCard
    {
        [HarmonyPostfix]
        static void Prefix(CardModel card)
        {
            CardPileMap.Remove(card);
        }
    }

    //[HarmonyPatch]
    static class ContainsPatch
    {
        static bool ContainsHandler(CardPile pile, CardModel card)
        {
            return CardPileMap.GetValueOrDefault(card) == pile;
        }
        
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.AddInternal));
            yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.RemoveInternal));
            yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.MoveToBottomInternal));
            yield return AccessTools.Method(typeof(CardPile), nameof(CardPile.MoveToTopInternal));
        }
        
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
        {
            // Without ILGenerator, the CodeMatcher will not be able to create labels
            var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);

            codeMatcher.MatchStartForward(
                    CodeMatch.Calls(() => default(CardPile).Cards.Contains<CardModel>(default))
                )
                .ThrowIfInvalid("Could not find call to CardPile.Cards.Contains")
                .RemoveInstruction()
                .InsertAndAdvance(
                    CodeInstruction.Call(() => ContainsHandler(default, default))
                );

            return codeMatcher.Instructions();
        }
    }
    
    
}