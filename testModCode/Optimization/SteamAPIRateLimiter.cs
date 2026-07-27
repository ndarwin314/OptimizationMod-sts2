using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using Steamworks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Platform.Steam;


namespace testMod.testModCode.Optimization;

[HarmonyPatch(typeof(SteamAPI), nameof(SteamAPI.IsSteamRunning))]
public class SteamApiRateLimiter
{
    private static DateTime _lastCheckTime;
    private const int DelayMilliseconds = 1000;

    private static bool Helper()
    {
        TimeSpan delta =  DateTime.Now - _lastCheckTime;
        _lastCheckTime = DateTime.Now;
        return delta.TotalMilliseconds >= DelayMilliseconds;
    }

    // this is very hacky but currently calls to SteamAPI_IsSteamRunning can cause significant lag 
    // for reasons i don't understand so this is a bandaid to make it run less frequently
    // i would have preferred changing the run task async method but afaik that is harder since
    // it invokes a hook which i cant do outside the class
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result)
    {
        if (Helper()) return true;

        __result = true;
        return false;
    }
}
