using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace OptimizationMod.OptimizationModCode.Save;

public class SerializableCardTuple : IPacketSerializable
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