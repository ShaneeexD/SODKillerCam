using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System;
using UnityEngine;

namespace KillerCam
{
    [BepInPlugin("KillerCam", "KillerCam", "1.0.0")]
    public class KillerCam : BasePlugin
    {
        public static ManualLogSource Logger;
        private Harmony harmony;

        public static ConfigEntry<float> walkSpeedMultiplier;
        public static ConfigEntry<float> runSpeedMultiplier;

        public override void Load()
        {
            Logger = Log;
            NewGameHandler eventHandler = new NewGameHandler();
            Logger.LogInfo("Loading Killer Cam...");
            try
            {
                harmony = new Harmony("KillerCam");
                harmony.PatchAll();
                Logger.LogInfo("All patches applied.");
            }

            catch (Exception ex)
            {
                Logger.LogError($"Error during Load: {ex}");
            }
        }
    }
}
