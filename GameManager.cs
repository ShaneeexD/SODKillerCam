using HarmonyLib;
using SOD.Common.Helpers.SyncDiskObjects;
using SOD.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using AsmResolver.Collections;
using UnityEngine;
using static Rewired.Data.UserDataStore_PlayerPrefs;
using UnityEngine.SceneManagement;

[HarmonyPatch(typeof(SessionData))]
    [HarmonyPatch("PauseGame")]
    public class PauseGameManager
    {
        public static void Prefix(ref bool showPauseText, ref bool delayOverride, ref bool openDesktopMode)
        {
            GameStateVars.isPaused = true;      
        }
}

    [HarmonyPatch(typeof(SessionData))]
    [HarmonyPatch("ResumeGame")]
    public class ResumeGameManager
    {
        public static void Prefix()
        {
            GameStateVars.isPaused = false;
        }
    }

    public class GameStateVars
    {
        public static bool isPaused = false;
    }

    [HarmonyPatch(typeof(MurderController))]
    [HarmonyPatch("SpawnItem")]
    public class SpawnItemManager
    {
        public static void Prefix(ref MurderController.Murder murder, ref InteractablePreset spawnItem, ref MurderPreset.LeadSpawnWhere spawnWhere, ref MurderPreset.LeadCitizen spawnBelongsTo, ref MurderPreset.LeadCitizen spawnWriter, ref MurderPreset.LeadCitizen spawnReceiver, ref int security, ref InteractablePreset.OwnedPlacementRule ownedRule, ref int priority, ref JobPreset.JobTag itemTag)
        {

        }
    }