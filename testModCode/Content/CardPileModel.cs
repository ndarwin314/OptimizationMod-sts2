using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace testMod.testModCode.Content;

public abstract class CardPileModel: CardModel
{

    public CardPile? CurrentPile;
    
    public CardPileModel(
        int canonicalEnergyCost,
        CardType type,
        CardRarity rarity,
        TargetType targetType,
        bool shouldShowInCardLibrary = true) : 
        base(canonicalEnergyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
        CurrentPile = null;
    }
    
    public new CardPile? Pile => this.CurrentPile;
    
    
}