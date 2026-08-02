using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace testMod.testModCode.Save;

public class Serializable
{
    [HarmonyPatch(typeof(JsonSerializationUtility), nameof(JsonSerializationUtility.Options), MethodType.Getter)]
    public class Utility
    {

        [HarmonyPrefix]
        public static bool Options(ref JsonSerializerOptions __result)
        {
            var options = new JsonSerializerOptions(MegaCritSerializerContext.DefaultGeneratedSerializerOptions);
            options.Converters.Add(new PlayerConverter());
            options.TypeInfoResolver =
                MegaCritSerializerContext.Default.WithAddedModifier(
                        JsonSerializationUtility.AlphabetizeProperties)
                    .WithAddedModifier(JsonSerializeConditionAttribute.CheckJsonSerializeConditionsModifier);
            __result = options;
            return false;
        }
    }

}