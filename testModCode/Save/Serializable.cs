using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace testMod.testModCode.Save;

public class Serializable
{
    
    private static int ReadIntNoAdvance(PacketReader reader, int bits = 32 /*0x20*/)
    {
        Array.Clear(reader._tempBuffer);
        BitSerializationUtil.ReadBits(reader.Buffer, reader.BitPosition, reader._tempBuffer, bits);
        return BinaryPrimitives.ReadInt32LittleEndian(reader._tempBuffer.AsSpan());
    }
    

    //[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])]
    public class Save
    {
        public static async Task SaveRun(SerializableRun save, bool isMultiplayer, RunSaveManager __instance)
        {
            string savePath = isMultiplayer ? __instance.CurrentMultiplayerRunSavePath : __instance.CurrentRunSavePath;
            MemoryStream stream = new MemoryStream();
            var compressed = SerializableRunCompressed.FromSerializableRun(save);
            try
            {
                if (!__instance._forceSynchronous)
                {
                    await JsonSerializer.SerializeAsync(stream, compressed, JsonSerializationUtility.GetTypeInfo<SerializableRunCompressed>());
                    stream.Seek(0L, SeekOrigin.Begin);
                    await __instance._saveStore.WriteFileAsync(savePath, stream.ToArray());
                }
                else
                {
                    JsonSerializer.Serialize(stream, compressed, JsonSerializationUtility.GetTypeInfo<SerializableRunCompressed>());
                    stream.Seek(0L, SeekOrigin.Begin);
                    __instance._saveStore.WriteFile(savePath, stream.ToArray());
                    // I haven't figured out a workaround to call the event from here so I will see if it breaks stuff
                    // in an obvious way
                }

            }
            finally
            {
                stream.Dispose();
            }
        }

        [HarmonyPrefix]
        public static bool Helper(SerializableRun save, bool isMultiplayer, RunSaveManager __instance, ref Task __result)
        {
            __result = SaveRun(save, isMultiplayer, __instance);
            return false;
        }
    }
    
    [HarmonyPatch(typeof(MigrationManager), nameof(MigrationManager.LoadWithAggressiveRecovery))]
    public class Load
    {
      private static ReadSaveResult<T> SchemaNewerThanCurrent<T>(
        int schemaVersion, 
        int currentVersion,
        int supportedVersion,
        string filePath,
        MigrationManager manager,
        MigratingData migratingData) where T : ISaveSchema, new()
      {
        Log.Warn($"Save version {schemaVersion} is newer than current {currentVersion}, attempting recovery...");
        var data = manager.RecoverPartialDataFromCorruptSave<T>(migratingData);
        if (data != null)
        {
          Log.Info($"Successfully recovered data from future save version {schemaVersion}");
          return new ReadSaveResult<T>(data, ReadSaveStatus.RecoveredWithDataLoss, $"Data recovered from future version {schemaVersion} but newer fields were discarded");
        }
        string errorMessage = $"Save file version {schemaVersion} is newer than current version {currentVersion}";
        Log.Error($"{errorMessage}: {filePath}");
        manager.PreserveCorruptFile(filePath, ReadSaveStatus.FutureVersion);
        return new ReadSaveResult<T>(ReadSaveStatus.FutureVersion, errorMessage);
      }
      
      private static ReadSaveResult<T> SchemaOlderThanSupported<T>(
        int schemaVersion, 
        int currentVersion,
        int supportedVersion,
        string filePath,
        MigrationManager manager,
        MigratingData migratingData) where T : ISaveSchema, new()
      {
        Log.Warn($"Save version {schemaVersion} is below minimum {supportedVersion}, attempting data scavenging...");
        var data = manager.RecoverPartialDataFromCorruptSave<T>(migratingData);
        if (data != null)
        {
          Log.Info($"Successfully scavenged data from old save version {schemaVersion} (recovery data not persisted)");
          return new ReadSaveResult<T>(data, ReadSaveStatus.RecoveredWithDataLoss, $"Data recovered from version {schemaVersion} but some information may be lost");
        }
        string errorMessage = $"Save file version {schemaVersion} is too old and couldn't be scavenged";
        Log.Error($"{errorMessage}: {filePath}");
        manager.PreserveCorruptFile(filePath, ReadSaveStatus.VersionTooOld);
        return new ReadSaveResult<T>(ReadSaveStatus.VersionTooOld, errorMessage);
      }
      
      private static ReadSaveResult<T> SchemaOlderThanCurrent<T>(
        int schemaVersion, 
        int currentVersion,
        int supportedVersion,
        string filePath,
        MigrationManager manager,
        MigratingData migratingData) where T : ISaveSchema, new()
      {
        T? data;
        try
        {
          data = manager.MigrateDataSequentially<T>(migratingData).ToObject<T>();
          Log.Info($"Successfully migrated {typeof (T).Name} from v{schemaVersion} to v{data.SchemaVersion} (migration not persisted)");
          return new ReadSaveResult<T>(data, ReadSaveStatus.MigrationRequired, $"Save was migrated from version {schemaVersion} to {data.SchemaVersion}");
        }
        catch (Exception ex)
        {
          Log.Error($"Migration failed for {filePath} with exception: {ex}");
          data = manager.RecoverPartialDataFromCorruptSave<T>(migratingData);
          if (data != null)
          {
            Log.Info("Migration failed but data scavenging succeeded");
            return new ReadSaveResult<T>(data, ReadSaveStatus.RecoveredWithDataLoss, $"Migration failed, recovered partial data from version {schemaVersion}");
          }
          manager.PreserveCorruptFile(filePath, ReadSaveStatus.MigrationFailed);
          return new ReadSaveResult<T>(ReadSaveStatus.MigrationFailed, ex.Message);
        }
      }
      
      //[HarmonyPrefix]
      public static ReadSaveResult<T> LoadWithAggressiveRecovery<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(
          MigrationManager __instance,
          string filePath,
          string content)
          where T : ISaveSchema, new()
      {
        try
        {
          using var document = JsonDocument.Parse(content);
          var migratingData = new MigratingData(document);
          var schemaMissing = false;
          int schemaVersion;
          try
          {
            schemaVersion = MigrationManager.ExtractSchemaVersion(migratingData);
          }
          catch (MissingSchemaVersionException ex)
          {
            Log.Warn($"Missing schema version in {filePath}, attempting to infer...");
            schemaMissing = true;
            schemaVersion = __instance.InferSchemaVersionFromStructure<T>(migratingData).GetValueOrDefault();
            migratingData.Set("schema_version", schemaVersion);
          }
          
          // hacky code to check if the save is in my weird save format that I haven't figured out how to integrate
          // properly with the versioning stuff
          if (schemaVersion == 99 && typeof(T)==typeof(SerializableRun))
          {
            var hack = JsonSerializationUtility.FromJson<SerializableRunCompressed>(content);

            if (hack is { Success: true, SaveData: not null })
            {
              return new ReadSaveResult<SerializableRun>(SerializableRunCompressed.ToSerializableRun(hack.SaveData)) as ReadSaveResult<T>;
 
            }
          }
          
          int currentVersion = __instance.GetCurrentVersion<T>();
          int supportedVersion = __instance.GetMinimumSupportedVersion<T>();
          
          if (schemaVersion > currentVersion) 
            return SchemaNewerThanCurrent<T>(schemaVersion, currentVersion, supportedVersion, filePath, __instance, migratingData);

          if (schemaVersion < supportedVersion)
            return SchemaOlderThanSupported<T>(schemaVersion, currentVersion, supportedVersion, filePath, __instance, migratingData);
          
          if (schemaVersion < currentVersion)
            return SchemaOlderThanCurrent<T>(schemaVersion, currentVersion, supportedVersion, filePath, __instance, migratingData);
          
          var readSaveResult = JsonSerializationUtility.FromJson<T>(content);
          if (readSaveResult.Success && readSaveResult.SaveData != null)
            return readSaveResult;
          
          
          Log.Error($"Failed to deserialize {filePath}: {readSaveResult.ErrorMessage}");
          var data = __instance.RecoverPartialDataFromCorruptSave<T>(migratingData);
          if (data != null)
          {
            Log.Info("Deserialization failed but data scavenging succeeded");
            return new ReadSaveResult<T>(data, ReadSaveStatus.RecoveredWithDataLoss, "Save file was corrupt but partial data was recovered");
          }
          if (schemaMissing)
          {
            __instance.PreserveCorruptFile(filePath, ReadSaveStatus.MissingSchemaVersion);
            return new ReadSaveResult<T>(ReadSaveStatus.MissingSchemaVersion, "Save file is missing schema version and cannot be deserialized");
          }
          if (MigrationManager.ShouldPreserveCorrupt(readSaveResult.Status))
            __instance.PreserveCorruptFile(filePath, readSaveResult.Status);
          return new ReadSaveResult<T>(readSaveResult.Status, readSaveResult.ErrorMessage);
          
        }
        catch (JsonException ex)
        {
          var str = ex.Path ?? "unknown";
          Log.Error($"JSON parse error in {filePath} at path={str}, line={ex.LineNumber}: {ex.Message}");
          var content1 = __instance.RepairCommonJsonErrors(content);
          if (content1 != null)
          {
            Log.Info("JSON repair succeeded, retrying load...");
            ReadSaveResult<T> readSaveResult = __instance.LoadWithAggressiveRecovery<T>(filePath, content1);
            if (!readSaveResult.Success)
              return readSaveResult;
            __instance._saveStore.WriteFile(filePath + ".pre-repair", content);
            return new ReadSaveResult<T>(readSaveResult.SaveData, ReadSaveStatus.JsonRepaired, "Save file had JSON errors that were automatically repaired");
          }
          __instance.PreserveCorruptFile(filePath, ReadSaveStatus.JsonParseError);
          return new ReadSaveResult<T>(ReadSaveStatus.JsonParseError, $"JSON error at {str} (line {ex.LineNumber}): {ex.Message}");
        }
        catch (Exception ex)
        {
          Log.Error($"Unexpected error loading {filePath}: {ex.Message}");
          __instance.PreserveCorruptFile(filePath, ReadSaveStatus.Unrecoverable);
          return new ReadSaveResult<T>(ReadSaveStatus.Unrecoverable, ex.Message);
        } 
      } 
    }
}