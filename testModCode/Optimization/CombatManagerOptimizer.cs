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
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Combat;


namespace testMod.testModCode.Optimization;

[HarmonyPatch]
public static class CombatManagerOptimizer
{ 
  [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetupPlayerTurn))]
  class StartTurn
    {
      private static bool InnateHelper(CardModel card)
      {
        return card.LocalKeywords.Contains(CardKeyword.Innate);
      }
      
      // I remember I made some optimizations here but i cant remember what
      private static async Task Helper(Player player, HookPlayerChoiceContext playerChoiceContext, CombatManager __instance)
      {
        if (__instance._state == null || player.PlayerCombatState == null)
        {
          Log.Warn($"Combat state is null. Assuming that the run has been cleaned up. (CombatState: {__instance._state} PlayerCombatState: {player.PlayerCombatState})");
        }
        else
        {
          CombatState state = __instance._state;
          if (Hook.ShouldPlayerResetEnergy(state, player))
          {
            SfxCmd.Play("event:/sfx/ui/gain_energy");
            player.PlayerCombatState.ResetEnergy();
          }
          else
            player.PlayerCombatState.AddMaxEnergyToCurrent();
          await Hook.AfterEnergyReset(state, player);
          CancellationToken combatCt = __instance.CombatCt;
          combatCt.ThrowIfCancellationRequested();
          await Hook.BeforeHandDraw(state, player, playerChoiceContext);
          combatCt = __instance.CombatCt;
          combatCt.ThrowIfCancellationRequested();
          Decimal handDraw = Hook.ModifyHandDraw(state, player, 5M, out var modifiers);
          await Hook.AfterModifyingHandDraw(state, modifiers);
          combatCt = __instance.CombatCt;
          combatCt.ThrowIfCancellationRequested();
          if (player.PlayerCombatState.TurnNumber == 1)
          {
            CardPile pile = PileType.Draw.GetPile(player);
            var cardsBottom = pile.Cards.Where(c =>
            {
              EnchantmentModel? enchantment = c.Enchantment;
              return enchantment is { ShouldStartAtBottomOfDrawPile: true };
            }).ToList();
            
            foreach (CardModel card in cardsBottom)
              pile.MoveToBottomInternal(card);
            
            var cardsInnate = pile.Cards.Where(InnateHelper).Except(cardsBottom).ToList();
            
            foreach (CardModel card in cardsInnate)
              pile.MoveToTopInternal(card);

            handDraw = Math.Max(handDraw, cardsInnate.Count);
            handDraw = Math.Min(handDraw, CardPile.MaxCardsInHand);
          }
          await CardPileCmd.Draw(playerChoiceContext, handDraw, player, true);
          combatCt = __instance.CombatCt;
          combatCt.ThrowIfCancellationRequested();
          await Hook.AfterPlayerTurnStart(state, playerChoiceContext, player);
        }
      }
      
      [HarmonyPrefix]
      static bool Prefix(Player player, HookPlayerChoiceContext playerChoiceContext, CombatManager __instance, ref Task __result)
      {
        __result = Helper(player, playerChoiceContext, __instance);
        return false;
      }
    }
  
  [HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.AfterCombatEnd))]
  public class Clear
  {
    [HarmonyPrefix]
    public static bool Prefix(PlayerCombatState __instance)
    {
      CombatManager.Instance.StateTracker.Unsubscribe(__instance);
      foreach (var allPile in __instance.AllPiles)
      {
        allPile.Clear(true);
        CombatManager.Instance.StateTracker.Unsubscribe(allPile);
      }
      __instance._pets.Clear();
      return false;
    }
  }
  
}