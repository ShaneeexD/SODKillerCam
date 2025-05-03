using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BepInEx.Unity.IL2CPP.UnityEngine;
using KillerCam; // Add this if SpectatorUI is in the KillerCam namespace
using TMPro;    // Add this if using TextMeshPro in SpectatorUI
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.SceneManagement;
using FMOD.Studio;  // For StudioListener
using FMODUnity;
using SOD.Common;    // For Unity integration
using SOD.Common.Extensions;

namespace KillerCam
{
    [HarmonyPatch(typeof(Player), "Update")]
    public class CamPatch
    {
        // Static variables for camera control
        public static bool isSpectatingMurderer = false;
        public static bool isSpectatingVictim = false;
        public static Camera murdererCamera = null;
        private static GameObject murdererCameraObject = null;
        public static Camera victimCamera = null;
        private static GameObject victimCameraObject = null;
        private static Camera playerCamera = null;
        
        // Track which camera we're currently using
        private static SpectateTarget currentSpectateTarget = SpectateTarget.None;
        
        public static Human targetHuman = null;
        // Enum to track which camera we're using
        public enum SpectateTarget
        {
            None,
            Murderer,
            Victim
        }
        public static MurdererInfoProvider murdererInfoProvider;
        public static MurderController murderController;
        public static SpeechController speechController;
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
        private static float transitionDuration = 0.5f; // Added duration for smooth transition
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
            // NOTE: We no longer handle SpectateTarget.None here, as returning to player is immediate.
            if (targetType != SpectateTarget.None) 
            {
                // Get the target human
                
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
                    SwitchToPlayerCamera();
                }
            }
            
            // Make sure all cameras are properly set up before transition
            // Disable all cameras first
            if (playerCamera != null) playerCamera.enabled = false;
            if (murdererCamera != null) murdererCamera.enabled = false;
            if (victimCamera != null) victimCamera.enabled = false;

            // Then enable only the target camera (which will be the active one during transition)
            if (target != null) target.enabled = true;
            
            // Set active camera for the transition
            activeCamera = target;
            sourceCamera = source;
            targetCamera = target;
            
            // Set transition parameters
            transitionProgress = 0f;
            isTransitioning = true;
            transitionTargetType = targetType;
            
            KillerCam.Logger.LogInfo($"Started camera transition to {targetType} from {(source == playerCamera ? "player" : (source == murdererCamera ? "murderer" : "victim"))}");
        }
        
        // Track key states for arrow keys
        private static bool wasUpPressed = false;
        private static bool wasDownPressed = false;
        private static bool wasLeftPressed = false;
        private static bool wasRightPressed = false;
        
        [HarmonyPrefix]
        public static void Prefix(Player __instance)
        {
            
            if (!SpectatorUI.isCreated)
            {
                SpectatorUI.CreateSpectatorText();
            }

            if (isSpectatingMurderer || isSpectatingVictim)
            {
              //  TeleportPlayerToTarget(currentSpectateTarget);
            }

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
                    transitionProgress += Time.deltaTime / transitionDuration; // Use transitionDuration
                    
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
                            // Enable murderer camera, disable others
                            if (murdererCamera != null) murdererCamera.enabled = true;
                            if (victimCamera != null) victimCamera.enabled = false;
                            if (playerCamera != null) playerCamera.enabled = false;
                            
                            isSpectatingMurderer = true;
                            isSpectatingVictim = false;
                            currentSpectateTarget = SpectateTarget.Murderer;
                            
                            KillerCam.Logger.LogInfo("Completed transition to murderer camera");
                        }
                        else if (transitionTargetType == SpectateTarget.Victim)
                        {
                            // Enable victim camera, disable others
                            if (victimCamera != null) victimCamera.enabled = true;
                            if (murdererCamera != null) murdererCamera.enabled = false;
                            if (playerCamera != null) playerCamera.enabled = false;
                            
                            isSpectatingVictim = true;
                            isSpectatingMurderer = false;
                            currentSpectateTarget = SpectateTarget.Victim;
                            
                            KillerCam.Logger.LogInfo("Completed transition to victim camera");
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
                

            }
            else if (wasSpectatingLastFrame && !isSpectatingMurderer)
            {

                wasSpectatingLastFrame = false;
            }
            
            // Track spectating state for the next frame
            if (isSpectatingMurderer || isSpectatingVictim)
            {
                wasSpectatingLastFrame = true;
            }
            
            // If we're spectating the murderer or victim, update the camera position and handle culling
            if (isSpectatingMurderer || isSpectatingVictim)
            {
                // Update the appropriate camera
                if (isSpectatingMurderer)
                {
                    UpdateMurdererCamera();
                }
                else // isSpectatingVictim
                {
                    UpdateVictimCamera();
                }
                
                // Get the current target human and room based on which camera we're using
                Human targetHuman = null;
                NewRoom targetRoom = null;
                string targetType = "";
                
                if (isSpectatingMurderer && murderController != null && murderController.currentMurderer != null)
                {
                    targetHuman = murderController.currentMurderer.GetComponent<Human>();
                    targetType = "murderer";
                }
                else if (isSpectatingVictim && murderController != null && murderController.currentVictim != null)
                {
                    targetHuman = murderController.currentVictim.GetComponent<Human>();
                    targetType = "victim";
                }
                
                // Get the room from the target human
                if (targetHuman != null)
                {
                    targetRoom = targetHuman.currentRoom;
                }
                
                // Handle culling for the target's location to ensure proper rendering
                if (targetRoom != null)
                {
                    try
                    {
                        // Only update the room if it's different from the last one or if the cooldown has expired
                        cullingUpdateCooldown -= Time.deltaTime;
                        bool needsUpdate = targetRoom != lastMurdererRoom || cullingUpdateCooldown <= 0;
                        
                        // Always ensure the target room is tracked
                        if (needsUpdate)
                        {
                            // Update the SpectatorRoomTracker with current room information
                            SpectatorRoomTracker.UpdateTargetRoom(targetRoom);
                            
                            // Force a full culling update
                            // No longer needed here, UpdateTargetRoom handles it
                            // GeometryCullingController.Instance.UpdateCullingForRoom(targetRoom, true, false, null, true);
                            KillerCam.Logger.LogInfo($"Full culling update for {targetType}'s room: {targetRoom.name}");
                            
                            // Remember this room and reset cooldown
                            lastMurdererRoom = targetRoom;
                            cullingUpdateCooldown = 3.0f; // Only update every 3 seconds at most
                        }
                    }
                    catch (Exception ex)
                    {
                        KillerCam.Logger.LogError($"Error updating culling for {targetType}'s room: {ex.Message}");
                    }
                }
            }

            // --- Update Spectator UI --- 
            if (isSpectatingMurderer || isSpectatingVictim)
            {
                Human targetHuman = null;
                string targetTypeString = "";

                if (isSpectatingMurderer && murderController?.currentMurderer != null)
                {
                    targetHuman = murderController.currentMurderer.GetComponent<Human>();
                    targetTypeString = "Murderer";
                }
                else if (isSpectatingVictim && murderController?.currentVictim != null)
                {
                    targetHuman = murderController.currentVictim.GetComponent<Human>();
                    targetTypeString = "Victim";
                }

                if (targetHuman != null)
                {
                    string npcName = targetHuman.GetCitizenName() ?? "Unknown";
                    string status = "Idle"; // Default status
                    
                    try 
                    {
                       if (targetHuman.ai != null) {
                            if (targetHuman.ai.currentAction?.preset?.name != null && !string.IsNullOrEmpty(targetHuman.ai.currentAction.preset.name))
                            {
                                status = targetHuman.ai.currentAction.preset.name;
                            }
                            else if (targetHuman.ai.currentGoal?.preset?.name != null && !string.IsNullOrEmpty(targetHuman.ai.currentGoal.preset.name))
                            {
                                status = targetHuman.ai.currentGoal.preset.name;
                            }
                            // Add more specific checks if needed, e.g., for interacting
                            else if (targetHuman.interactingWith != null)
                            {
                                status = "Interacting";
                            }
                            else if (targetHuman.inConversation)
                            {
                                status = "In Conversation";
                            }
                       }
                    }
                    catch (Exception ex) 
                    {
                        // Log error getting AI state, but continue 
                        KillerCam.Logger.LogError($"Error getting AI status for {npcName}: {ex.Message}");
                        status = "Error Reading Status";
                    }

                    if (KillerCam.hideTargetName.Value)
                    {
                        npcName = "Hidden";
                    }

                    string displayText = $"Spectating {targetTypeString}: {npcName} - {status}";
                    SpectatorUI.UpdateText(displayText); 
                    SpectatorUI.ShowText();
                }
                else
                {
                    // If target human is lost, update UI to reflect that
                     SpectatorUI.UpdateText($"Spectating {targetTypeString}: Target Lost");
                     SOD.Common.Lib.GameMessage.ShowPlayerSpeech($"No current {targetTypeString} available", 2f, true);
                     SwitchToPlayerCamera();  
                }
            }
            else
            {
                // Not spectating anyone, ensure UI is hidden
                SpectatorUI.HideText();
            }
            // --- End Update Spectator UI ---
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
                Player.Instance.EnablePlayerMovement(false);

                // If we're currently spectating something else, directly switch to the new target
                // without going back to player camera first
                if (isSpectatingMurderer || isSpectatingVictim)
                {
                    // Direct transition between spectate targets
                    SwitchToTargetCamera(target);
                    
                    // Update flags based on target
                    if (target == SpectateTarget.Murderer)
                    {
                        isSpectatingMurderer = true;
                        isSpectatingVictim = false;
                    }
                    else if (target == SpectateTarget.Victim)
                    {
                        isSpectatingVictim = true;
                        isSpectatingMurderer = false;
                    }
                }
                else
                {
                    // Not currently spectating, so switch from player to target
                    SwitchToTargetCamera(target);
                    
                    // Update flags based on target
                    if (target == SpectateTarget.Murderer)
                    {
                        isSpectatingMurderer = true;
                        isSpectatingVictim = false;
                    }
                    else if (target == SpectateTarget.Victim)
                    {
                        isSpectatingVictim = true;
                        isSpectatingMurderer = false;
                    }
                }
                
                // Update current target
                currentSpectateTarget = target;
            }
        }

        private static void TeleportPlayerToTarget(SpectateTarget target)
        {
            try
            {
                // Find the target human based on the target type
                Human targetHuman = null;
                if (target == SpectateTarget.Murderer && murderController?.currentMurderer != null)
                {
                    targetHuman = murderController.currentMurderer.GetComponent<Human>();
                }
                else if (target == SpectateTarget.Victim && murderController?.currentVictim != null)
                {
                    targetHuman = murderController.currentVictim.GetComponent<Human>();
                }

                if (targetHuman != null)
                {
                    //Player.Instance.SetHiding(true, null);
                    //Player.Instance.AddLocationOfAuthorty(targetHuman.currentRoom.gameLocation);
                    //notificationController.HUDNotificationsIcon.gameObject.SetActive(false);
                    //crosshairController.maxSize = 0; //200 Default
                    //interfaceControls.enableTooltips = false;
                    //interfaceControls.gameObject.SetActive(false);
                    //Player.Instance.gameObject.GetComponent<Rigidbody>().isKinematic = true;

                    // Teleport the player to the target's position
                  //  Player.Instance.transform.position = targetHuman.transform.position;
                  //  Player.Instance.transform.rotation = targetHuman.transform.rotation;
                  //  KillerCam.Logger.LogInfo($"Teleported player to {targetHuman.GetCitizenName()}");
                }
                else
                {
                    KillerCam.Logger.LogWarning("Could not find target human to teleport player to.");
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error teleporting player to target: {ex.ToString()}");
            }
        }
        
        private static void SwitchToVictimCamera()
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

                if (murderController.currentVictim == null)
                {
                    SwitchToPlayerCamera();
                    SOD.Common.Lib.GameMessage.ShowPlayerSpeech($"No current victim available", 2f, true);                     
                    return;
                }
                
                if (murdererCamera != null)
                {
                    murdererCamera.enabled = true;
                    UpdateMurdererCamera(); // Initial position update
                    
                    // Enable the SpectatorRoomTracker to handle culling
                    if (murderController != null && murderController.currentMurderer != null)
                    {
                        Human murdererHuman = murderController.currentMurderer.GetComponent<Human>();
                        NewRoom murdererRoom = murdererHuman?.currentRoom;
                        
                        if (murdererRoom != null)
                        {
                            // First save and clear player rooms for optimization - Removed, handled by StartSpectating
                            // SpectatorRoomTracker.SaveAndClearPlayerRooms();
                            
                            // Then set up murderer room tracking
                            SpectatorRoomTracker.StartSpectating(murdererRoom);
                            KillerCam.Logger.LogInfo("Activated SpectatorRoomTracker for room: " + murdererRoom.name);
                        }
                    }
                    
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
                
                // Disable the SpectatorRoomTracker
                // SpectatorRoomTracker.StopSpectating(); // Removed redundant call
                
                KillerCam.Logger.LogError("Error switching to murderer camera: " + ex.Message);
            }
        }
        
        
        public static void SwitchToPlayerCamera()
        {
            KillerCam.Logger.LogInfo("Switching back to player camera.");
            Player.Instance.EnablePlayerMovement(true);
            // Get player camera if not already stored
            if (playerCamera == null)
            {
                playerCamera = FindActiveCamera(); // Use FindActiveCamera for consistency
                if (playerCamera == null)
                {
                    KillerCam.Logger.LogError("Could not find player camera!");
                    return;
                }
            }

            // --- Immediate Switch Logic --- 
            // Disable spectator cameras
            if (murdererCamera != null) murdererCamera.enabled = false;
            if (victimCamera != null) victimCamera.enabled = false;
            
            // Enable player camera
            if (playerCamera != null) 
            {
                playerCamera.enabled = true;
                activeCamera = playerCamera; // Set player camera as active immediately
                KillerCam.Logger.LogInfo("Enabled player camera directly.");
            }
            else
            {
                 KillerCam.Logger.LogError("Failed to switch to player camera - Player camera is null.");
                 return; // Critical error if player camera is null
            }
            // --- End Immediate Switch Logic ---
            
            // --- State Reset and Culling --- 
            // Reset spectating states
            isSpectatingMurderer = false;
            isSpectatingVictim = false;
            currentSpectateTarget = SpectateTarget.None;
            lastMurdererRoom = null; // Reset room tracking
            wasSpectatingLastFrame = false;

            // Ensure the spectator UI is hidden
            SpectatorUI.HideText();

            // Restore player controls/UI if they were disabled
            // RestorePlayerFunctionality(); // Keep commented out

             // Reset camera rotation
            rotationX = 0f;
            rotationY = 0f;
            
            // Deactivate room tracking AFTER enabling player camera
            SpectatorRoomTracker.StopSpectating();
                            
            // Explicitly update culling for the player's current room
            if (Player.Instance != null && Player.Instance.currentRoom != null && GeometryCullingController.Instance != null)
            {
                GeometryCullingController.Instance.UpdateCullingForRoom(Player.Instance.currentRoom, true, false, null, true);
                KillerCam.Logger.LogInfo($"Forced culling update for player room: {Player.Instance.currentRoom.name}");
            }
            else
            {
                KillerCam.Logger.LogWarning("Could not force player culling update (Player, Room, or CullingController missing).");
            }

            InterfaceControls interfaceControls = InterfaceControls.Instance;
            if(interfaceControls != null && !interfaceControls.hudCanvas.gameObject.activeSelf)
            {
                interfaceControls.hudCanvas.gameObject.SetActive(true);
                KillerCam.Logger.LogInfo("Interface controls enabled");
            }
            // --- End State Reset and Culling ---
        }

        private static void SwitchToTargetCamera(SpectateTarget targetType)
        {
            try
            {
                // Initialize rotation smoothing variables
                Transform initialTargetTransform = null;
                if (targetType == SpectateTarget.Murderer && murderController?.currentMurderer != null)
                    initialTargetTransform = murderController.currentMurderer.transform;
                else if (targetType == SpectateTarget.Victim && murderController?.currentVictim != null)
                    initialTargetTransform = murderController.currentVictim.transform;

                if (initialTargetTransform != null)
                {
                    Quaternion initialRotation = initialTargetTransform.rotation;
                    Quaternion initialOffsetRotation = initialRotation * Quaternion.Euler(cameraRotationX, cameraYOffset, 0);
                    targetCameraRotation = initialOffsetRotation; // Start smoothing towards the initial offset rotation
                    lastSignificantTargetRotation = initialRotation; // Set the initial significant rotation
                }

                InterfaceControls interfaceControls = InterfaceControls.Instance;
                if(interfaceControls != null && interfaceControls.hudCanvas.gameObject.activeSelf)
                {
                    interfaceControls.hudCanvas.gameObject.SetActive(false);
                    KillerCam.Logger.LogInfo("Interface controls disabled");
                }

                // If a transition is already in progress, don't start a new one
                if (isTransitioning)
                {
                    KillerCam.Logger.LogInfo("Camera transition already in progress, ignoring switch request");
                    return;
                }
                
                // Find the active camera in the scene if needed
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
                
                if (targetType == SpectateTarget.Murderer)
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
                else if (targetType == SpectateTarget.Victim)
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
                            victimCamera.depth = playerCamera.depth + 1; // Render after main camera
                            victimCamera.useOcclusionCulling = false; // Explicitly disable occlusion culling
                        }
                        else
                        {
                            KillerCam.Logger.LogWarning("Could not find player camera to copy settings for VictimCamera.");
                            // Set some default depth if player camera isn't found
                            victimCamera.depth = 1; 
                            victimCamera.useOcclusionCulling = false; // Also disable if using defaults
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
                
                if (targetHuman == null)
                {
                    SwitchToPlayerCamera();
                    SOD.Common.Lib.GameMessage.ShowPlayerSpeech($"No current {targetType} available", 2f, true);                     
                    return;
                }
                
                if (targetCamera != null)
                {
                    // Determine the source camera for the transition
                    Camera sourceCamera;
                    
                    // If we're already spectating, use the current active camera as the source
                    if (isSpectatingMurderer && murdererCamera != null && murdererCamera.enabled)
                    {
                        sourceCamera = murdererCamera;
                        KillerCam.Logger.LogInfo("Using murderer camera as transition source");
                    }
                    else if (isSpectatingVictim && victimCamera != null && victimCamera.enabled)
                    {
                        sourceCamera = victimCamera;
                        KillerCam.Logger.LogInfo("Using victim camera as transition source");
                    }
                    else
                    {
                        // Default to player camera if not currently spectating
                        sourceCamera = playerCamera;
                        KillerCam.Logger.LogInfo("Using player camera as transition source");
                    }
                    
                    // Ensure source camera is not the same as target camera
                    if (sourceCamera == targetCamera)
                    {
                        KillerCam.Logger.LogWarning("Source and target cameras are the same, no transition needed");
                        return;
                    }
                    
                    // Start a smooth transition from the source camera to target camera
                    TransitionCamera(sourceCamera, targetCamera, targetType);
                    
                    // Update Spectator UI Text
                    string targetRole = targetType.ToString();
                    string displayName = targetRole; // Default to role name
 
                    // Attempt to get the actual player name
                    if (targetHuman != null && !string.IsNullOrEmpty(targetHuman.firstName) && !string.IsNullOrEmpty(targetHuman.surName))
                    {
                        displayName = $"{targetRole} ({targetHuman.firstName} {targetHuman.surName})";
                    }
                    else
                    {
                        KillerCam.Logger.LogWarning($"Could not retrieve name for {targetRole}.");
                    }
                    
                    SpectatorUI.UpdateText($"Spectating: {displayName}");
                    
                    // Enable the SpectatorRoomTracker to handle culling
                    if (targetRoom != null)
                    {
                        // Then set up room tracking for the new target
                        SpectatorRoomTracker.StartSpectating(targetRoom);
                        
                        // Force an immediate culling update for the target room
                        // No longer needed here, StartSpectating handles it
                        // GeometryCullingController.Instance.UpdateCullingForRoom(targetRoom, true, false, null, true);
                        KillerCam.Logger.LogInfo("Activated room tracking and forced culling update for room: " + targetRoom.name);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error switching to " + targetType.ToString() + " camera: " + ex.Message);
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
        private static float defaultCameraDistance = 1.5f;  // Default distance behind the target
        private static float minCameraDistance = 0.5f;     // Minimum distance when colliding
        
        // Rotation Smoothing Variables
        private static float rotationThresholdDegrees = 2.0f; // Degrees target must rotate to trigger camera rotation
        private static float rotationSmoothFactor = 5.0f; // Higher value = faster smoothing
        private static Quaternion targetCameraRotation; // The rotation the camera is smoothly moving towards
        private static Quaternion lastSignificantTargetRotation; // Target's rotation when targetCameraRotation was last updated
        
        // Previous manual rotation state for change detection
        private static float prevCameraRotationX = 0f;
        private static float prevCameraYOffset = 0f;
        
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

                if (murderController.currentMurderer == null)
                {
                    SwitchToPlayerCamera();
                    SOD.Common.Lib.GameMessage.ShowPlayerSpeech($"No current murderer available", 2f, true);                     
                    return;
                }
                
                // Calculate the desired position (behind and slightly above the murderer)
                Vector3 targetPosition = murdererTransform.position + new Vector3(0, 1.7f, 0);
                
                // Apply rotation offset from arrow keys
                Quaternion currentTargetRotation = murdererTransform.rotation;
                Quaternion desiredRotation = currentTargetRotation * Quaternion.Euler(cameraRotationX, cameraYOffset, 0);
 
                // Check if target rotation changed significantly OR if manual input changed
                float angleDiff = Quaternion.Angle(lastSignificantTargetRotation, currentTargetRotation);
                bool manualInputChanged = cameraRotationX != prevCameraRotationX || cameraYOffset != prevCameraYOffset;
                
                if (angleDiff > rotationThresholdDegrees || manualInputChanged)
                {
                    targetCameraRotation = desiredRotation; // Update the target rotation for smoothing
                    lastSignificantTargetRotation = currentTargetRotation; // Store this as the last significant rotation
                }
 
                // Get the direction the camera should be looking
                // *** Use instantaneous desiredRotation for position calculation ***
                Vector3 cameraDirection = desiredRotation * Vector3.forward * -1f;
 
                // Default distance from the murderer
                float currentDistance = defaultCameraDistance;
                
                // Only check for collisions if we're not transitioning
                if (!isTransitioning)
                {
                    // Check for collisions using a simple raycast instead of SphereCast (more compatible with IL2CPP)
                    // Cast multiple rays in slightly different directions to simulate a sphere
                    bool hitDetected = false;
                    float closestHitDistance = defaultCameraDistance;
                    
                    // Main raycast straight from the target position
                    RaycastHit hit;
                    if (Physics.Raycast(targetPosition, cameraDirection, out hit, defaultCameraDistance))
                    {
                        hitDetected = true;
                        closestHitDistance = hit.distance;
                    }
                    
                    // Additional raycasts in slightly offset directions to simulate a sphere
                    Vector3[] offsets = new Vector3[] {
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
                
                // Smoothly move the camera to the new position - use a new velocity vector like the victim camera
                Vector3 currentVelocity = Vector3.zero;
                float smoothTime = 0.12f; // Increased from 0.1f for smoother auto-rotation
                
                murdererCameraObject.transform.position = Vector3.SmoothDamp(
                    murdererCameraObject.transform.position,
                    finalPosition,
                    ref currentVelocity,
                    smoothTime
                );
                
                // Smoothly interpolate camera rotation towards the target rotation
                float rotationSmoothTime = rotationSmoothFactor * Time.deltaTime;
                murdererCamera.transform.rotation = Quaternion.Slerp(murdererCamera.transform.rotation, targetCameraRotation, rotationSmoothTime);
                
                // Update culling for the murderer's room if needed
                Human murdererHuman = murderController.currentMurderer.GetComponent<Human>();
                if (murdererHuman != null && murdererHuman.currentRoom != null)
                {
                    SpectatorRoomTracker.UpdateTargetRoom(murdererHuman.currentRoom);
                }
                
                // Update previous manual rotation state
                prevCameraRotationX = cameraRotationX;
                prevCameraYOffset = cameraYOffset;
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

                if (murderController.currentVictim == null)
                {
                    SwitchToPlayerCamera();
                    SOD.Common.Lib.GameMessage.ShowPlayerSpeech($"No current victim available", 2f, true);                     
                    return;
                }
                
                // Calculate the desired position (behind and slightly above the victim)
                Vector3 targetPosition = victimTransform.position + new Vector3(0, 1.7f, 0);
                
                // Apply rotation offset from arrow keys
                Quaternion currentTargetRotation = victimTransform.rotation;
                Quaternion desiredRotation = currentTargetRotation * Quaternion.Euler(cameraRotationX, cameraYOffset, 0);
 
                // Check if target rotation changed significantly OR if manual input changed
                float angleDiff = Quaternion.Angle(lastSignificantTargetRotation, currentTargetRotation);
                bool manualInputChanged = cameraRotationX != prevCameraRotationX || cameraYOffset != prevCameraYOffset;
                
                if (angleDiff > rotationThresholdDegrees || manualInputChanged)
                {
                    targetCameraRotation = desiredRotation; // Update the target rotation for smoothing
                    lastSignificantTargetRotation = currentTargetRotation; // Store this as the last significant rotation
                }
 
                // Get the direction the camera should be looking
                // *** Use instantaneous desiredRotation for position calculation ***
                Vector3 cameraDirection = desiredRotation * Vector3.forward * -1f;
 
                // Default distance from the victim
                float currentDistance = defaultCameraDistance;
                
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
                float smoothDampTime = 0.12f; // Increased from 0.1f for smoother auto-rotation
                
                victimCamera.transform.position = Vector3.SmoothDamp(
                    victimCamera.transform.position,
                    finalPosition,
                    ref currentVelocity,
                    smoothDampTime
                );
                
                // Smoothly interpolate camera rotation towards the target rotation
                float rotationSmoothTime = rotationSmoothFactor * Time.deltaTime;
                victimCamera.transform.rotation = Quaternion.Slerp(victimCamera.transform.rotation, targetCameraRotation, rotationSmoothTime);
                
                // Update culling for the victim's room if needed
                Human victimHuman = murderController.currentVictim.GetComponent<Human>();
                if (victimHuman != null && victimHuman.currentRoom != null)
                {
                    SpectatorRoomTracker.UpdateTargetRoom(victimHuman.currentRoom);
                }
                
                // Update previous manual rotation state
                prevCameraRotationX = cameraRotationX;
                prevCameraYOffset = cameraYOffset;
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error updating victim camera: " + ex.Message);
            }
        }
        
        // Method to create the victim camera if it doesn't exist
         private static void CreateVictimCamera()
        {
            if (victimCameraObject == null)
            {
                victimCameraObject = new GameObject("VictimCamera");
                victimCamera = victimCameraObject.AddComponent<Camera>();

                // Find the player camera to copy settings
                if (playerCamera == null) playerCamera = FindActiveCamera();

                if (playerCamera != null)
                {
                    victimCamera.CopyFrom(playerCamera);
                    victimCamera.depth = playerCamera.depth + 1; // Render after main camera
                    victimCamera.useOcclusionCulling = false; // Explicitly disable occlusion culling
                }
                 else
                {
                     KillerCam.Logger.LogWarning("Could not find player camera to copy settings for VictimCamera.");
                    // Set some default depth if player camera isn't found
                    victimCamera.depth = 1; 
                    victimCamera.useOcclusionCulling = false; // Also disable if using defaults
                }
                victimCamera.enabled = false; // Start disabled
                GameObject.DontDestroyOnLoad(victimCameraObject);
                KillerCam.Logger.LogInfo("Victim camera created.");
            }
        }
        public static Transform GetActiveSpectatorCameraTransform()
        {
            if (isTransitioning && activeCamera != null)
            {
                return activeCamera.transform;
            }
            else if (!isTransitioning)
            {
                switch (currentSpectateTarget)
                {
                    case SpectateTarget.Murderer:
                        return murdererCamera?.transform;
                    case SpectateTarget.Victim:
                        return victimCamera?.transform;
                }
            }
            // Return null if not spectating, camera is null, or in an unexpected state
            return null; 
        }
    }
}