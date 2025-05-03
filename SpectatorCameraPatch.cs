using HarmonyLib;
using System;
using UnityEngine;

namespace KillerCam
{
    /// <summary>
    /// Patch to fix object visibility when spectating by temporarily moving the player camera
    /// to the spectator camera position during the player update
    /// </summary>
    [HarmonyPatch(typeof(Player), "Update")]
    public class SpectatorCameraPatch
    {
        // Store the original camera position and rotation
        private static Vector3 originalCameraPosition;
        private static Quaternion originalCameraRotation;
        private static bool isCameraModified = false;
        
        [HarmonyPrefix]
        public static void Prefix(Player __instance)
        {
            try
            {
                // Only modify camera when spectating
                if (!CamPatch.isSpectatingMurderer && !CamPatch.isSpectatingVictim)
                {
                    // If we previously modified the camera, restore it
                    if (isCameraModified)
                    {
                        RestorePlayerCamera(__instance);
                    }
                    return;
                }
                
                // Get the spectator camera transform
                Transform spectatorCam = CamPatch.GetActiveSpectatorCameraTransform();
                if (spectatorCam == null)
                    return;
                
                // Get the player camera
                Camera playerCamera = __instance.GetComponentInChildren<Camera>();
                if (playerCamera == null)
                    return;
                
                // Store the original camera position and rotation if we haven't already
                if (!isCameraModified)
                {
                    originalCameraPosition = playerCamera.transform.position;
                    originalCameraRotation = playerCamera.transform.rotation;
                    isCameraModified = true;
                }
                
                // Move the player camera to the spectator camera position and rotation
                // This ensures objects are rendered from the spectator's perspective
                playerCamera.transform.position = spectatorCam.position;
                playerCamera.transform.rotation = spectatorCam.rotation;
                
                // Debug log to confirm the camera is being moved
                KillerCam.Logger.LogInfo($"Moved player camera to spectator position: {spectatorCam.position}");
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error in SpectatorCameraPatch.Prefix: {ex.Message}");
            }
        }
        
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            try
            {
                // Only restore camera when spectating (we'll handle non-spectating in the Prefix)
                if (!CamPatch.isSpectatingMurderer && !CamPatch.isSpectatingVictim)
                    return;
                
                // We don't restore the camera here because we want it to stay at the spectator position
                // during the entire frame. The camera will be restored in the next Prefix call when
                // spectating ends.
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error in SpectatorCameraPatch.Postfix: {ex.Message}");
            }
        }
        
        // Helper method to restore the player camera to its original position
        private static void RestorePlayerCamera(Player player)
        {
            if (!isCameraModified)
                return;
                
            Camera playerCamera = player.GetComponentInChildren<Camera>();
            if (playerCamera == null)
                return;
                
            // Restore the original camera position and rotation
            playerCamera.transform.position = originalCameraPosition;
            playerCamera.transform.rotation = originalCameraRotation;
            
            // Reset the flag
            isCameraModified = false;
            
            // Debug log to confirm the camera is being restored
            KillerCam.Logger.LogInfo("Restored player camera to original position");
        }
    }
}
