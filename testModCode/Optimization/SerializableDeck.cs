using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace testMod.testModCode.Optimization;

[HarmonyPatch(typeof(SerializablePlayer))]
public class SerializableDeck
{
    public class CardTuple : IPacketSerializable
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
        
        [JsonPropertyName("card")]
        public SerializableCard Card { get; set; }
        
        public void Serialize(PacketWriter writer)
        {
            writer.WriteInt(Count);
            writer.Write(Card);
        }

        public void Deserialize(PacketReader reader)
        {
            Count = reader.ReadInt();
            Card = reader.Read<SerializableCard>();
        }
    }
    
    // TODO: i should probably make this a transpiler instead of a prefix
    //[HarmonyPatch(nameof(SerializablePlayer.Serialize))]
    //[HarmonyPrefix]
    public static bool SerializeDeck(PacketWriter writer, SerializablePlayer __instance)
    {
        writer.WriteULong(__instance.NetId);
        writer.WriteModelEntry(__instance.CharacterId);
        writer.WriteInt(__instance.CurrentHp);
        writer.WriteInt(__instance.MaxHp);
        writer.WriteInt(__instance.MaxEnergy, 16 /*0x10*/);
        writer.WriteInt(__instance.MaxPotionSlotCount, 8);
        writer.WriteInt(__instance.Gold);
        writer.WriteInt(__instance.BaseOrbSlotCount, 16 /*0x10*/);
        writer.WriteList( DeckHelper(__instance.Deck));
        writer.WriteList( __instance.Relics);
        writer.WriteList(__instance.Potions);
        writer.Write(__instance.Rng);
        writer.Write(__instance.Odds);
        writer.Write(__instance.RelicGrabBag);
        writer.Write(__instance.ExtraFields);
        writer.Write(__instance.UnlockState);
        writer.WriteFullModelIdList(__instance.DiscoveredCards);
        writer.WriteFullModelIdList( __instance.DiscoveredEnemies);
        writer.WriteInt(__instance.DiscoveredEpochs.Count);
        foreach (string discoveredEpoch in __instance.DiscoveredEpochs)
            writer.WriteEpochId(discoveredEpoch);
        writer.WriteFullModelIdList( __instance.DiscoveredPotions);
        writer.WriteFullModelIdList( __instance.DiscoveredRelics);
        return false;
    }
    
    //[HarmonyPatch(nameof(SerializablePlayer.Deserialize))]
    //[HarmonyPrefix]
    public bool Deserialize(PacketReader reader, SerializablePlayer __instance)
    {
        var deck = reader.ReadList<CardTuple>();
        
        __instance.NetId = reader.ReadULong();
        __instance.CharacterId = reader.ReadModelIdAssumingType<CharacterModel>();
        __instance.CurrentHp = reader.ReadInt();
        __instance.MaxHp = reader.ReadInt();
        __instance.MaxEnergy = reader.ReadInt(16 /*0x10*/);
        __instance.MaxPotionSlotCount = reader.ReadInt(8);
        __instance.Gold = reader.ReadInt();
        __instance.BaseOrbSlotCount = reader.ReadInt(16 /*0x10*/);
        __instance.Deck = DeckUnHelper(deck);
        __instance.Relics = reader.ReadList<SerializableRelic>();
        __instance.Potions = reader.ReadList<SerializablePotion>();
        __instance.Rng = reader.Read<SerializablePlayerRngSet>();
        __instance.Odds = reader.Read<SerializablePlayerOddsSet>();
        __instance.RelicGrabBag = reader.Read<SerializableRelicGrabBag>();
        __instance.ExtraFields = reader.Read<SerializableExtraPlayerFields>();
        __instance.UnlockState = reader.Read<SerializableUnlockState>();
        __instance.DiscoveredCards = reader.ReadFullModelIdList();
        __instance.DiscoveredEnemies = reader.ReadFullModelIdList();
        int num = reader.ReadInt();
        for (int index = 0; index < num; ++index)
            __instance.DiscoveredEpochs.Add(reader.ReadEpochId());
        __instance.DiscoveredPotions = reader.ReadFullModelIdList();
        __instance.DiscoveredRelics = reader.ReadFullModelIdList();
        return false;
    }

    
    // Takes list of cards and returns a list of tuples of SerializableCard and the number of copies of that same card
    private static List<CardTuple> DeckHelper(List<SerializableCard> cards)
    {
        Dictionary<SerializableCard, int> cardCounter = new();
        List<CardTuple> output = new();
        foreach (var card in cards)
        {
            cardCounter.TryGetValue(card, out int currentCount);
            cardCounter[card] = currentCount + 1;
        }

        foreach (var (key, value) in cardCounter)
        {
            output.Add(new CardTuple{Count =  value, Card = key});
        }
        return output;
    }
    
    private static List<SerializableCard> DeckUnHelper(List<CardTuple> cardCounter)
    {
        List<SerializableCard> output = new();

        foreach (var tuple in cardCounter)
        {
            for (int i=0; i < tuple.Count; ++i) 
                output.Add(tuple.Card);
        }
        return output;
    }
}