/*
 Copyright (C) 2026  Noa Feinberg

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.*/
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace testMod.testModCode.Optimization;

// Variety of changes to cards to try and improve performance. Primarily centered around creating a dictionary to track
// card locations rather than doing slow List.Contains calls
public class CardOptimizer
{
    // Dictionary to track all cards currently in a Pile
    private static readonly Dictionary<CardModel, CardPile?> CardPileMap = new (512);
    

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.Pile), MethodType.Getter)]
    class CardPileGetter
    {
        [HarmonyPrefix]
        static bool Prefix(CardModel __instance, ref CardPile? __result)
        {
            var temp = CardPileMap.GetValueOrDefault(__instance);
            
            __result = temp;
            
            return false;
        }
    }
    
    // Postfix to AddInternal that updates CardPileMap
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    static class AddInternal
    {
        [HarmonyPostfix]
        static void Postfix(CardModel card, CardPile __instance)
        {
            CardPileMap[card] = __instance;
        }
    }
    
    // Postfix to RemoveInternal that updates CardPileMap
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
    static class RemoveInternal
    {
        [HarmonyPostfix]
        static void Postfix(CardModel card, CardPile __instance)
        {
            CardPileMap.Remove(card);
        }
    }
    
    // Postfix to AddCard that updates CardPileMap
    [HarmonyPatch(typeof(CombatState), nameof(CombatState.AddCard), [typeof(CardModel)])]
    static class AddCard
    {
        [HarmonyPrefix]
        static void Prefix(CardModel card)
        {
            CardPileMap.TryAdd(card, null);
        }
    }
    
    // Postfix to RemoveCard that updates CardPileMap
    [HarmonyPatch(typeof(CombatState), nameof(CombatState.RemoveCard), [typeof(CardModel)])]
    static class RemoveCard
    {
        [HarmonyPostfix]
        static void Prefix(CardModel card)
        {
            CardPileMap.Remove(card);
        }
    }

    // Uses Dictionary lookup instead of List Lookup in each pile
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

    
    // Postfix to AddCard that updates CardPileMap
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
            {
                // prevents from overwriting value with null
                CardPileMap.TryAdd(card, null);
            }

            return false;
        }
    }
    
    // Postfix to RemoveCard that updates CardPileMap
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
        private static bool ContainsHandler(CardPile pile, CardModel card)
        {
            return CardPileMap.GetValueOrDefault(card) == pile;
        }
        
        static IEnumerable<MethodBase> TargetMethods()
        {
            // Methods that use slow self.Cards.Contains to check if the contain a card before adding ore removing
            // replaces this with my faster contains
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
                ThrowIfNotMatch("Failed to find match for self.Cards.Contains").
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

    
    // Clears CardPileMap upon exiting run to main menu to avoid memory leaks
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