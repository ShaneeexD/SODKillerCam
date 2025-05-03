using HarmonyLib;
using System;
using UnityEngine;

namespace KillerCam
{
    /// <summary>
    /// Patch to fix object visibility when spectating by modifying the CameraController.Instance.cam
    /// </summary>
    public static class ObjectVisibilityPatch
    {
        // Store the original camera for restoration
        private static Camera originalCamera = null;
        private static bool isCameraModified = false;
        
        // Method to update the CameraController.Instance.cam when spectating starts
        public static void UpdateCameraForSpectating()
        {
            try
            {
                // Only modify if we're spectating
                if (!CamPatch.isSpectatingMurderer && !CamPatch.isSpectatingVictim)
                {
                    // If we previously modified the camera, restore it
                    if (isCameraModified)
                    {
                        RestoreOriginalCamera();
                    }
                    return;
                }
                
                // Get the spectator camera transform
                Transform spectatorCam = CamPatch.GetActiveSpectatorCameraTransform();
                if (spectatorCam == null)
                {
                    KillerCam.Logger.LogWarning("Spectator camera transform is null, cannot update camera for object visibility");
                    return;
                }
                
                // Get the CameraController instance
                if (CameraController.Instance == null || CameraController.Instance.cam == null)
                {
                    KillerCam.Logger.LogWarning("CameraController.Instance or its camera is null");
                    return;
                }
                
                // Store the original camera if we haven't already
                if (!isCameraModified)
                {
                    originalCamera = CameraController.Instance.cam;
                    KillerCam.Logger.LogInfo("Stored original camera for later restoration");
                }
                
                // Get the active spectator camera
                Camera spectatorCamera = null;
                if (CamPatch.isSpectatingMurderer && CamPatch.murdererCamera != null)
                {
                    spectatorCamera = CamPatch.murdererCamera;
                }
                else if (CamPatch.isSpectatingVictim && CamPatch.victimCamera != null)
                {
                    spectatorCamera = CamPatch.victimCamera;
                }
                
                if (spectatorCamera != null)
                {
                    // Set the CameraController.Instance.cam to the spectator camera
                    CameraController.Instance.cam = spectatorCamera;
                    isCameraModified = true;
                    KillerCam.Logger.LogInfo($"Updated CameraController.Instance.cam to spectator camera at position {spectatorCamera.transform.position}");
                }
                else
                {
                    KillerCam.Logger.LogWarning("Could not find active spectator camera");
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error in UpdateCameraForSpectating: {ex.Message}");
            }
        }
        
        // Method to restore the original camera when spectating ends
        public static void RestoreOriginalCamera()
        {
            try
            {
                if (!isCameraModified || originalCamera == null || CameraController.Instance == null)
                    return;
                
                // Restore the original camera
                CameraController.Instance.cam = originalCamera;
                isCameraModified = false;
                KillerCam.Logger.LogInfo("Restored original camera in CameraController.Instance.cam");
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error in RestoreOriginalCamera: {ex.Message}");
            }
        }
    }
    
    // Patch for CamPatch.ToggleSpectateCamera to update the camera when spectating state changes
    [HarmonyPatch(typeof(CamPatch), "ToggleSpectateCamera")]
    public class ToggleSpectateCameraPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Update the camera for spectating after toggling
            ObjectVisibilityPatch.UpdateCameraForSpectating();
        }
    }
    
    // Patch for Player.Update to ensure the camera is updated every frame
    [HarmonyPatch(typeof(Player), "Update")]
    public class PlayerUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Update the camera for spectating every frame
            ObjectVisibilityPatch.UpdateCameraForSpectating();
        }
    }
}
