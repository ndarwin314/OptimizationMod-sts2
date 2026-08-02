using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace OptimizationMod.OptimizationModCode.Save;

public class PlayerConverter: JsonConverter<SerializablePlayer>

{
    public static List<SerializableCardTuple> DeckHelper(IEnumerable<SerializableCard> cards)
    {
        Dictionary<SerializableCard, int> cardCounter = new();
        List<SerializableCardTuple> output = new();
        foreach (var card in cards)
        {
            cardCounter.TryGetValue(card, out int currentCount);
            cardCounter[card] = currentCount + 1;
        }

        foreach (var (key, value) in cardCounter)
        {
            output.Add(new SerializableCardTuple{Count =  value, Card = key});
        }
        return output;
    }
    
    public static List<SerializableCard> DeckUnHelper(List<SerializableCardTuple> cardCounter)
    {
        List<SerializableCard> output = new();

        foreach (var tuple in cardCounter)
        {
            for (int i=0; i < tuple.Count; ++i) 
                output.Add(tuple.Card);
        }
        return output;
    }
    
    private static void WriteCollectionIfNotEmpty<T>(
        Utf8JsonWriter writer,
        string propertyName,
        List<T>? collection,
        JsonSerializerOptions options)
    {
        if (collection is not { Count: > 0 })
            return;

        writer.WritePropertyName(propertyName);
        JsonSerializer.Serialize(writer, collection, options);
    }
    
    private static void WriteSerializable<T>(
        Utf8JsonWriter writer,
        string propertyName,
        T serializable,
        JsonSerializerOptions options)
    {
        writer.WritePropertyName(propertyName);
        JsonSerializer.Serialize(writer, serializable, options);
    }
    
    private static List<SerializableCard> ReadDeck(
        ref Utf8JsonReader reader,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        var deck = new List<SerializableCard>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;
  

            int count = 1;
            SerializableCard? card = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName: // reader for SerializableCardTuple
                    {
                        string property = reader.GetString()!;
                        reader.Read();

                        if (property == "count")
                        {
                            count = reader.GetInt32();
                        }
                        else //body of item, the card we are reading
                        {
                            using JsonDocument doc = JsonDocument.ParseValue(ref reader);

                            var json = doc.RootElement.GetRawText();
                            card = JsonSerializer.Deserialize<SerializableCard>(json, options);
                        }

                        break;
                    }
                    case JsonTokenType.StartObject: // fallback reader for SerializableCard
                        {using JsonDocument doc = JsonDocument.ParseValue(ref reader);

                        var json = doc.RootElement.GetRawText();
                        card = JsonSerializer.Deserialize<SerializableCard>(json, options);}
                        break;
                }
            }

            for (int i = 0; i < count; i++)
                deck.Add(card!);
        }

        return deck;
    }
    
    public override SerializablePlayer Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var player = new SerializablePlayer();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return player;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string property = reader.GetString()!;
            reader.Read();

            switch (property)
            {
                case "base_orb_slot_count":
                    player.BaseOrbSlotCount = reader.GetInt32();
                    break;

                case "character_id":
                    player.CharacterId =
                        JsonSerializer.Deserialize<ModelId>(ref reader, options);
                    break;

                case "current_hp":
                    player.CurrentHp = reader.GetInt32();
                    break;

                case "max_hp":
                    player.MaxHp = reader.GetInt32();
                    break;

                case "max_energy":
                    player.MaxEnergy = reader.GetInt32();
                    break;

                case "max_potion_slot_count":
                    player.MaxPotionSlotCount = reader.GetInt32();
                    break;

                case "gold":
                    player.Gold = reader.GetInt32();
                    break;

                case "net_id":
                    player.NetId = reader.GetUInt64();
                    break;

                case "deck":
                    player.Deck = ReadDeck(ref reader, options);
                    break;

                case "relics":
                    player.Relics =
                        JsonSerializer.Deserialize<List<SerializableRelic>>(ref reader, options)!;
                    break;

                case "potions":
                    player.Potions =
                        JsonSerializer.Deserialize<List<SerializablePotion>>(ref reader, options)!;
                    break;

                case "rng":
                    player.Rng =
                        JsonSerializer.Deserialize<SerializablePlayerRngSet>(ref reader, options)!;
                    break;

                case "odds":
                    player.Odds =
                        JsonSerializer.Deserialize<SerializablePlayerOddsSet>(ref reader, options)!;
                    break;

                case "relic_grab_bag":
                    player.RelicGrabBag =
                        JsonSerializer.Deserialize<SerializableRelicGrabBag>(ref reader, options)!;
                    break;

                case "extra_fields":
                    player.ExtraFields =
                        JsonSerializer.Deserialize<SerializableExtraPlayerFields>(ref reader, options)!;
                    break;

                case "unlock_state":
                    player.UnlockState =
                        JsonSerializer.Deserialize<SerializableUnlockState>(ref reader, options)!;
                    break;

                case "discovered_cards":
                    player.DiscoveredCards =
                        JsonSerializer.Deserialize<List<ModelId>>(ref reader, options)!;
                    break;

                case "discovered_enemies":
                    player.DiscoveredEnemies =
                        JsonSerializer.Deserialize<List<ModelId>>(ref reader, options)!;
                    break;

                case "discovered_epochs":
                    player.DiscoveredEpochs =
                        JsonSerializer.Deserialize<List<string>>(ref reader, options)!;
                    break;

                case "discovered_potions":
                    player.DiscoveredPotions =
                        JsonSerializer.Deserialize<List<ModelId>>(ref reader, options)!;
                    break;

                case "discovered_relics":
                    player.DiscoveredRelics =
                        JsonSerializer.Deserialize<List<ModelId>>(ref reader, options)!;
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException();
    }
    

    public override void Write(Utf8JsonWriter writer, SerializablePlayer value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("base_orb_slot_count", value.BaseOrbSlotCount);
        writer.WritePropertyName("character_id");
        JsonSerializer.Serialize(writer, value.CharacterId, options);
        writer.WriteNumber("current_hp", value.CurrentHp);
        
        var deck = DeckHelper(value.Deck);
        writer.WriteStartArray("deck");
        // everything else is default at the moment
        // write cards as {"count": count, card : <card_serialization>}
        foreach (var card in deck)
        {
            writer.WriteStartObject();
            writer.WriteNumber("count", card.Count);
            writer.WritePropertyName("card");
            JsonSerializer.Serialize(writer, card.Card, options);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        
        WriteSerializable(writer, "extra_fields", value.ExtraFields, options);
        writer.WriteNumber("gold", value.Gold);
        writer.WriteNumber("max_energy", value.MaxEnergy);
        writer.WriteNumber("max_hp", value.MaxHp);
        writer.WriteNumber("max_potion_slot_count", value.MaxPotionSlotCount);
        writer.WriteNumber("net_id", value.NetId);
        WriteSerializable(writer, "odds", value.Odds, options);
        WriteCollectionIfNotEmpty(writer, "potions", value.Potions, options);
        WriteSerializable(writer, "relic_grab_bag", value.RelicGrabBag, options);
        WriteCollectionIfNotEmpty(writer, "relics", value.Relics, options);
        WriteSerializable(writer, "rng", value.Rng, options);
        WriteSerializable(writer, "unlock_state", value.UnlockState, options);
        WriteCollectionIfNotEmpty(writer, "discovered_cards", value.DiscoveredCards, options);
        WriteCollectionIfNotEmpty(writer, "discovered_enemies", value.DiscoveredEnemies, options);
        WriteCollectionIfNotEmpty(writer, "discovered_potions", value.DiscoveredPotions, options);
        WriteCollectionIfNotEmpty(writer, "discovered_relics", value.DiscoveredRelics, options);
        writer.WriteEndObject();
    }
}