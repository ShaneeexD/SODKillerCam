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
        public static ConfigEntry<bool> hideTargetName;

        public override void Load()
        {
            Logger = Log;
            NewGameHandler eventHandler = new NewGameHandler();
            Logger.LogInfo("Loading Killer Cam...");

            hideTargetName = Config.Bind("General", "HideTargetName", false, new ConfigDescription("Hide the Murderer/Victim name while spectating."));
            
            try
            {
                harmony = new Harmony("KillerCam");
                harmony.PatchAll();
                Logger.LogInfo("All patches applied successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during Load: {ex}");
            }
        }
    }
}