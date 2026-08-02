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
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace OptimizationMod.OptimizationModCode.Optimization;


// At a high level, both of these patches are doing the same 3 things. The first two should have no behavior impact
// the last does change behavior of the method but as far as I have seen it doesn't break any function using them
// 1. Avoiding constructing an additional List in memory, and simply yielding elements in order
// 2. Moving a .Contains check into the method body, allowing us to skip a switch statement over different model types
// 3. Optional skipping over CardModels since they don't seem to be used for any hooks and it is a considerable speedup
//
// In the future I might try to combine some of the logic of these two methods since there seems to be considerable
// overlap in what they do
[HarmonyPatch]
public static class IteratorOptimizer
{

  public static readonly Dictionary<Player, List<CardModel>> CardModelsHack = new();
  public static readonly List<CardModel> CardModels = new();

  public static List<CardModel> GetPlayerCards(Player player)
  {
    if (CardModelsHack.TryGetValue(player, out var list)) return list;
    list = [];
    CardModelsHack[player] = list;
    return list;
  }
  
  private static IEnumerable<AbstractModel> RunStateHelper(ICombatState? childCombatState, RunState runState, bool skipCard)
  {
    foreach (var player in runState.Players)
    {
      if (!player.IsActiveForHooks) continue;

      if (skipCard) continue;
      foreach (var card in player.Deck.Cards)
      {
        if (!card.HasBeenRemovedFromState)
          yield return card;
        if (card.Enchantment != null)
          yield return card.Enchantment;
      }
    }

    // hacky workaround, originally i was using the similarly named CardModelHack
    // however this requires more work to behave as intended, on clone the owner of a card is null
    if (skipCard)
    {
      foreach (var card in CardModels)
      {
        if (!card.HasBeenRemovedFromState)
          yield return card;
        if (card.Enchantment != null)
          yield return card.Enchantment;
      }
    }
    
    if (childCombatState == null)
    {
      foreach (var player in runState.Players.ToList())
      {
        if (!player.IsActiveForHooks) continue;
        
        foreach (var relicModel in player.Relics.ToList())
        {
          if (relicModel is { IsMelted: false, HasBeenRemovedFromState: false } )
            yield return relicModel;
        }
        
        foreach (var potionModel in player.PotionSlots.ToList())
        {
          if (potionModel is { HasBeenRemovedFromState: false })
            yield return potionModel;
        }
      }

      foreach (var modifier in runState.Modifiers)
        yield return modifier;
      
      foreach (var badgeModel in runState.BadgeModels)
        yield return badgeModel;

      yield return runState.MultiplayerScalingModel;
    }

    foreach (var runStateSubscriber in ModHelper.IterateAllRunStateSubscribers(runState))
      yield return runStateSubscriber;
    
    if (childCombatState == null) yield break;
    foreach (var iterateHookListener in childCombatState.IterateHookListeners())
      yield return iterateHookListener;
  }
  public static IEnumerable<AbstractModel> CombatStateHelper(CombatState __instance, bool skipCard)
  { 
      var combatState = __instance;
      for (int i = 0; i < combatState._allies.Count + combatState._enemies.Count; i++)
      { 
        Creature creature = 
          i < combatState._allies.Count ? 
            combatState._allies[i] : 
            combatState._enemies[i - combatState._allies.Count];
        Player? player = creature.Player;
        
        // powers can remove themselves which will throw an enumerator error, so we need to use ToList
        foreach (var power in creature.Powers.ToList().Where(power => player==null || player.IsActiveForHooks))
        {
          yield return power;
        }
        if (player == null) {
          if (creature.Monster != null)
            yield return creature.Monster;
        }
        else if (player.IsActiveForHooks)
        {
          foreach (var relicModel in player.Relics.ToList())
          {
            if (relicModel is { IsMelted: false, HasBeenRemovedFromState: false } )
              yield return relicModel;
          }
          
          foreach (var potionModel in player.PotionSlots.ToList())
          {
            if (potionModel is { HasBeenRemovedFromState: false })
              yield return potionModel;
          }

          if (player.PlayerCombatState == null) continue;

          foreach (var orb in player.PlayerCombatState.OrbQueue.Orbs.ToList())
          {
            if (orb is { HasBeenRemovedFromState: false })
              yield return orb;
          }

          if (skipCard) continue;
          foreach (var pile in player.PlayerCombatState.AllPiles)
          {
            foreach (var cardModel in pile.Cards)
            {
              if (!cardModel.HasBeenRemovedFromState)
                yield return cardModel;
              if (cardModel.Affliction != null)
                yield return cardModel.Affliction;
              if (cardModel.Enchantment != null)
                yield return cardModel.Enchantment;
            }
          }
        }
      }
      foreach (AbstractModel combatStateSubscriber in ModHelper.IterateAllCombatStateSubscribers(combatState))
        yield return combatStateSubscriber;
  }

    [HarmonyPatch(typeof(CombatState), nameof(CombatState.IterateHookListeners))]
    class IterateCombat
    {
        [HarmonyPostfix]
        static IEnumerable<AbstractModel> Postfix(IEnumerable<AbstractModel> hack, CombatState __instance)
        {
          return CombatStateHelper(__instance, true);
        }
        
        [HarmonyPrefix]
        static bool Prefix()
        {
          return false;
        }
        
    }
    
    [HarmonyPatch(typeof(RunState), nameof(RunState.IterateHookListeners))]
    class IterateRun
    {
      [HarmonyPostfix]
      static IEnumerable<AbstractModel> Postfix(
        IEnumerable<AbstractModel> hack, 
        ICombatState? childCombatState,
        RunState __instance)
      {
        return RunStateHelper(childCombatState, __instance, true);
      }
        
      [HarmonyPrefix]
      static bool Prefix()
      {
        return false;
      }
        
    }
    
}