using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;


namespace testMod.testModCode.Optimization;

[HarmonyPatch]
public static class CombatStateOptimizer
{

    [HarmonyPatch(typeof(CombatState), nameof(CombatState.IterateHookListeners))]
    class Iterate
    {
        [HarmonyPostfix]
        static IEnumerable<AbstractModel> Postfix(IEnumerable<AbstractModel> hack, CombatState __instance)
        { 
            var combatState = __instance;
            for (int i = 0; i < combatState._allies.Count + combatState._enemies.Count; i++)
            { 
              Creature creature = i < combatState._allies.Count ? combatState._allies[i] : combatState._enemies[i - combatState._allies.Count];
              Player? player = creature.Player;
              if (player == null) {
                if (creature.Monster != null)
                  yield return creature.Monster;
              }
              else if (player.IsActiveForHooks)
              {
                foreach (var relicModel in player.Relics)
                {
                  if (relicModel is { IsMelted: false, HasBeenRemovedFromState: false } )
                    yield return relicModel;
                }
                
                foreach (var potionModel in player.PotionSlots)
                {
                  if (potionModel is { HasBeenRemovedFromState: false })
                    yield return potionModel;
                }

                if (player.PlayerCombatState == null) continue;

                foreach (var orb in player.PlayerCombatState.OrbQueue.Orbs)
                {
                  if (orb is { HasBeenRemovedFromState: false })
                    yield return orb;
                }
                
                foreach (var pile in player.PlayerCombatState.AllPiles)
                {
                  foreach (var cardModel in pile.Cards)
                  {
                    if (cardModel.HasBeenRemovedFromState)
                      yield return cardModel;
                    if (cardModel.Affliction != null)
                      yield return cardModel.Affliction;
                    if (cardModel.Enchantment != null)
                      yield return  cardModel.Enchantment;
                  }
                }
              }
            }
            foreach (AbstractModel combatStateSubscriber in ModHelper.IterateAllCombatStateSubscribers(combatState))
              yield return combatStateSubscriber;
        }
        
        [HarmonyPrefix]
        static bool Prefix()
        {
          return false;
        }
        
    }
    
  
    
}