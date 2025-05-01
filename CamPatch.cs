using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BepInEx.Unity.IL2CPP.UnityEngine;

namespace KillerCam
{
    [HarmonyPatch(typeof(Player), "Update")]
    public class CamPatch
    {
        // Static variables for camera control
        private static bool isSpectatingMurderer = false;
        private static bool isSpectatingVictim = false;
        private static Camera murdererCamera = null;
        private static GameObject murdererCameraObject = null;
        private static Camera victimCamera = null;
        private static GameObject victimCameraObject = null;
        private static Camera playerCamera = null;
        
        // Track which camera we're currently using
        private static SpectateTarget currentSpectateTarget = SpectateTarget.None;
        
        // Enum to track which camera we're using
        private enum SpectateTarget
        {
            None,
            Murderer,
            Victim
        }
        public static MurdererInfoProvider murdererInfoProvider;
        public static MurderController murderController;
        private static UnityEngine.KeyCode toggleKey = UnityEngine.KeyCode.F8; // You can change this to any key you prefer
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppToggleKeyMurderer = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.F8; // IL2CPP equivalent
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppToggleKeyVictim = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.F9; // IL2CPP equivalent
        
        // Track the murderer's last room to avoid unnecessary culling updates
        private static NewRoom lastMurdererRoom = null;
        private static float cullingUpdateCooldown = 0f;
        
        // Track spectating state between frames
        private static bool wasSpectatingLastFrame = false;

        // IL2CPP key codes for arrow keys
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppUpKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.UpArrow;
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppDownKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.DownArrow;
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppLeftKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.LeftArrow;
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppRightKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.RightArrow;
        
        // Track key state to implement our own key press detection
        private static bool wasKeyPressed = false;
        
        // Camera rotation variables
        
        // Camera rotation variables
        private static float rotationX = 0f;
        private static float rotationY = 0f;
        private static float rotationSpeed = 5f;
        
        // Camera transition variables
        private static bool isTransitioning = false;
        private static Vector3 transitionStartPosition;
        private static Quaternion transitionStartRotation;
        private static Vector3 transitionTargetPosition;
        private static Quaternion transitionTargetRotation;
        private static float transitionProgress = 0f;
        private static float transitionDuration = 1.5f; // Seconds for a complete transition
        private static SpectateTarget transitionTargetType = SpectateTarget.None;
        private static Camera activeCamera = null; // The currently active camera during transitions
        private static Camera sourceCamera = null; // The camera we're transitioning from
        private static Camera targetCamera = null; // The camera we're transitioning to
        
        // Method to start a camera transition between two cameras
        private static void TransitionCamera(Camera source, Camera target, SpectateTarget targetType)
        {
            if (source == null || target == null)
            {
                KillerCam.Logger.LogError("Cannot transition with null cameras");
                return;
            }
            
            // Disable collision detection during transitions
            isTransitioning = true;
            
            // Set up transition parameters
            transitionStartPosition = source.transform.position;
            transitionStartRotation = source.transform.rotation;
            
            // If transitioning to a target camera (murderer or victim), calculate position behind them
            if (targetType != SpectateTarget.None)
            {
                // Get the target human
                Human targetHuman = null;
                if (targetType == SpectateTarget.Murderer && murderController != null)
                    targetHuman = murderController.currentMurderer?.GetComponent<Human>();
                else if (targetType == SpectateTarget.Victim && murderController != null)
                    targetHuman = murderController.currentVictim?.GetComponent<Human>();
                
                if (targetHuman != null)
                {
                    Vector3 targetPosition = targetHuman.transform.position;
                    Quaternion targetRotation = targetHuman.transform.rotation;
                    
                    // Calculate position behind and above the target
                    transitionTargetPosition = targetPosition + new Vector3(0, 1.7f, 0) - (targetRotation * Vector3.forward * 1.5f);
                    transitionTargetRotation = targetRotation;
                }
                else
                {
                    // Fallback if we can't find the target human
                    transitionTargetPosition = target.transform.position;
                    transitionTargetRotation = target.transform.rotation;
                }
            }
            else
            {
                // For player camera, use its current position
                transitionTargetPosition = target.transform.position;
                transitionTargetRotation = target.transform.rotation;
            }
            
            // Enable both cameras during transition
            source.enabled = true;
            target.enabled = true;
            
            // Set active camera for the transition
            activeCamera = target;
            sourceCamera = source;
            targetCamera = target;
            
            // Set transition parameters
            transitionProgress = 0f;
            isTransitioning = true;
            transitionTargetType = targetType;
            
            KillerCam.Logger.LogInfo($"Started camera transition to {targetType}");
        }
        
        // Track key states for arrow keys
        private static bool wasUpPressed = false;
        private static bool wasDownPressed = false;
        private static bool wasLeftPressed = false;
        private static bool wasRightPressed = false;
        
        [HarmonyPrefix]
        public static void Prefix(Player __instance)
        {
            
            // Check for F8 key press
            bool isKeyDownMurderer = BepInEx.Unity.IL2CPP.UnityEngine.Input.GetKeyInt(il2cppToggleKeyMurderer);
            bool isKeyDownVictim = BepInEx.Unity.IL2CPP.UnityEngine.Input.GetKeyInt(il2cppToggleKeyVictim);

            
            // Handle murderer camera toggle (F8)
            if (isKeyDownMurderer && !wasKeyPressed)
            {
                // Get the current murder controller instance
                murderController = MurderController.Instance;
                KillerCam.Logger.LogInfo("F8 Pressed, MurderController: " + (murderController != null ? "Not null" : "Null"));
                
                // Log the murderer position if available
                if (murderController != null && murderController.currentMurderer != null)
                {
                    Vector3 pos = murderController.currentMurderer.transform.position;
                    KillerCam.Logger.LogInfo("Murderer position: " + pos.ToString());
                }
                
                // Toggle the murderer camera
                ToggleSpectateCamera(SpectateTarget.Murderer);
            }
            
            // Handle victim camera toggle (F9)
            if (isKeyDownVictim && !wasKeyPressed)
            {
                // Get the current murder controller instance
                murderController = MurderController.Instance;
                KillerCam.Logger.LogInfo("F9 Pressed, MurderController: " + (murderController != null ? "Not null" : "Null"));
                
                // Log the victim position if available
                if (murderController != null && murderController.currentVictim != null)
                {
                    Vector3 pos = murderController.currentVictim.transform.position;
                    KillerCam.Logger.LogInfo("Victim position: " + pos.ToString());
                }
                
                // Toggle the victim camera
                ToggleSpectateCamera(SpectateTarget.Victim);
            }
            
            wasKeyPressed = isKeyDownMurderer || isKeyDownVictim;
            
            // Handle arrow key rotation when in any spectator camera mode
            if ((isSpectatingMurderer || isSpectatingVictim) && (murdererCamera != null || victimCamera != null))
            {
                // Check arrow key presses using IL2CPP compatible method
                bool upPressed = BepInEx.Unity.IL2CPP.UnityEngine.Input.GetKeyInt(il2cppUpKey);
                bool downPressed = BepInEx.Unity.IL2CPP.UnityEngine.Input.GetKeyInt(il2cppDownKey);
                bool leftPressed = BepInEx.Unity.IL2CPP.UnityEngine.Input.GetKeyInt(il2cppLeftKey);
                bool rightPressed = BepInEx.Unity.IL2CPP.UnityEngine.Input.GetKeyInt(il2cppRightKey);
                
                // Update rotation values based on arrow keys
                if (upPressed)
                {
                    // Look up (decrease X rotation)
                    cameraRotationX -= cameraRotationSpeed;
                }
                if (downPressed)
                {
                    // Look down (increase X rotation)
                    cameraRotationX += cameraRotationSpeed;
                }
                if (leftPressed)
                {
                    // Look left (decrease Y rotation offset)
                    cameraYOffset -= cameraRotationSpeed;
                }
                if (rightPressed)
                {
                    // Look right (increase Y rotation offset)
                    cameraYOffset += cameraRotationSpeed;
                }
                
                // Clamp vertical rotation to prevent camera flipping
                cameraRotationX = Mathf.Clamp(cameraRotationX, -80f, 80f);
                
                // Handle camera transitions or normal updates
                if (isTransitioning)
                {
                    // Update the transition progress
                    transitionProgress += Time.deltaTime / transitionDuration;
                    
                    // Clamp progress to 0-1 range
                    transitionProgress = Mathf.Clamp01(transitionProgress);
                    
                    // Use smooth step for easing
                    float t = Mathf.SmoothStep(0f, 1f, transitionProgress);
                    
                    // Interpolate position and rotation
                    Vector3 newPosition = Vector3.Lerp(transitionStartPosition, transitionTargetPosition, t);
                    Quaternion newRotation = Quaternion.Slerp(transitionStartRotation, transitionTargetRotation, t);
                    
                    // Apply to the active camera
                    if (activeCamera != null)
                    {
                        activeCamera.transform.position = newPosition;
                        activeCamera.transform.rotation = newRotation;
                    }
                    
                    // Check if transition is complete
                    if (transitionProgress >= 1.0f)
                    {
                        // Transition complete
                        isTransitioning = false;
                        transitionProgress = 0f;
                        
                        // Update the appropriate camera based on transition target
                        if (transitionTargetType == SpectateTarget.Murderer)
                        {
                            isSpectatingMurderer = true;
                            isSpectatingVictim = false;
                            currentSpectateTarget = SpectateTarget.Murderer;
                        }
                        else if (transitionTargetType == SpectateTarget.Victim)
                        {
                            isSpectatingVictim = true;
                            isSpectatingMurderer = false;
                            currentSpectateTarget = SpectateTarget.Victim;
                        }
                        else
                        {
                            // Transition back to player
                            isSpectatingMurderer = false;
                            isSpectatingVictim = false;
                            currentSpectateTarget = SpectateTarget.None;
                            
                            // Enable player camera
                            if (playerCamera != null)
                            {
                                playerCamera.enabled = true;
                            }
                            
                            // Disable other cameras
                            if (murdererCamera != null)
                            {
                                murdererCamera.enabled = false;
                            }
                            if (victimCamera != null)
                            {
                                victimCamera.enabled = false;
                            }
                            
                            // Restore HUD elements
                            RestoreHUDElements();
                            
                            // Deactivate room tracking
                            MurdererRoomTracker.IsActive = false;
                            MurdererRoomTracker.RestorePlayerRooms();
                        }
                    }
                }
                else
                {
                    // Normal camera updates (no transition)
                    if (isSpectatingMurderer)
                    {
                        UpdateMurdererCamera();
                    }
                    else if (isSpectatingVictim)
                    {
                        UpdateVictimCamera();
                    }
                }
                
                // Continuously check and hide HUD elements while in spectate mode
                UpdateHUDVisibility();
            }
            else if (wasSpectatingLastFrame && !isSpectatingMurderer)
            {
                // If we just switched back to player view, restore HUD elements
                RestoreHUDElements();
                wasSpectatingLastFrame = false;
            }
            
            // Track spectating state for the next frame
            if (isSpectatingMurderer)
            {
                wasSpectatingLastFrame = true;
            }
            
            // If we're spectating the murderer, update the camera position and handle culling
            if (isSpectatingMurderer)
            {
                UpdateMurdererCamera();
                
                // Handle culling for the murderer's location to ensure proper rendering
                if (murderController != null && murderController.currentMurderer != null)
                {
                    try
                    {
                        // Find the room the murderer is in
                        Human murdererHuman = murderController.currentMurderer.GetComponent<Human>();
                        NewRoom murdererRoom = murdererHuman?.currentRoom;
                        
                        // Always ensure the murderer's room is visible
                        if (murdererRoom != null)
                        {
                            // Update the MurdererRoomTracker with current room information
                            MurdererRoomTracker.UpdateMurdererRoom(murdererRoom);
                            
                            // Only do a full culling update when the room changes or on cooldown
                            cullingUpdateCooldown -= Time.deltaTime;
                            if (murdererRoom != lastMurdererRoom || cullingUpdateCooldown <= 0)
                            {
                                // Force a full culling update
                                GeometryCullingController.Instance.UpdateCullingForRoom(murdererRoom, true, false, null, true);
                                KillerCam.Logger.LogInfo("Full culling update for murderer's room: " + murdererRoom.name);
                                
                                // Remember this room and reset cooldown
                                lastMurdererRoom = murdererRoom;
                                cullingUpdateCooldown = 3.0f; // Only update every 3 seconds at most
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        KillerCam.Logger.LogError("Error updating culling for murderer's room: " + ex.Message);
                    }
                }
            }
        }
        
        private static void ToggleSpectateCamera(SpectateTarget target)
        {
            // If we're already spectating this target, switch back to player camera
            if ((target == SpectateTarget.Murderer && isSpectatingMurderer) ||
                (target == SpectateTarget.Victim && isSpectatingVictim))
            {
                // Switch back to player camera
                SwitchToPlayerCamera();
                
                // Reset flags
                isSpectatingMurderer = false;
                isSpectatingVictim = false;
                currentSpectateTarget = SpectateTarget.None;
            }
            else
            {
                // If we're currently spectating something else, switch to player first
                if (isSpectatingMurderer || isSpectatingVictim)
                {
                    SwitchToPlayerCamera();
                    isSpectatingMurderer = false;
                    isSpectatingVictim = false;
                }
                
                // Now switch to the requested target
                if (target == SpectateTarget.Murderer)
                {
                    SwitchToTargetCamera(target);
                    isSpectatingMurderer = true;
                    isSpectatingVictim = false;
                }
                else if (target == SpectateTarget.Victim)
                {
                    SwitchToTargetCamera(target);
                    isSpectatingVictim = true;
                    isSpectatingMurderer = false;
                }
                
                // Update current target
                currentSpectateTarget = target;
            }
        }
        
        // List to track hidden HUD elements
        private static List<GameObject> hiddenHUDElements = new List<GameObject>();
        
        private static void SwitchToMurdererCamera()
        {
            try
            {
                // Find the active camera in the scene
                if (playerCamera == null)
                {
                    // Try to find the active camera
                    playerCamera = FindActiveCamera();
                    KillerCam.Logger.LogInfo("Found active camera: " + (playerCamera != null) + 
                        (playerCamera != null ? ", Name: " + playerCamera.name : ""));
                }
                
                // Create murderer camera if it doesn't exist
                if (murdererCamera == null)
                {
                    CreateMurdererCamera();
                    KillerCam.Logger.LogInfo("Created murderer camera: " + (murdererCamera != null));
                }
                
                // Disable player camera and enable murderer camera
                if (playerCamera != null)
                {
                    playerCamera.enabled = false;
                    KillerCam.Logger.LogInfo("Disabled player camera");
                }
                
                if (murdererCamera != null)
                {
                    murdererCamera.enabled = true;
                    UpdateMurdererCamera(); // Initial position update
                    
                    // Enable the MurdererRoomTracker to handle culling
                    if (murderController != null && murderController.currentMurderer != null)
                    {
                        Human murdererHuman = murderController.currentMurderer.GetComponent<Human>();
                        NewRoom murdererRoom = murdererHuman?.currentRoom;
                        
                        if (murdererRoom != null)
                        {
                            // First save and clear player rooms for optimization
                            MurdererRoomTracker.SaveAndClearPlayerRooms();
                            
                            // Then set up murderer room tracking
                            MurdererRoomTracker.UpdateMurdererRoom(murdererRoom);
                            MurdererRoomTracker.IsActive = true;
                            KillerCam.Logger.LogInfo("Activated MurdererRoomTracker for room: " + murdererRoom.name);
                        }
                    }
                    
                    // Hide HUD elements
                    HideHUDElements();
                    
                    KillerCam.Logger.LogInfo("Switched to murderer camera. Press " + toggleKey.ToString() + " to switch back.");
                }
            }
            catch (Exception ex)
            {
                // If anything fails, try to revert to player camera
               // isSpectatingMurderer = false;
                
                if (playerCamera != null)
                {
                    playerCamera.enabled = true;
                }
                
                if (murdererCamera != null)
                {
                    murdererCamera.enabled = false;
                }
                
                // Disable the MurdererRoomTracker
                MurdererRoomTracker.IsActive = false;
                
                KillerCam.Logger.LogError("Error switching to murderer camera: " + ex.Message);
            }
        }
        
        // Method to continuously update HUD visibility during runtime
        private static void UpdateHUDVisibility()
        {
            try
            {
                if (InterfaceControls.Instance == null)
                    return;
                
                // Hide reticle/crosshair
                if (InterfaceControls.Instance.reticleContainer != null && 
                    InterfaceControls.Instance.reticleContainer.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.reticleContainer.gameObject.SetActive(false);
                    if (!hiddenHUDElements.Contains(InterfaceControls.Instance.reticleContainer.gameObject))
                        hiddenHUDElements.Add(InterfaceControls.Instance.reticleContainer.gameObject);
                }
                
                // Hide interaction elements
                if (InterfaceControls.Instance.interactionRect != null && 
                    InterfaceControls.Instance.interactionRect.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.interactionRect.gameObject.SetActive(false);
                    if (!hiddenHUDElements.Contains(InterfaceControls.Instance.interactionRect.gameObject))
                        hiddenHUDElements.Add(InterfaceControls.Instance.interactionRect.gameObject);
                }
                
                // Hide interaction text
                if (InterfaceControls.Instance.interactionTextContainer != null && 
                    InterfaceControls.Instance.interactionTextContainer.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.interactionTextContainer.gameObject.SetActive(false);
                    if (!hiddenHUDElements.Contains(InterfaceControls.Instance.interactionTextContainer.gameObject))
                        hiddenHUDElements.Add(InterfaceControls.Instance.interactionTextContainer.gameObject);
                }
                
                // Hide action interaction display
                if (InterfaceControls.Instance.actionInteractionDisplay != null && 
                    InterfaceControls.Instance.actionInteractionDisplay.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.actionInteractionDisplay.gameObject.SetActive(false);
                    if (!hiddenHUDElements.Contains(InterfaceControls.Instance.actionInteractionDisplay.gameObject))
                        hiddenHUDElements.Add(InterfaceControls.Instance.actionInteractionDisplay.gameObject);
                }
                
                // Hide light orb
                if (InterfaceControls.Instance.lightOrbRect != null && 
                    InterfaceControls.Instance.lightOrbRect.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.lightOrbRect.gameObject.SetActive(false);
                    if (!hiddenHUDElements.Contains(InterfaceControls.Instance.lightOrbRect.gameObject))
                        hiddenHUDElements.Add(InterfaceControls.Instance.lightOrbRect.gameObject);
                }
                
                // Hide notifications - direct approach using GameObject.Find
                try {
                    // We'll use GameObject.Find to locate notification objects
                    // Look for common parent objects that might contain notifications
                    var notificationObjects = new string[] {
                        "NotificationIcon", "Notification", "NotificationParent", "NotificationsPanel", 
                        "HUDNotificationsIcon", "NotificationController",
                        // Add more specific names based on the game's UI hierarchy
                        "NotificationPanel", "NotifyIcon", "NotifyPanel", "HUDNotifications"
                    };
                    
                    foreach (var objName in notificationObjects)
                    {
                        var obj = GameObject.Find(objName);
                        if (obj != null && obj.activeSelf)
                        {
                            obj.SetActive(false);
                            if (!hiddenHUDElements.Contains(obj))
                                hiddenHUDElements.Add(obj);
                            
                            // If we found a notification object, also try to find its HUD icon
                            var hudIcon = GameObject.Find(objName + "Icon");
                            if (hudIcon != null && hudIcon.activeSelf)
                            {
                                hudIcon.SetActive(false);
                                if (!hiddenHUDElements.Contains(hudIcon))
                                    hiddenHUDElements.Add(hudIcon);
                            }
                        }
                    }
                    
                    // Try to find notification objects in the HUD canvas
                    if (InterfaceControls.Instance.hudCanvas != null)
                    {
                        // We can't iterate through children in IL2CPP, but we can check if the canvas itself
                        // has notifications and hide the entire canvas as a last resort
                        // This is commented out because it would hide ALL HUD elements
                        // InterfaceControls.Instance.hudCanvas.gameObject.SetActive(false);
                        // hiddenHUDElements.Add(InterfaceControls.Instance.hudCanvas.gameObject);
                    }
                } catch (Exception ex) {
                    // Just log and continue
                    KillerCam.Logger.LogWarning($"Error hiding notifications: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // Just log and continue
                KillerCam.Logger.LogWarning($"Error updating HUD visibility: {ex.Message}");
            }
        }
        
        // Method to hide HUD elements
        private static void HideHUDElements()
        {
            try
            {
                // Clear the list of hidden elements
                hiddenHUDElements.Clear();
                
                if (InterfaceControls.Instance == null)
                {
                    KillerCam.Logger.LogWarning("InterfaceControls.Instance is null, cannot hide HUD elements");
                    return;
                }
                
                // Hide reticle/crosshair
                if (InterfaceControls.Instance.reticleContainer != null && 
                    InterfaceControls.Instance.reticleContainer.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.reticleContainer.gameObject.SetActive(false);
                    hiddenHUDElements.Add(InterfaceControls.Instance.reticleContainer.gameObject);
                }
                
                // Hide interaction elements
                if (InterfaceControls.Instance.interactionRect != null && 
                    InterfaceControls.Instance.interactionRect.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.interactionRect.gameObject.SetActive(false);
                    hiddenHUDElements.Add(InterfaceControls.Instance.interactionRect.gameObject);
                }
                
                // Hide interaction text
                if (InterfaceControls.Instance.interactionTextContainer != null && 
                    InterfaceControls.Instance.interactionTextContainer.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.interactionTextContainer.gameObject.SetActive(false);
                    hiddenHUDElements.Add(InterfaceControls.Instance.interactionTextContainer.gameObject);
                }
                
                // Hide action interaction display
                if (InterfaceControls.Instance.actionInteractionDisplay != null && 
                    InterfaceControls.Instance.actionInteractionDisplay.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.actionInteractionDisplay.gameObject.SetActive(false);
                    hiddenHUDElements.Add(InterfaceControls.Instance.actionInteractionDisplay.gameObject);
                }
                
                // Hide light orb
                if (InterfaceControls.Instance.lightOrbRect != null && 
                    InterfaceControls.Instance.lightOrbRect.gameObject.activeSelf)
                {
                    InterfaceControls.Instance.lightOrbRect.gameObject.SetActive(false);
                    hiddenHUDElements.Add(InterfaceControls.Instance.lightOrbRect.gameObject);
                }
                
                KillerCam.Logger.LogInfo("HUD elements hidden for murderer spectate mode");
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error hiding HUD elements: {ex.Message}");
            }
        }
        
        private static void SwitchToPlayerCamera()
        {
            try
            {
                // If a transition is already in progress, don't start a new one
                if (isTransitioning)
                {
                    KillerCam.Logger.LogInfo("Camera transition already in progress, ignoring switch request");
                    return;
                }
                
                // Find the player camera if it's not already set
                if (playerCamera == null)
                {
                    playerCamera = FindActiveCamera();
                    if (playerCamera == null)
                    {
                        KillerCam.Logger.LogError("Could not find player camera for transition");
                        return;
                    }
                }
                
                // Direct switch back to player camera (no transition)
                KillerCam.Logger.LogInfo("Direct switch back to player camera");
                
                // Enable player camera
                if (playerCamera != null)
                {
                    playerCamera.enabled = true;
                    KillerCam.Logger.LogInfo("Enabled player camera");
                }
                else if (CameraController.Instance != null)
                {
                    try
                    {
                        // Try to find and enable the active camera
                        var activeCamera = FindActiveCamera();
                        if (activeCamera != null)
                        {
                            activeCamera.enabled = true;
                            KillerCam.Logger.LogInfo("Enabled found camera as fallback: " + activeCamera.name);
                        }
                    }
                    catch (Exception ex)
                    {
                        KillerCam.Logger.LogError("Failed to enable camera: " + ex.Message);
                    }
                }
                
                // Disable spectator cameras
                if (murdererCamera != null)
                    murdererCamera.enabled = false;
                if (victimCamera != null)
                    victimCamera.enabled = false;
                
                // Restore HUD elements
                RestoreHUDElements();
                
                // Disable the MurdererRoomTracker
                MurdererRoomTracker.IsActive = false;
                MurdererRoomTracker.RestorePlayerRooms();
                
                // Reset flags
                isSpectatingMurderer = false;
                isSpectatingVictim = false;
                currentSpectateTarget = SpectateTarget.None;
            }
            catch (Exception ex)
            {
                // If anything fails, try to revert to player camera directly
                if (playerCamera != null)
                    playerCamera.enabled = true;
                if (murdererCamera != null)
                    murdererCamera.enabled = false;
                if (victimCamera != null)
                    victimCamera.enabled = false;
                    
                // If switching fails, try to find and enable the game's camera
                try
                {
                    if (CameraController.Instance != null)
                    {
                        try
                        {
                            // Try to find and enable the active camera
                            var activeCamera = FindActiveCamera();
                            if (activeCamera != null)
                            {
                                activeCamera.enabled = true;
                                KillerCam.Logger.LogInfo("Emergency camera recovery: enabled found camera " + activeCamera.name);
                            }
                        }
                        catch (Exception ex2)
                        {
                            KillerCam.Logger.LogError("Emergency camera recovery failed: " + ex2.Message);
                            
                            // Last resort - try to enable Camera.main
                            if (Camera.main != null && Camera.main != murdererCamera)
                            {
                                Camera.main.enabled = true;
                                KillerCam.Logger.LogInfo("Last resort: enabled Camera.main");
                            }
                        }
                    }
                }
                catch
                {
                    KillerCam.Logger.LogError("Critical camera failure: couldn't restore any camera");
                }
                
                KillerCam.Logger.LogError("Error switching to player camera: " + ex.Message);
            }

            KillerCam.Logger.LogInfo("Switched to player camera. Press " + toggleKey.ToString() + " to switch back.");
        }
        
        // Method to restore hidden HUD elements
        private static void RestoreHUDElements()
        {
            try
            {
                // Restore all hidden HUD elements
                foreach (var element in hiddenHUDElements)
                {
                    if (element != null)
                    {
                        element.SetActive(true);
                    }
                }
                
                // Clear the list
                hiddenHUDElements.Clear();
                
                // Make sure the HUD canvas is enabled
                if (InterfaceControls.Instance != null && InterfaceControls.Instance.hudCanvas != null)
                {
                    InterfaceControls.Instance.hudCanvas.gameObject.SetActive(true);
                }
                
                KillerCam.Logger.LogInfo("HUD canvas and elements restored");
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error restoring HUD elements: {ex.Message}");
            }
        }
        
        private static void CreateMurdererCamera()
        {
            try
            {
                KillerCam.Logger.LogInfo("Creating murderer camera");
                
                // Create a new camera object - keep it simple like the victim camera
                murdererCameraObject = new GameObject("MurdererCamera");
                murdererCamera = murdererCameraObject.AddComponent<Camera>();
                
                // Find the player camera if needed
                if (playerCamera == null)
                {
                    playerCamera = FindActiveCamera();
                }
                
                // Copy settings from player camera - just like the victim camera does
                if (playerCamera != null)
                {
                    // Use CopyFrom to get all the same settings as the player camera
                    murdererCamera.CopyFrom(playerCamera);
                    murdererCamera.depth = playerCamera.depth; // Use the same depth as player camera
                    
                    // Disable occlusion culling to prevent rendering issues
                    murdererCamera.useOcclusionCulling = false;
                }
                else
                {
                    // Fallback settings if no player camera is found
                    murdererCamera.fieldOfView = 60f;
                    murdererCamera.nearClipPlane = 0.1f;
                    murdererCamera.farClipPlane = 1000f;
                    murdererCamera.useOcclusionCulling = false;
                    KillerCam.Logger.LogWarning("No player camera found, using default settings");
                }
                
                // Reset rotation values to defaults
                cameraRotationX = 0f;
                cameraYOffset = 0f;
                
                // Disable it initially
                murdererCamera.enabled = false;
                
                KillerCam.Logger.LogInfo("Successfully created murderer camera");
            }
            catch (Exception ex)
            {
                // If camera creation fails, reset our state
                murdererCamera = null;
                murdererCameraObject = null;
                isSpectatingMurderer = false;
                
                KillerCam.Logger.LogError("Error creating murderer camera: " + ex.Message);
            }
        }
        
        // Method to find the active camera in the scene
        private static Camera FindActiveCamera()
        {
            try
            {
                // Try Camera.main first - this is the most reliable way
                if (Camera.main != null)
                {
                    KillerCam.Logger.LogInfo("Found Camera.main: " + Camera.main.name);
                    return Camera.main;
                }
                
                // If Camera.main is null, try to get the camera from the player
                if (Player.Instance != null)
                {
                    // Try to find a camera on the player or its children
                    Camera playerCam = Player.Instance.GetComponentInChildren<Camera>();
                    if (playerCam != null)
                    {
                        KillerCam.Logger.LogInfo("Found camera on player: " + playerCam.name);
                        return playerCam;
                    }
                }
                
                // Last resort - try to access the camera through CameraController
                if (CameraController.Instance != null)
                {
                    try
                    {
                        // Try to get the camera as a component on CameraController
                        Camera controllerCam = CameraController.Instance.GetComponent<Camera>();
                        if (controllerCam != null)
                        {
                            KillerCam.Logger.LogInfo("Found camera on CameraController: " + controllerCam.name);
                            return controllerCam;
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors here
                    }
                }
                
                KillerCam.Logger.LogError("No suitable camera found in the scene");
                return null;
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error finding active camera: " + ex.Message);
                return null;
            }
        }
        
        
        // Variables for camera rotation control
        private static float cameraRotationX = 0f;
        private static float cameraYOffset = 0f; // Manual offset for left/right rotation
        private static float cameraRotationSpeed = 3.5f;
        
        // Camera collision variables
        private static float defaultCameraDistance = 1.5f;  // Default distance behind the murderer
        private static float minCameraDistance = 0.5f;     // Minimum distance when colliding with objects
        private static float collisionRadius = 0.2f;       // Radius of collision detection
        private static float currentDistance = 1.5f;      // Current camera distance (will be adjusted based on collisions)
        
        private static void UpdateMurdererCamera()
        {
            try
            {
                // Safety check
                if (murdererCameraObject == null)
                {
                    return;
                }
                
                // Get murderer location directly from MurderController.Instance
                murderController = MurderController.Instance;
                
                if (murderController == null || murderController.currentMurderer == null)
                {
                    return;
                }
                
                // Get the murderer's position and rotation
                Transform murdererTransform = murderController.currentMurderer.transform;
                if (murdererTransform == null)
                {
                    return;
                }
                
                // Calculate the desired position (behind and slightly above the murderer)
                Vector3 targetPosition = murdererTransform.position + new Vector3(0, 1.7f, 0);
                
                // Apply rotation offset from arrow keys
                Quaternion targetRotation = murdererTransform.rotation * Quaternion.Euler(cameraRotationX, cameraYOffset, 0);
                
                // Get the direction the camera should be looking
                Vector3 cameraDirection = targetRotation * Vector3.forward * -1f;
                
                // Default distance from the murderer
                float adjustedDistance = defaultCameraDistance;
                RaycastHit hit;
                
                // Only check for collisions if we're not transitioning
                if (!isTransitioning)
                {
                    // Check for collisions using a simple raycast instead of SphereCast (more compatible with IL2CPP)
                    // Cast multiple rays in slightly different directions to simulate a sphere
                    bool hitDetected = false;
                    float closestHitDistance = defaultCameraDistance;
                    
                    // Main raycast straight from the target position
                    if (Physics.Raycast(targetPosition, cameraDirection, out hit, defaultCameraDistance))
                    {
                        hitDetected = true;
                        closestHitDistance = hit.distance;
                    }
                    
                    // Additional raycasts in slightly offset directions to simulate a sphere
                    Vector3[] offsets = new Vector3[] {
                        new Vector3(collisionRadius, 0, 0),
                        new Vector3(-collisionRadius, 0, 0),
                        new Vector3(0, collisionRadius, 0),
                        new Vector3(0, -collisionRadius, 0)
                    };
                    
                    foreach (Vector3 offset in offsets)
                    {
                        if (Physics.Raycast(targetPosition + offset, cameraDirection, out hit, defaultCameraDistance))
                        {
                            hitDetected = true;
                            if (hit.distance < closestHitDistance)
                            {
                                closestHitDistance = hit.distance;
                            }
                        }
                    }
                    
                    if (hitDetected)
                    {
                        // If we hit something, adjust the distance to be just before the hit point
                        adjustedDistance = Mathf.Max(closestHitDistance * 0.9f, minCameraDistance);
                    }
                    
                    // Smoothly adjust the current distance
                    currentDistance = Mathf.Lerp(currentDistance, adjustedDistance, Time.deltaTime * 5f);
                }
                else
                {
                    // During transitions, use the default distance without collision checks
                    currentDistance = defaultCameraDistance;
                }
                
                // Calculate the final camera position with collision avoidance
                Vector3 finalPosition = targetPosition + (cameraDirection * currentDistance);
                
                // Smoothly move the camera to the new position - use a new velocity vector like the victim camera
                Vector3 currentVelocity = Vector3.zero;
                float smoothTime = 0.1f; // Increased from 0.1f for smoother auto-rotation
                
                murdererCameraObject.transform.position = Vector3.SmoothDamp(
                    murdererCameraObject.transform.position,
                    finalPosition,
                    ref currentVelocity,
                    smoothTime
                );
                
                // Update the camera rotation to look at the murderer (plus our offset)
                murdererCamera.transform.rotation = targetRotation;
                
                // Update culling for the murderer's room if needed
                Human murdererHuman = murderController.currentMurderer.GetComponent<Human>();
                if (murdererHuman != null && murdererHuman.currentRoom != null)
                {
                    MurdererRoomTracker.UpdateMurdererRoom(murdererHuman.currentRoom);
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error updating murderer camera: " + ex.Message);
            }
        }
        
        // Method to update the victim camera position and rotation
        private static void UpdateVictimCamera()
        {
            try
            {
                if (victimCamera == null || !victimCamera.enabled || murderController == null || murderController.currentVictim == null)
                {
                    return;
                }
                
                // Get the victim's position and rotation
                Transform victimTransform = murderController.currentVictim.transform;
                if (victimTransform == null)
                {
                    return;
                }
                
                // Calculate the desired position (behind and slightly above the victim)
                Vector3 targetPosition = victimTransform.position + new Vector3(0, 1.7f, 0);
                
                // Apply rotation offset from arrow keys
                Quaternion targetRotation = victimTransform.rotation * Quaternion.Euler(cameraRotationX, cameraYOffset, 0);
                
                // Get the direction the camera should be looking
                Vector3 cameraDirection = targetRotation * Vector3.forward * -1f;
                
                // Default distance from the victim
                float defaultCameraDistance = 1.5f;
                float currentDistance = defaultCameraDistance;
                float minCameraDistance = 0.5f;
                
                // Only check for collisions if we're not transitioning
                if (!isTransitioning)
                {
                    // Check for collisions to avoid clipping through walls
                    RaycastHit hit;
                    bool hitDetected = false;
                    float closestHitDistance = float.MaxValue;
                    
                    // Cast rays in multiple directions to avoid clipping through walls
                    Vector3[] offsets = new Vector3[5]
                    {
                        Vector3.zero,
                        new Vector3(0.2f, 0, 0),
                        new Vector3(-0.2f, 0, 0),
                        new Vector3(0, 0.2f, 0),
                        new Vector3(0, -0.2f, 0)
                    };
                    
                    foreach (Vector3 offset in offsets)
                    {
                        if (Physics.Raycast(targetPosition + offset, cameraDirection, out hit, defaultCameraDistance))
                        {
                            hitDetected = true;
                            if (hit.distance < closestHitDistance)
                            {
                                closestHitDistance = hit.distance;
                            }
                        }
                    }
                    
                    if (hitDetected)
                    {
                        // If we hit something, adjust the distance to be just before the hit point
                        currentDistance = Mathf.Max(closestHitDistance * 0.9f, minCameraDistance);
                    }
                }
                else
                {
                    // During transitions, use the default distance without collision checks
                    currentDistance = defaultCameraDistance;
                }
                
                // Calculate the final camera position with collision avoidance
                Vector3 finalPosition = targetPosition + (cameraDirection * currentDistance);
                
                // Smoothly move the camera to the new position
                Vector3 currentVelocity = Vector3.zero;
                float smoothDampTime = 0.1f; // Increased from 0.1f for smoother auto-rotation
                
                victimCamera.transform.position = Vector3.SmoothDamp(
                    victimCamera.transform.position,
                    finalPosition,
                    ref currentVelocity,
                    smoothDampTime
                );
                
                // Update the camera rotation to look at the victim (plus our offset)
                victimCamera.transform.rotation = targetRotation;
                
                // Update culling for the victim's room if needed
                Human victimHuman = murderController.currentVictim.GetComponent<Human>();
                if (victimHuman != null && victimHuman.currentRoom != null)
                {
                    MurdererRoomTracker.UpdateMurdererRoom(victimHuman.currentRoom);
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error updating victim camera: " + ex.Message);
            }
        }
        
        // Method to switch to either murderer or victim camera with smooth transitions
        private static void SwitchToTargetCamera(SpectateTarget target)
        {
            try
            {
                // If a transition is already in progress, don't start a new one
                if (isTransitioning)
                {
                    KillerCam.Logger.LogInfo("Camera transition already in progress, ignoring switch request");
                    return;
                }
                
                // Find the active camera in the scene
                if (playerCamera == null)
                {
                    // Try to find the active camera
                    playerCamera = FindActiveCamera();
                    KillerCam.Logger.LogInfo("Found active camera: " + (playerCamera != null) + 
                        (playerCamera != null ? ", Name: " + playerCamera.name : ""));
                }
                
                // Create target camera if it doesn't exist
                Camera targetCamera = null;
                Human targetHuman = null;
                NewRoom targetRoom = null;
                
                if (target == SpectateTarget.Murderer)
                {
                    // Create murderer camera if it doesn't exist
                    if (murdererCamera == null)
                    {
                        CreateMurdererCamera();
                        KillerCam.Logger.LogInfo("Created murderer camera: " + (murdererCamera != null));
                    }
                    targetCamera = murdererCamera;
                    
                    // Get murderer human and room
                    if (murderController != null && murderController.currentMurderer != null)
                    {
                        targetHuman = murderController.currentMurderer.GetComponent<Human>();
                        targetRoom = targetHuman?.currentRoom;
                    }
                }
                else if (target == SpectateTarget.Victim)
                {
                    // Create victim camera if it doesn't exist
                    if (victimCamera == null)
                    {
                        // Create a new camera for the victim view
                        GameObject victimCameraObj = new GameObject("VictimCamera");
                        victimCamera = victimCameraObj.AddComponent<Camera>();
                        
                        // Copy settings from player camera
                        if (playerCamera != null)
                        {
                            victimCamera.CopyFrom(playerCamera);
                            victimCamera.depth = playerCamera.depth; // Ensure same rendering order
                        }
                        
                        KillerCam.Logger.LogInfo("Created victim camera");
                    }
                    targetCamera = victimCamera;
                    
                    // Get victim human and room
                    if (murderController != null && murderController.currentVictim != null)
                    {
                        targetHuman = murderController.currentVictim.GetComponent<Human>();
                        targetRoom = targetHuman?.currentRoom;
                    }
                }
                
                if (targetCamera != null)
                {
                    // Start a smooth transition from player camera to target camera
                    TransitionCamera(playerCamera, targetCamera, target);
                    
                    // Hide HUD elements
                    HideHUDElements();
                    
                    // Enable the MurdererRoomTracker to handle culling
                    if (targetRoom != null)
                    {
                        // First save and clear player rooms for optimization
                        MurdererRoomTracker.SaveAndClearPlayerRooms();
                        
                        // Then set up room tracking
                        MurdererRoomTracker.UpdateMurdererRoom(targetRoom);
                        MurdererRoomTracker.IsActive = true;
                        KillerCam.Logger.LogInfo("Activated MurdererRoomTracker for room: " + targetRoom.name);
                    }
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error switching to " + target.ToString() + " camera: " + ex.Message);
            }
        }
    }
}
