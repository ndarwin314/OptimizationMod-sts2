using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace OptimizationMod.OptimizationModCode.Save;

public class CardListConverter: JsonConverter<List<SerializableCard>>
{
    public override List<SerializableCard>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Serializable.ReadCardList(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, List<SerializableCard> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var deck = Serializable.DeckHelper(value);
        foreach (var card in deck)
        {
            writer.WriteStartObject();
            writer.WriteNumber("count", card.Count);
            writer.WritePropertyName("card");
            JsonSerializer.Serialize(writer, card.Card, options);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}