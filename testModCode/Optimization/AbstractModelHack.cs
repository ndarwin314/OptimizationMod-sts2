using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace testMod.testModCode.Optimization;


// a more robust way of handling this would be to make a subclass of CardModel that overrides the AfterClone method
// to automatically add to this lookup i am using

// im 99% sure that at the time AfterCloned is called, the owner will be null if we are cloning from a canonical model
// so it kinda doesnt work the way i want, to be even lazier im just using a list that doesnt track what the owner of the card is
public class AbstractModelHack
{
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.AfterCloned))]
    public class AfterCloned
    {
        [HarmonyPostfix]
        public static void Helper(CardModel __instance)
        {
            var card = __instance;
            switch (card)
            {
                case Guilty:
                case Dowsing:
                case ByrdonisEgg:
                    IteratorOptimizer.CardModels.Add(card);
                    //var player = card.Owner;
                    //IteratorOptimizer.GetPlayerCards(player).Add(card);
                    break;
            }

        }
    }
}