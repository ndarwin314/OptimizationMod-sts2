using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;
using Timer = Godot.Timer;

namespace testMod.testModCode.Optimization;

public class CloneOptimizer
{

  // Counter of how many cards we have rendered on CloneRestSiteOption, should be reset at end of call
  private static int _visualCounter = 0;
  // Limit for how many cards to render during Clone
  private const int CloneVisualLimit = 15;

  private static void PreviewHelper(
    IReadOnlyList<CardPileAddResult> results,
    float time = 1.2f)
  {
    const PileType pileType = PileType.Deck;
    Control control = NRun.Instance.GlobalUi.MessyCardPreviewContainer;
    var tween = control.CreateTween().SetParallel();
    foreach (var result in results)
    {
      var card = result.cardAdded;
      var modifyingModels = result.modifyingModels!;
      var relicsToFlash = (modifyingModels?.OfType<RelicModel>() ?? null)!;
      var node = NCard.Create(card)!;
      control.AddChildSafely(node);
      node.UpdateVisuals(pileType, CardPreviewMode.Normal);
      var source = new TaskCompletionSource();
      tween.TweenProperty(
        node, 
        (NodePath) "scale", 
        Vector2.One, 0.25)
        .From(Vector2.Zero).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
      tween.TweenCallback(
        Callable.From((Action) (() => TaskHelper.RunSafely(CardCmd.FlashRelics(node, relicsToFlash)))));
      tween.TweenCallback(Callable.From((Action) (() =>
      {
        NCardFlyVfx child= NCardFlyVfx.Create(node, card.Pile?.Type ?? pileType, true, card.Owner.Character.TrailPath)!;
        Node parent2 = NRun.Instance?.GlobalUi.TopBar.TrailContainer!;
        parent2.AddChildSafely(child);
        TaskHelper.RunSafely(child.SwooshAwayCompletion?.Task.ContinueWith(_ => source.SetResult())!);
      }))).SetDelay(time);
    }
  }
    [HarmonyPatch(typeof(CloneRestSiteOption), nameof(CloneRestSiteOption.OnSelect))]
    public class OnSelect
    {
        // LINQ sometimes doesn't like if i replace this with a lambda
        private static bool Filter(CardModel card) => card.Enchantment is Clone;
        
        
        private static async Task<bool> Helper(CloneRestSiteOption option)
        {
          // Use LINQ to apply CloneCard to all cards at once, rather than a foreach loop
          // Could potentially use parallel version but it seems fine as is
            var clonedCards = 
                option.Owner.Deck.Cards.
                    Where(Filter).
                    Select(option.Owner.RunState.CloneCard).ToList();

            var deck = option.Owner.Deck;
            // use modified version of Add method for List<CardModel> to avoid multiple calls
            var results = await CloneAdd(clonedCards, deck);
            // It seems like the cards don't get rendered without a call to this, I don't fully understand
            // how the animations work so idk
            //CardCmd.PreviewCardPileAdd(results.Take(CloneVisualLimit).ToList(), style: CardPreviewStyle.MessyLayout);
            PreviewHelper(results.Take(CloneVisualLimit).ToList());
            // reset counter
            _visualCounter = 0;
            return true;
        }

        [HarmonyPrefix]
        public static bool Prefix(CloneRestSiteOption __instance, ref Task<bool> __result)
        {
            __result = Helper(__instance);
            Thread.Sleep(500);
            return false;
        }
    }

    [HarmonyPatch(typeof(Hoarder), nameof(Hoarder.AfterCardChangedPiles))]
    public class HoarderFix
    {
      
      // Uses the static field to determine whether to render card moving piles
      private static async Task Helper(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy,
        Hoarder hoarder)
      {
        Hoarder clonedBy1 = hoarder;
        if (oldPileType != PileType.None)
          return;
        CardPile pile = card.Pile;
        if ((pile != null ? (pile.Type != PileType.Deck ? 1 : 0) : 1) != 0 || clonedBy != null || clonedBy1._cardsToSkip.Remove(card))
          return;
        for (int i = 0; i < 2; ++i)
        {
          CardModel card1 = card.Owner.RunState.CloneCard(card);
          clonedBy1._cardsToSkip.Add(card1);
          var skipVisuals = _visualCounter >= CloneVisualLimit;
          _visualCounter++;
          await CardPileCmd.Add(card1, PileType.Deck, clonedBy: clonedBy1, skipVisuals: skipVisuals);
        }
      }
      
      [HarmonyPrefix]
      public static bool Prefix(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy,
        Hoarder __instance, 
        ref Task __result)
      {
        __result = Helper(card, oldPileType, clonedBy, __instance);
        return false;
      }
    }

    [HarmonyPatch(typeof(BingBong), nameof(BingBong.AfterCardChangedPiles))]
    public class BingBongFix
    {
      // Uses the static field to determine whether to render card moving piles
      private static async Task Helper(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy,
        BingBong bingBong)
      {
        BingBong clonedBy1 = bingBong;
        CardPile pile = card.Pile;
        if ((pile != null ? pile.Type != PileType.Deck ? 1 : 0 : 1) != 0 || card.Owner != clonedBy1.Owner || clonedBy != null || clonedBy1.CardsToSkip.Remove(card))
          return;
        clonedBy1.Flash();
        CardModel card1 = clonedBy1.Owner.RunState.CloneCard(card);
        clonedBy1.CardsToSkip.Add(card1);
        var skipVisuals = _visualCounter >= CloneVisualLimit;
        _visualCounter++;
        await CardPileCmd.Add(card1, PileType.Deck, clonedBy: clonedBy1, skipVisuals: skipVisuals);
      }

      [HarmonyPrefix]
      public static bool Prefix(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy,
        BingBong __instance, 
        ref Task __result)
      {
        __result = Helper(card, oldPileType, clonedBy, __instance);
        return false;
      }
    }
    
    // Modified version of Add function that should only render first CloneVisualLimit cards
    public static async Task<IReadOnlyList<CardPileAddResult>> CloneAdd(
    IEnumerable<CardModel> cards,
    CardPile newPile,
    CardPilePosition position = CardPilePosition.Bottom,
    AbstractModel? clonedBy = null,
    bool isChangingOwners = false)
  {
    var cardModels = cards as CardModel[] ?? cards.ToArray();
    if (cardModels.Length == 0)
      return [];
    if (newPile.IsCombatPile && CombatManager.Instance.IsEnding)
      return cardModels.Select((Func<CardModel, CardPileAddResult>) (c => new CardPileAddResult
      {
        cardAdded = c,
        success = false
      })).ToList();
    
    List<CardPileAddResult> results = [];
    Player owningPlayer = null;
    foreach (var card in cardModels)
    {
      if (card.Owner == null)
        throw new InvalidOperationException(card.Id.Entry + " has no owner.");
      var creature = card.Owner.Creature;
      CardPileAddResult cardPileAddResult;
      if (card.HasBeenRemovedFromState || creature.IsDead || card.IsInCombat && creature.CombatState == null)
      {
        cardPileAddResult = new CardPileAddResult
        {
          success = false,
          cardAdded = card,
          oldPile = card.Pile,
          modifyingModels = null
        };
        results.Add(cardPileAddResult);
      }
      else
      {
        if (newPile.Type == PileType.Deck)
        {
          if (!card.Owner.RunState.ContainsCard(card))
          {
            if (card.Owner.RunState is NullRunState)
              throw new InvalidOperationException($"Tried to add card {card.Id.Entry} to deck for an owner with a NullRunState!");
            throw new InvalidOperationException(card.Id.Entry + " must be added to a RunState before adding it to your deck.");
          }
        }
        else if (card.IsInCombat && creature.CombatState != null && !creature.CombatState.ContainsCard(card))
          throw new InvalidOperationException(card.Id.Entry + " must be added to a CombatState before adding it to this pile.");
        if (card.UpgradePreviewType.IsPreview())
          throw new InvalidOperationException("A card preview cannot be added to a pile.");
        cardPileAddResult = new CardPileAddResult
        {
          success = true,
          cardAdded = card,
          oldPile = card.Pile,
          modifyingModels = null
        };
        results.Add(cardPileAddResult);
        owningPlayer ??= card.Owner;
        if (owningPlayer != card.Owner)
          throw new InvalidOperationException("Tried to add cards with different owners to the same pile!");
      }
    }
    bool owningPlayerIsLocal = LocalContext.IsMe(owningPlayer);
    if (newPile.Type == PileType.Deck)
    {
      for (int i = 0; i < results.Count; ++i)
      {
        var result = results[i];
        if (Hook.ShouldAddToDeck(owningPlayer.RunState, result.cardAdded, out var preventer))
        {
          var runState = owningPlayer.RunState;
          runState.CurrentMapPointHistoryEntry?.GetEntry(owningPlayer.NetId).CardsGained.Add(result.cardAdded.ToSerializable());
          result.cardAdded.FloorAddedToDeck = runState.TotalFloor;
        }
        else
        {
          await preventer.AfterAddToDeckPrevented(result.cardAdded);
          result.success = false;
          results[i] = result;
        }
      }
    }
    if (newPile.IsCombatPile && !CombatManager.Instance.IsInProgress || !results.Any(r => r.success))
      return results;
    List<NCard> cardNodes = [];
    List<CardModel> cardsWithoutNodesChangingPiles = [];
    for (int i = 0; i < results.Count; ++i)
    {
      bool skipVisuals = _visualCounter > CloneVisualLimit;
      _visualCounter++;
      CardPileAddResult cardPileAddResult = results[i];
      if (!cardPileAddResult.success) continue;
      
      NCard cardNode = null;
      CardPile oldPile = cardPileAddResult.oldPile;
      CardModel card = cardPileAddResult.cardAdded;
      CardPile targetPile = newPile;
      bool isFullHandAdd = targetPile.Type == PileType.Hand && targetPile.Cards.Count >= CardPile.MaxCardsInHand;
      if (isFullHandAdd)
        targetPile = CardPile.Get(PileType.Discard, card.Owner);
      int num1;
      if (!owningPlayerIsLocal && targetPile.Type != PileType.Play)
      {
        CardPile cardPile = oldPile;
        num1 = cardPile != null ? (cardPile.Type == PileType.Play ? 1 : 0) : 0;
      }
      else
        num1 = 1;
      if (TestMode.IsOff & num1 != 0 && !skipVisuals)
      {
        cardNode = NCard.FindOnTable(card);
        bool flag1 = cardNode == null && targetPile.Type.IsCombatPile() && (isFullHandAdd || oldPile != null || targetPile.Type == PileType.Hand);
        bool flag2 = cardNode == null;
        if (flag2)
        {
          bool flag3;
          if (oldPile != null)
          {
            switch (oldPile.Type)
            {
              case PileType.Draw:
              case PileType.Discard:
              case PileType.Exhaust:
              case PileType.Deck:
                flag3 = true;
                goto label_51;
            }
          }
          flag3 = false;
          label_51:
          flag2 = flag3;
        }
        bool flag4 = flag2;
        if (flag4)
        {
          bool flag5;
          switch (targetPile.Type)
          {
            case PileType.Draw:
            case PileType.Discard:
            case PileType.Deck:
              flag5 = true;
              break;
            default:
              flag5 = false;
              break;
          }
          flag4 = flag5;
        }
        if (flag4)
          cardsWithoutNodesChangingPiles.Add(card);
        else if (flag1)
          cardNode = CardPileCmd.CreateCardNodeAndUpdateVisuals(card, targetPile.Type, owningPlayerIsLocal);
        if (cardNode != null)
          cardNodes.Add(cardNode);
      }
      CardModel card1 = card;
      if (oldPile != null)
        card.RemoveFromCurrentPile(skipVisuals);
      else if (targetPile.Type == PileType.Deck)
      {
        List<AbstractModel> modifyingModels;
        CardModel deck = Hook.ModifyCardBeingAddedToDeck(card.Owner.RunState, card, out modifyingModels);
        card1 = deck;
        if (modifyingModels != null && modifyingModels.Count > 0)
        {
          cardPileAddResult.cardAdded = deck;
          cardPileAddResult.modifyingModels = modifyingModels;
          results[i] = cardPileAddResult;
        }
      }
      int num2;
      switch (position)
      {
        case CardPilePosition.Bottom:
          num2 = -1;
          break;
        case CardPilePosition.Top:
          num2 = 0;
          break;
        case CardPilePosition.Random:
          num2 = card.Owner.RunState.Rng.Shuffle.NextInt(targetPile.Cards.Count + 1);
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof (position), position, null);
      }
      int index = num2;
      targetPile.AddInternal(card1, index);
      if (oldPile == null && targetPile.IsCombatPile && !isChangingOwners)
        await Hook.AfterCardEnteredCombat(card.CombatState, card);
      if (isFullHandAdd & owningPlayerIsLocal)
        ThinkCmd.Play(new LocString("combat_messages", "HAND_FULL"), owningPlayer.Creature, 2.0);
      CardPile cardPile1 = oldPile;
      if ((cardPile1 != null ? (cardPile1.Type != PileType.Play ? 1 : 0) : 1) != 0 || newPile.Type == PileType.Hand || card.IsDupe)
        cardNode?.UpdateVisuals(targetPile.Type, CardPreviewMode.Normal);
    }
    Tween tween = null;
    if (cardNodes.Count != 0)
    {
      NPlayerHand handNode = NCombatRoom.Instance?.Ui.Hand;
      tween = NCombatRoom.Instance?.CreateTween().SetParallel();
      foreach (NCard ncard in cardNodes)
      {
        NCard cardNode = ncard;
        CardModel card = cardNode.Model;
        CardPile oldPile = results.Find((r => r.cardAdded == card)).oldPile;
        CardPileCmd.MoveCardNodeToNewPileBeforeTween(cardNode, card.Pile.Type);
        bool flag6 = !owningPlayerIsLocal;
        if (flag6)
        {
          bool flag7;
          switch (card.Pile.Type)
          {
            case PileType.Draw:
            case PileType.Hand:
            case PileType.Discard:
            case PileType.Deck:
              flag7 = true;
              break;
            default:
              flag7 = false;
              break;
          }
          flag6 = flag7;
        }
        if (flag6)
        {
          tween?.Parallel().TweenProperty(cardNode, (NodePath) nameof (position), (cardNode.Position + Vector2.Down * 25f), SaveManager.Instance.PrefsSave.FastMode == FastModeType.Fast ? 0.20000000298023224 : 0.30000001192092896);
          tween?.Parallel().TweenProperty(cardNode, (NodePath) "modulate", StsColors.exhaustGray, SaveManager.Instance.PrefsSave.FastMode == FastModeType.Fast ? 0.20000000298023224 : 0.30000001192092896);
          tween?.Chain().TweenCallback(Callable.From( cardNode.QueueFreeSafely));
        }
        else
        {
          switch (card.Pile.Type)
          {
            case PileType.Hand:
              CardPileCmd.AppendPileLerpTween(tween, cardNode, card.Pile.Type, oldPile);
              if (tween != null)
              {
                tween.Parallel().TweenCallback(Callable.From((Action) (() => handNode?.Add(cardNode))));
              }
              continue;
            case PileType.Exhaust:
              card.Pile.InvokeCardAddFinished();
              if (oldPile != null && oldPile.Type != PileType.Hand && oldPile.Type != PileType.Play)
              {
                CardPileCmd.AppendPileLerpTween(tween, cardNode, PileType.Play, oldPile);
                float num;
                switch (SaveManager.Instance.PrefsSave.FastMode)
                {
                  case FastModeType.Fast:
                    num = 0.2f;
                    break;
                  case FastModeType.Instant:
                    num = 0.01f;
                    break;
                  default:
                    num = 0.5f;
                    break;
                }
                float time = num;
                tween?.Chain().TweenInterval(time);
              }
              if (oldPile != null && oldPile.Type == PileType.Hand)
              {
                if (tween != null)
                {
                  tween.Chain().TweenCallback(Callable.From((Action) (() =>
                  {
                    NCardExhaustQuickVfx ncardExhaustQuickVfx = NCardExhaustQuickVfx.Create(cardNode);
                    if (ncardExhaustQuickVfx != null)
                    {
                      NDebugAudioManager.Instance?.Play("card_exhaust.mp3");
                      TaskHelper.RunSafely(ncardExhaustQuickVfx.PlayAnimation());
                    }
                    else
                      cardNode.QueueFreeSafely();
                  })));
                }
                continue;
              }
              if (tween != null)
              {
                tween.Chain().TweenCallback(Callable.From((Action) (() =>
                {
                  NCombatRoom instance = NCombatRoom.Instance;
                  NCardExhaustVfx child = instance != null ? NCardExhaustVfx.Create(cardNode) : null;
                  if (child != null)
                  {
                    instance.Ui.AddChildSafely((Node) child);
                    NDebugAudioManager.Instance?.Play("card_exhaust.mp3");
                    TaskHelper.RunSafely(child.PlayAnimation());
                  }
                  else
                    cardNode.QueueFreeSafely();
                })));
              }
              continue;
            case PileType.Play:
              CardPileCmd.AppendPlayPileLerpTween(tween, cardNode, oldPile);
              continue;
            default:
              if (tween != null)
              {
                tween.TweenCallback(Callable.From((Action) (() =>
                {
                  Node node = card.Pile.Type != PileType.Deck ? (Node) card.Owner.Creature.GetVfxContainer() : NRun.Instance.GlobalUi.TopBar.TrailContainer;
                  cardNode.Reparent(node);
                  NCardFlyVfx child = NCardFlyVfx.Create(cardNode, card.Pile.Type, true, card.Owner.Character.TrailPath);
                  if (node == null)
                    return;
                  node.AddChildSafely((Node) child);
                })));
              }
              continue;
          }
        }
      }
    }
    if (cardsWithoutNodesChangingPiles.Count != 0)
    {
      foreach (CardModel cardModel in cardsWithoutNodesChangingPiles)
      {
        CardModel card = cardModel;
        CardPile oldPile = results.Find(r => r.cardAdded == card).oldPile;
        Node vfxContainer = 
          card.Pile.Type != PileType.Deck ? 
            (Node) card.Owner.Creature.GetVfxContainer() : 
            NRun.Instance.GlobalUi.TopBar.TrailContainer;
        if (tween != null)
        {
          tween.TweenCallback(Callable.From((Action) (() =>
          {
            NCardFlyShuffleVfx child = NCardFlyShuffleVfx.Create(oldPile, card.Pile, card.Owner.Character.TrailPath);
            Node parent = vfxContainer;
            if (parent == null)
              return;
            parent.AddChildSafely((Node) child);
          })));
        }
        else
        {
          NCardFlyShuffleVfx child = NCardFlyShuffleVfx.Create(oldPile, card.Pile, card.Owner.Character.TrailPath);
          Node parent = vfxContainer;
          if (parent != null)
            parent.AddChildSafely((Node) child);
        }
      }
    }
    if (tween != null)
    {
      tween.Play();
      if (!await tween.AwaitFinished((Node) NCombatRoom.Instance))
        return results;
    }
    foreach (CardPileAddResult cardPileAddResult in results)
    {
      if (!cardPileAddResult.success) continue;
      CardModel cardAdded = cardPileAddResult.cardAdded;
      IRunState runState = cardAdded.Owner.RunState;
      ICombatState combatState = cardAdded.CombatState;
      CardModel card = cardAdded;
      CardPile oldPile = cardPileAddResult.oldPile;
      int type = oldPile != null ? (int) oldPile.Type : 0;
      AbstractModel clonedBy1 = clonedBy;
      await Hook.AfterCardChangedPiles(runState, combatState, card, (PileType) type, clonedBy1);
    }
    return results;
  }


}