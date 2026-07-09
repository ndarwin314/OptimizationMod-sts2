using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Random;


namespace testMod.testModCode.Optimization;

static class PlayerOptimizer
{
    [HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
    class Populate
    {
        [HarmonyPrefix]
        static bool Prefix(Rng rng, CombatState state, Player __instance)
        {
            foreach (var mutableCard in __instance.Deck.Cards.ToList())
            {
                // bypass some slow checks in CombatState.CloneCard to determine what pile the original card was in
                // specifically, donwstream it find the pile of a card which takes linear time in the number of cards
                CardModel card = (CardModel) mutableCard.ClonePreservingMutability();
                state._allCards.Add(card);
                card.DeckVersion = mutableCard;
                var pile = __instance.PlayerCombatState.DrawPile;
                pile._cards.Add(card);
                pile.InvokeContentsChanged();
            }
            __instance.PlayerCombatState.DrawPile.RandomizeOrderInternal(__instance, rng, state);
            return false;
        }
    }
}