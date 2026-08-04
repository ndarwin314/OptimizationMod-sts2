using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace OptimizationMod.OptimizationModCode.Save;

public class PlayerConverter: JsonConverter<SerializablePlayer>
{
    
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
                    player.Deck = Serializable.ReadCardList(ref reader, options);
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
        
        var deck = Serializable.DeckHelper(value.Deck);
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
        
        Serializable.WriteSerializable(writer, "extra_fields", value.ExtraFields, options);
        writer.WriteNumber("gold", value.Gold);
        writer.WriteNumber("max_energy", value.MaxEnergy);
        writer.WriteNumber("max_hp", value.MaxHp);
        writer.WriteNumber("max_potion_slot_count", value.MaxPotionSlotCount);
        writer.WriteNumber("net_id", value.NetId);
        Serializable. WriteSerializable(writer, "odds", value.Odds, options);
        Serializable.WriteCollectionIfNotEmpty(writer, "potions", value.Potions, options);
        Serializable.WriteSerializable(writer, "relic_grab_bag", value.RelicGrabBag, options);
        Serializable.WriteCollectionIfNotEmpty(writer, "relics", value.Relics, options);
        Serializable.WriteSerializable(writer, "rng", value.Rng, options);
        Serializable.WriteSerializable(writer, "unlock_state", value.UnlockState, options);
        Serializable.WriteCollectionIfNotEmpty(writer, "discovered_cards", value.DiscoveredCards, options);
        Serializable.WriteCollectionIfNotEmpty(writer, "discovered_enemies", value.DiscoveredEnemies, options);
        Serializable.WriteCollectionIfNotEmpty(writer, "discovered_potions", value.DiscoveredPotions, options);
        Serializable.WriteCollectionIfNotEmpty(writer, "discovered_relics", value.DiscoveredRelics, options);
        writer.WriteEndObject();
    }
}