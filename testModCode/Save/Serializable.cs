using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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
  
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])]
    public class Save
    {
        public static async Task SaveRun(SerializableRun save, bool isMultiplayer, RunSaveManager __instance)
        {
            string savePath = isMultiplayer ? __instance.CurrentMultiplayerRunSavePath : __instance.CurrentRunSavePath;
            var options = new JsonSerializerOptions
            {
              TypeInfoResolver = JsonTypeInfoResolver.Combine(
                MegaCritSerializerContext.Default, HackSerializerContext.Default)
            };
            var stream = new MemoryStream();
            var compressed = SerializableRunCompressed.FromSerializableRun(save);
            try
            {
                if (!__instance._forceSynchronous)
                {
                    await JsonSerializer.SerializeAsync(stream, compressed, options);
                    stream.Seek(0L, SeekOrigin.Begin);
                    await __instance._saveStore.WriteFileAsync(savePath, stream.ToArray());
                }
                else
                {
                    JsonSerializer.Serialize(stream, compressed, options);
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

        //[HarmonyPrefix]
        public static bool Helper(SerializableRun save, bool isMultiplayer, RunSaveManager __instance, ref Task __result)
        {
            __result = SaveRun(save, isMultiplayer, __instance);
            return false;
        }
    }
}