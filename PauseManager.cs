using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using KillerCam;

namespace KillerCam
{
    [HarmonyPatch(typeof(SessionData))]
    [HarmonyPatch("PauseGame")]
    public class PauseManager
    {
        // Prefix runs before the original PauseGame method
        public static void Prefix(ref bool showPauseText, ref bool delayOverride, ref bool openDesktopMode)
        {
            CamPatch.SwitchToPlayerCamera();
            SpectatorUI.HideText();        
        }
    }

    [HarmonyPatch(typeof(SessionData))]
    [HarmonyPatch("ResumeGame")]
    public class ResumeGameManager
    {
        // Prefix runs before the original ResumeGame method
        public static void Prefix() 
        {
            
        }
    }
}