/*
 Copyright (C) 2026  Noa Feinberg

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.*/

using HarmonyLib;
using Steamworks;

namespace OptimizationMod.OptimizationModCode.Optimization;

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
