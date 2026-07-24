using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace testMod.testModCode.Optimization;

public class CardOptimizer
{
    public static readonly Dictionary<CardModel, CardPile?> CardPileMap = new (512);
    

    
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.Pile), MethodType.Getter)]
    class CardPileGetter
    {
        [HarmonyPrefix]
        static bool Prefix(CardModel __instance, ref CardPile? __result)
        {
            var temp = CardPileMap.GetValueOrDefault(__instance);
            if (temp == null)
                return true;
            
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
            CardPileMap.Remove(card);
        }
    }

    [HarmonyPatch(typeof(CombatState), nameof(CombatState.AddCard), [typeof(CardModel)])]
    static class AddCard
    {
        [HarmonyPrefix]
        static void Prefix(CardModel card)
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

    [HarmonyPatch(typeof(RunState), nameof(RunState.ContainsCard))]
    static class RunStateContains
    {
        [HarmonyPrefix]
        static bool Prefix(CardModel card, ref bool __result)
        {
            __result = CardPileMap.ContainsKey(card);
            return false;
        }
    }

    [HarmonyPatch(typeof(RunState), nameof(RunState.AddCard), [typeof(CardModel)])]
    static class AddCardRun
    {
        [HarmonyPrefix]
        static bool Prefix(CardModel card, RunState __instance)
        {
            card.AssertMutable();
            if (card.HasBeenRemovedFromState)
            {
                if (!__instance.ContainsCard(card))
                    throw new InvalidOperationException($"Tried to add card {card} to RunState that has HasBeenRemovedFromState set as true, but it does not belong to this state!");
                card.HasBeenRemovedFromState = false;
            }
            else
                CardPileMap[card] = null;

            return false;
        }
    }
    
    [HarmonyPatch(typeof(RunState), nameof(RunState.RemoveCard))]
    static class RemoveCardRun
    {
        [HarmonyPrefix]
        static bool Prefix(CardModel card)
        {
            CardPileMap.Remove(card);
            card.Owner = null!;
            return false;
        }
    }

    [HarmonyPatch]
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
    
    
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
            MethodInfo getCards = AccessTools.PropertyGetter(typeof(CardPile), nameof(CardPile.Cards));
            MethodInfo contains = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m =>
                    m.Name == nameof(Enumerable.Contains) &&
                    m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(CardModel));

            MethodInfo replacement = AccessTools.Method(typeof(ContainsPatch), nameof(ContainsHandler));

            codeMatcher.MatchStartForward(
                new CodeMatch(OpCodes.Call, getCards),
                new CodeMatch(OpCodes.Ldarg_1),
                new CodeMatch(OpCodes.Call, contains)
                ).
                ThrowIfNotMatch("noa you fucking suck").
                RemoveInstructions(3).
                Insert(
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Call, replacement)
                    );

            return codeMatcher.Instructions();
        } 
    }

    [HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
    public static class Test
    {
        //[HarmonyPrefix]
        public static bool PopulateCombatState(Rng rng, CombatState state, Player __instance)
        {
            foreach (CardModel mutableCard in __instance.Deck.Cards.ToList())
            {
                CardModel card = state.CloneCard(mutableCard);
                card.DeckVersion = mutableCard;
                __instance.PlayerCombatState.DrawPile.AddInternal(card);
            }
            __instance.PlayerCombatState.DrawPile.RandomizeOrderInternal(__instance, rng, state);
            return false;
        }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    static class Cleanup
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            CardPileMap.Clear();
        }
        
    }
    
}