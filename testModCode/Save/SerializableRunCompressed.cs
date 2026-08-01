// Decompiled with JetBrains decompiler
// Type: MegaCrit.Sts2.Core.Saves.SerializableRun
// Assembly: sts2, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 72F394CF-49A0-4C54-922A-543884CFAF1E
// Assembly location: /Users/ndarwin/RiderProjects/sts2Optimization/.godot/mono/temp/obj/ExportRelease/PublicizedAssemblies/sts2.1161FAC54FE8A557117C91C77FA4F15E/sts2.dll
// XML documentation location: /Users/ndarwin/RiderProjects/sts2Optimization/.godot/mono/temp/obj/ExportRelease/PublicizedAssemblies/sts2.1161FAC54FE8A557117C91C77FA4F15E/sts2.xml

#nullable enable
using System.Text.Json.Serialization;
using System.Linq;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.MapDrawing;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace testMod.testModCode.Save;

public class SerializableRunCompressed : ISaveSchema, IPacketSerializable
{
  /// <summary>The schema version of this save.</summary>
  [JsonPropertyName("schema_version")]
  public int SchemaVersion { get; set; }

  [JsonPropertyName("acts")]
  [JsonSerializeCondition(SerializationCondition.SaveIfNotCollectionEmptyOrNull)]
  public List<SerializableActModel> Acts { get; set; } = new();

  [JsonPropertyName("modifiers")]
  public List<SerializableModifier> Modifiers { get; set; } = new();

  /// <summary>
  /// This is null if the run is not a daily.
  /// Otherwise, it contains the date from the time server of the daily.
  /// </summary>
  [JsonPropertyName("dailyTime")]
  [JsonSerializeCondition(SerializationCondition.SaveIfNotTypeDefault)]
  public DateTimeOffset? DailyTime { get; set; }

  [JsonPropertyName("current_act_index")]
  public int CurrentActIndex { get; set; }

  [JsonPropertyName("events_seen")]
  [JsonSerializeCondition(SerializationCondition.SaveIfNotCollectionEmptyOrNull)]
  public List<ModelId> EventsSeen { get; set; } = new();

  [JsonPropertyName("pre_finished_room")]
  public SerializableRoom? PreFinishedRoom { get; set; }

  [JsonPropertyName("odds")]
  public SerializableRunOddsSet SerializableOdds { get; set; }

  [JsonPropertyName("shared_relic_grab_bag")]
  public SerializableRelicGrabBag SerializableSharedRelicGrabBag { get; set; }

  [JsonIgnore]
  private List<SerializablePlayer> Players { get; set; }
  
  [JsonPropertyName("players")]
  public List<SerializablePlayerCompressed> PlayersCompressed { get; set; }

  [JsonPropertyName("rng")]
  public SerializableRunRngSet SerializableRng { get; set; }

  /// <summary>The map coordinates you've visited in the current Act.</summary>
  [JsonPropertyName("visited_map_coords")]
  [JsonSerializeCondition(SerializationCondition.SaveIfNotCollectionEmptyOrNull)]
  public List<MapCoord> VisitedMapCoords { get; set; } = new ();

  [JsonPropertyName("map_point_history")]
  [JsonSerializeCondition(SerializationCondition.SaveIfNotCollectionEmptyOrNull)]
  public List<List<MapPointHistoryEntry>> MapPointHistory { get; set; } = new();

  /// <summary>When this save was created or last updated.</summary>
  [JsonPropertyName("save_time")]
  public long SaveTime { get; set; }

  [JsonPropertyName("start_time")]
  public long StartTime { get; set; }

  /// <summary>The amount of seconds that has elapsed for this run.</summary>
  [JsonPropertyName("run_time")]
  public long RunTime { get; set; }

  /// <summary>
  /// The exact moment when a Win was clocked on the RunTime. (Currently when you beat the Act 3 boss)
  /// </summary>
  [JsonPropertyName("win_time")]
  public long WinTime { get; set; }

  [JsonPropertyName("ascension")]
  public int Ascension { get; set; }

  [JsonPropertyName("num_reloads")]
  public int NumReloads { get; set; }

  [JsonPropertyName("platform_type")]
  public PlatformType PlatformType { get; set; }

  [JsonConverter(typeof (SerializableMapDrawingsJsonConverter))]
  [JsonPropertyName("map_drawings")]
  public SerializableMapDrawings? MapDrawings { get; set; }

  [JsonPropertyName("extra_fields")]
  public SerializableExtraRunFields ExtraFields { get; set; } = new ();

  [JsonPropertyName("game_mode")]
  public GameMode GameMode { get; set; }

  public void Serialize(PacketWriter writer)
  {
    // replace hard coded value
    writer.WriteInt(21);
    writer.WriteList(Acts);
    writer.WriteList(Modifiers);
    writer.WriteBool(DailyTime.HasValue);
    if (DailyTime.HasValue)
      writer.WriteLong(DailyTime.Value.ToUnixTimeSeconds());
    writer.WriteEnum(GameMode);
    writer.WriteInt(CurrentActIndex, 4);
    writer.WriteModelEntriesInList(EventsSeen);
    writer.WriteBool(PreFinishedRoom != null);
    if (PreFinishedRoom != null)
      writer.Write(PreFinishedRoom);
    writer.Write(SerializableOdds);
    writer.WriteList(PlayersCompressed);
    writer.Write(SerializableRng);
    writer.Write(SerializableSharedRelicGrabBag);
    writer.WriteList(VisitedMapCoords);
    writer.WriteInt(MapPointHistory.Count);
    foreach (var list in MapPointHistory)
      writer.WriteList(list);
    writer.WriteLong(SaveTime);
    writer.WriteLong(StartTime);
    writer.WriteLong(RunTime);
    writer.WriteLong(WinTime);
    writer.WriteInt(Ascension, 8);
    writer.WriteBool(MapDrawings != null);
    if (MapDrawings != null)
      writer.Write(MapDrawings);
    writer.Write(ExtraFields);
    writer.WriteInt(NumReloads);
  }

  public void Deserialize(PacketReader reader)
  {
    SchemaVersion = reader.ReadInt();
    Acts = reader.ReadList<SerializableActModel>();
    Modifiers = reader.ReadList<SerializableModifier>();
    if (reader.ReadBool())
      DailyTime = DateTimeOffset.FromUnixTimeSeconds(reader.ReadLong());
    GameMode = reader.ReadEnum<GameMode>();
    CurrentActIndex = reader.ReadInt(4);
    EventsSeen = reader.ReadModelIdListAssumingType<EventModel>();
    if (reader.ReadBool())
      PreFinishedRoom = reader.Read<SerializableRoom>();
    SerializableOdds = reader.Read<SerializableRunOddsSet>();
    var players = reader.ReadList<SerializablePlayerCompressed>();
    Players = players.Select(SerializablePlayerCompressed.ToSerializablePlayer).ToList();
    PlayersCompressed = players;
    SerializableRng = reader.Read<SerializableRunRngSet>();
    SerializableSharedRelicGrabBag = reader.Read<SerializableRelicGrabBag>();
    VisitedMapCoords = reader.ReadList<MapCoord>();
    int num = reader.ReadInt();
    for (int index = 0; index < num; ++index)
      MapPointHistory.Add(reader.ReadList<MapPointHistoryEntry>());
    SaveTime = reader.ReadLong();
    StartTime = reader.ReadLong();
    RunTime = reader.ReadLong();
    WinTime = reader.ReadLong();
    Ascension = reader.ReadInt(8);
    if (reader.ReadBool())
      MapDrawings = reader.Read<SerializableMapDrawings>();
    ExtraFields = reader.Read<SerializableExtraRunFields>();
    NumReloads = reader.ReadInt();
  }

  public SerializableRunCompressed Anonymized()
  {
    var players = 
      Players.Select((Func<SerializablePlayer, SerializablePlayer>)(p => p.Anonymized())).ToList();
    return new SerializableRunCompressed()
    {
      SchemaVersion = SchemaVersion,
      Acts = Acts,
      Modifiers = Modifiers,
      DailyTime = DailyTime,
      CurrentActIndex = CurrentActIndex,
      EventsSeen = EventsSeen,
      GameMode = GameMode,
      PreFinishedRoom = PreFinishedRoom,
      SerializableOdds = SerializableOdds,
      SerializableSharedRelicGrabBag = SerializableSharedRelicGrabBag,
      Players = players,
      PlayersCompressed = players.Select(SerializablePlayerCompressed.FromSerializablePlayer).ToList(),
      SerializableRng = SerializableRng,
      VisitedMapCoords = VisitedMapCoords,
      MapPointHistory =
        MapPointHistory.Select(
          (Func<List<MapPointHistoryEntry>, List<MapPointHistoryEntry>>) (l => 
            l.Select((Func<MapPointHistoryEntry, MapPointHistoryEntry>) (h => h.Anonymized())).ToList())).ToList(),
      SaveTime = SaveTime,
      StartTime = StartTime,
      RunTime = RunTime,
      WinTime = WinTime,
      Ascension = Ascension,
      PlatformType = PlatformType,
      MapDrawings = MapDrawings?.Anonymized(),
      ExtraFields = ExtraFields
    };
  }

  /// <summary>
  /// The furthest floor count reached during this run.
  /// Used when uploading the "floor" value when uploading to the daily leaderboards.
  /// </summary>
  [JsonIgnore]
  public int FloorReached
  {
    get
    {
      return MapPointHistory.Sum((Func<List<MapPointHistoryEntry>, int>) (c => c.Count));
    }
  }

  public static SerializableRunCompressed FromSerializableRun(SerializableRun serializableRun)
  {
    return new SerializableRunCompressed()
    {
      SchemaVersion = serializableRun.SchemaVersion+1,
      Acts = serializableRun.Acts,
      Modifiers = serializableRun.Modifiers,
      DailyTime = serializableRun.DailyTime,
      CurrentActIndex = serializableRun.CurrentActIndex,
      EventsSeen = serializableRun.EventsSeen,
      GameMode = serializableRun.GameMode,
      PreFinishedRoom = serializableRun.PreFinishedRoom,
      SerializableOdds = serializableRun.SerializableOdds,
      SerializableSharedRelicGrabBag = serializableRun.SerializableSharedRelicGrabBag,
      Players = serializableRun.Players,
      PlayersCompressed = serializableRun.Players.Select(SerializablePlayerCompressed.FromSerializablePlayer).ToList(),
      SerializableRng = serializableRun.SerializableRng,
      VisitedMapCoords = serializableRun.VisitedMapCoords,
      MapPointHistory = serializableRun.MapPointHistory,
      SaveTime = serializableRun.SaveTime,
      StartTime = serializableRun.StartTime,
      RunTime = serializableRun.RunTime,
      WinTime = serializableRun.WinTime,
      Ascension = serializableRun.Ascension,
      PlatformType = serializableRun.PlatformType,
      MapDrawings = serializableRun.MapDrawings?.Anonymized(),
      ExtraFields = serializableRun.ExtraFields
    };
  }
  
  public static SerializableRun ToSerializableRun(SerializableRunCompressed serializableRun)
  {
    return new SerializableRun()
    {
      SchemaVersion = serializableRun.SchemaVersion-1,
      Acts = serializableRun.Acts,
      Modifiers = serializableRun.Modifiers,
      DailyTime = serializableRun.DailyTime,
      CurrentActIndex = serializableRun.CurrentActIndex,
      EventsSeen = serializableRun.EventsSeen,
      GameMode = serializableRun.GameMode,
      PreFinishedRoom = serializableRun.PreFinishedRoom,
      SerializableOdds = serializableRun.SerializableOdds,
      SerializableSharedRelicGrabBag = serializableRun.SerializableSharedRelicGrabBag,
      Players = serializableRun.Players,
      SerializableRng = serializableRun.SerializableRng,
      VisitedMapCoords = serializableRun.VisitedMapCoords,
      MapPointHistory = serializableRun.MapPointHistory,
      SaveTime = serializableRun.SaveTime,
      StartTime = serializableRun.StartTime,
      RunTime = serializableRun.RunTime,
      WinTime = serializableRun.WinTime,
      Ascension = serializableRun.Ascension,
      PlatformType = serializableRun.PlatformType,
      MapDrawings = serializableRun.MapDrawings?.Anonymized(),
      ExtraFields = serializableRun.ExtraFields
    };
  }
}
