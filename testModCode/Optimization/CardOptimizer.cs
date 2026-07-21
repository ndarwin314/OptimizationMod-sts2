using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

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
            CardPileMap[card] = null;
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

   
    
}