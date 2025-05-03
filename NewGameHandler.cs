using SOD.Common;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Characters.FirstPerson;
using HarmonyLib;

namespace KillerCam
{
    public class NewGameHandler : MonoBehaviour
    {
        public static MurderController.Murder murder;
        public static Toolbox toolbox;
        private GameObject cube;
        public static MurdererInfoProvider murdererInfoProvider;

        public NewGameHandler()
        {       
            Lib.SaveGame.OnAfterLoad += HandleGameLoaded;
            Lib.SaveGame.OnAfterNewGame += HandleNewGameStarted;
        }

        private void HandleNewGameStarted(object sender, EventArgs e)
        {
            CamPatch.SwitchToPlayerCamera();
        }

        private void HandleGameLoaded(object sender, EventArgs e)
        {
            CamPatch.SwitchToPlayerCamera();
        }
    }
}