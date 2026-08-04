using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace OptimizationMod.OptimizationModCode.Save;

public class Serializable
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
    // logic to handle which format the card is saved in
    public static List<SerializableCard> ReadCardList(
        ref Utf8JsonReader reader,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        var deck = new List<SerializableCard>();
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);

        var json = doc.RootElement;
        foreach (var jsonElement in json.EnumerateArray())
        {
            int count = 1;
            SerializableCard card;
            JsonElement root = jsonElement;
            if (jsonElement.TryGetProperty("count", out var property))
            {
                count = property.GetInt32();
                root = jsonElement.GetProperty("card");
                
            }
            card = JsonSerializer.Deserialize<SerializableCard>(root, options);
            for (int i = 0; i < count; ++i)
            {
                deck.Add(card);
            }
        }
        return deck;
    }
    
    public static void WriteCollectionIfNotEmpty<T>(
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
    
    public static void WriteSerializable<T>(
        Utf8JsonWriter writer,
        string propertyName,
        T serializable,
        JsonSerializerOptions options)
    {
        writer.WritePropertyName(propertyName);
        JsonSerializer.Serialize(writer, serializable, options);
    }
    
    [HarmonyPatch(typeof(JsonSerializationUtility), nameof(JsonSerializationUtility.Options), MethodType.Getter)]
    public class Utility
    {

        [HarmonyPrefix]
        public static bool Options(ref JsonSerializerOptions __result)
        {
            var options = new JsonSerializerOptions(MegaCritSerializerContext.DefaultGeneratedSerializerOptions);
            options.Converters.Add(new CardListConverter());
            options.TypeInfoResolver =
                MegaCritSerializerContext.Default.WithAddedModifier(
                        JsonSerializationUtility.AlphabetizeProperties)
                    .WithAddedModifier(JsonSerializeConditionAttribute.CheckJsonSerializeConditionsModifier);
            __result = options;
            return false;
        }
    }

}