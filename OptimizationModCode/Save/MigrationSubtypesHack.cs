using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Migrations;

namespace OptimizationMod.OptimizationModCode.Save;

public class MigrationSubtypesHack
{
    [HarmonyPatch(typeof(MigrationRegistry), nameof(MigrationRegistry.RegisterAllMigrations))]
    public class Register
    {
        //[HarmonyPostfix]
        public static void RegisterMyMigration(MigrationManager manager)
        {
            var myClass = typeof(SerializableRunV20ToV21);
            try
            {
                if (Activator.CreateInstance(myClass) is IMigration instance)
                {
                    manager.RegisterMigration(instance);
                    Log.Debug($"Registered migration for {instance.SaveType.Name} from v{instance.FromVersion} to v{instance.ToVersion}");
                }
                else
                    Log.Error($"Failed to instantiate migration {myClass.Name}: Created instance is not an IMigration");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to instantiate migration {myClass.Name}: {ex.Message}");
            }
        }
    }
    
    [HarmonyPatch(typeof(MigrationManager), nameof(MigrationManager.DeriveAndSetLatestVersions))]
    public class Version
    {
        //[HarmonyPostfix]
        public static void UpdateCurrentVersion(MigrationManager __instance)
        {
            var key = typeof(SerializableRun);
            __instance._latestVersions[key] += 1;
        }
    }
}