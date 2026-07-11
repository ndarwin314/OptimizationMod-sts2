using System.Reflection;
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

    /*[HarmonyPatch]
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
    
    
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions , ILGenerator generator)
        {
            // Without ILGenerator, the CodeMatcher will not be able to create labels
            var codeMatcher = new CodeMatcher(instructions , ILGenerator generator);

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
    }*/

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

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddDuringManualCardPlay))]
   static class CmdTest
    {
        
        public static async Task Helper(CardModel card)
        {
            CardPile oldPile;
            if (CombatManager.Instance.IsOverOrEnding)
            {
                oldPile = (CardPile) null;
            }
            else
            {
                ICombatState combatState1 = card.Owner.Creature.CombatState;
                bool owningPlayerIsLocal = combatState1 != null && combatState1.ContainsCard(card) ? LocalContext.IsMe(card.Owner) : throw new InvalidOperationException(card.Id.Entry + " must be added to a CombatState before playing it.");
                oldPile = card.Pile;
                NCard cardNode = (NCard) null;
                if (TestMode.IsOff)
                    cardNode = NCard.FindOnTable(card) ?? CardPileCmd.CreateCardNodeAndUpdateVisuals(card, PileType.Play, owningPlayerIsLocal);
                card.RemoveFromCurrentPile();
                PileType.Play.GetPile(card.Owner).AddInternal(card);
                if (cardNode != null)
                {
                    CardPileCmd.MoveCardNodeToNewPileBeforeTween(cardNode, PileType.Play);
                    Tween tween = NCombatRoom.Instance.CreateTween().SetParallel();
                    CardPileCmd.AppendPlayPileLerpTween(tween, cardNode, oldPile);
                    cardNode.PlayPileTween = tween;
                    tween.Play();
                    if (card.Type == CardType.Power)
                    {
                        if (!await tween.AwaitFinished((Godot.Node) NCombatRoom.Instance))
                        {
                            oldPile = (CardPile) null;
                            return;
                        }
                    }
                }
                IRunState runState = card.Owner.RunState;
                ICombatState combatState2 = card.CombatState;
                CardModel card1 = card;
                CardPile cardPile = oldPile;
                int type = cardPile != null ? (int) cardPile.Type : 0;
                await Hook.AfterCardChangedPiles(runState, combatState2, card1, (PileType) type, (AbstractModel) null);
                oldPile = (CardPile) null;
            }
        }
        [HarmonyPrefix]
        static bool Prefix(CardModel card, ref Task __result)
        {
            __result = Helper(card);
            return false;
        }
    }
}