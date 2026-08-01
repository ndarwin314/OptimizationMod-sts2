using System.Text.Json.Serialization;

namespace testMod.testModCode.Save;

[JsonSerializable(typeof(SerializableRunCompressed))]
[JsonSerializable(typeof(SerializablePlayerCompressed))]
[JsonSerializable(typeof(SerializableCardTuple))]
public partial class HackSerializerContext : JsonSerializerContext
{

}