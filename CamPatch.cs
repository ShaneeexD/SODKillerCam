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
        // Static variables to maintain state
        private static bool isSpectatingMurderer = false;
        private static GameObject murdererCameraObject = null;
        private static Camera murdererCamera = null;
        private static Camera originalCamera = null;
        public static MurdererInfoProvider murdererInfoProvider;
        public static MurderController murderController;
        private static UnityEngine.KeyCode toggleKey = UnityEngine.KeyCode.F8; // You can change this to any key you prefer
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppToggleKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.F8; // IL2CPP equivalent
        
        // Track the murderer's last room to avoid unnecessary culling updates
        private static NewRoom lastMurdererRoom = null;
        private static float cullingUpdateCooldown = 0f;

        // IL2CPP key codes for arrow keys
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppUpKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.UpArrow;
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppDownKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.DownArrow;
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppLeftKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.LeftArrow;
        private static BepInEx.Unity.IL2CPP.UnityEngine.KeyCode il2cppRightKey = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.RightArrow;
        
        // Track key state to implement our own key press detection
        private static bool wasKeyPressed = false;
        
        // Flag to indicate if we've already logged camera info
        private static bool hasDumpedCameraInfo = false;
        
        // Camera rotation variables
        private static float rotationX = 0f;
        private static float rotationY = 0f;
        private static float rotationSpeed = 5f;
        
        // Track key states for arrow keys
        private static bool wasUpPressed = false;
        private static bool wasDownPressed = false;
        private static bool wasLeftPressed = false;
        private static bool wasRightPressed = false;
        
        [HarmonyPrefix]
        public static void Prefix(Player __instance)
        {
            // Dump camera information once to help debug
            if (!hasDumpedCameraInfo)
            {
                DumpCameraInfo();
                hasDumpedCameraInfo = true;
            }
            
            // Check for F8 key press
            bool isKeyDown = BepInEx.Unity.IL2CPP.UnityEngine.Input.GetKeyInt(il2cppToggleKey);
            
            // Detect key press (down this frame, but not last frame)
            if (isKeyDown && !wasKeyPressed)
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
                
                // Toggle the camera
                ToggleMurdererCamera();
            }
            wasKeyPressed = isKeyDown;
            
            // Handle arrow key rotation when in murderer camera mode
            if (isSpectatingMurderer && murdererCamera != null)
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
                
                // The rotation is now applied in UpdateMurdererCamera
                // This combines the murderer's base rotation with our manual offset
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
        
        private static void ToggleMurdererCamera()
        {
            if (isSpectatingMurderer)
            {
                // Switch back to player camera
                SwitchToPlayerCamera();
            }
            else
            {
                // Switch to murderer camera
                SwitchToMurdererCamera();
            }
            
            isSpectatingMurderer = !isSpectatingMurderer;
        }
        
        private static void SwitchToMurdererCamera()
        {
            try
            {
                // Find the active camera in the scene
                if (originalCamera == null)
                {
                    // Try to find the active camera
                    originalCamera = FindActiveCamera();
                    KillerCam.Logger.LogInfo("Found active camera: " + (originalCamera != null) + 
                        (originalCamera != null ? ", Name: " + originalCamera.name : ""));
                }
                
                // Create murderer camera if it doesn't exist
                if (murdererCamera == null)
                {
                    CreateMurdererCamera();
                    KillerCam.Logger.LogInfo("Created murderer camera: " + (murdererCamera != null));
                }
                
                // Disable original camera and enable murderer camera
                if (originalCamera != null)
                {
                    originalCamera.enabled = false;
                    KillerCam.Logger.LogInfo("Disabled original camera");
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
                    
                    KillerCam.Logger.LogInfo("Switched to murderer camera. Press " + toggleKey.ToString() + " to switch back.");
                }
            }
            catch (Exception ex)
            {
                // If anything fails, try to revert to original camera
               // isSpectatingMurderer = false;
                
                if (originalCamera != null)
                {
                    originalCamera.enabled = true;
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
        
        private static void SwitchToPlayerCamera()
        {
            try
            {
                // Disable the MurdererRoomTracker first to stop culling management
                MurdererRoomTracker.IsActive = false;
                
                // Restore player rooms that were hidden for optimization
                MurdererRoomTracker.RestorePlayerRooms();
                
                KillerCam.Logger.LogInfo("Deactivated MurdererRoomTracker and restored player rooms");
                
                // Disable murderer camera and enable original camera
                if (murdererCamera != null)
                {
                    murdererCamera.enabled = false;
                    KillerCam.Logger.LogInfo("Disabled murderer camera");
                }
                
                if (originalCamera != null)
                {
                    originalCamera.enabled = true;
                    KillerCam.Logger.LogInfo("Enabled original camera");
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
            }
            catch (Exception ex)
            {
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
        
        private static void CreateMurdererCamera()
        {
            try
            {
                KillerCam.Logger.LogInfo("Creating murderer camera");
                
                // Get murderer location directly from MurderController.Instance
                murderController = MurderController.Instance;
                KillerCam.Logger.LogInfo("MurderController: " + (murderController != null ? "Not null" : "Null"));
                
                Vector3 murdererPos;
                
                if (murderController != null && murderController.currentMurderer != null)
                {
                    murdererPos = murderController.currentMurderer.transform.position;
                    KillerCam.Logger.LogInfo("Using murderer position: " + murdererPos.ToString());
                }
                else
                {
                    // Default position if we can't find the murderer
                    murdererPos = new Vector3(0, 70, 0);
                    KillerCam.Logger.LogInfo("Using default position: " + murdererPos.ToString());
                }
                
                // Create a new camera object
                murdererCameraObject = new GameObject("MurdererCamera");
                murdererCamera = murdererCameraObject.AddComponent<Camera>();
                
                // Set camera to render on top of everything
                murdererCamera.depth = 100; // Higher depth means it renders on top of other cameras
                
                // Position the camera behind and above the murderer (third-person style)
                float heightOffset = 2.5f;  // Higher up for better view
                float backOffset = 1.5f;    // Distance behind the murderer
                
                // Get the murderer's forward direction if available
                Vector3 behindOffset = Vector3.zero;
                if (murderController != null && murderController.currentMurderer != null)
                {
                    // Calculate position behind the murderer based on their forward direction
                    behindOffset = -murderController.currentMurderer.transform.forward * backOffset;
                }
                
                // Set the camera position
                murdererCameraObject.transform.position = new Vector3(
                    murdererPos.x + behindOffset.x, 
                    murdererPos.y + heightOffset, 
                    murdererPos.z + behindOffset.z
                );
                
                // Reset rotation values to defaults
                cameraRotationX = 0f;
                cameraRotationY = 0f;
                
                // Set initial rotation
                if (murderController != null && murderController.currentMurderer != null)
                {
                    // Get the murderer's rotation as a starting point
                    Quaternion murdererRotation = murderController.currentMurderer.transform.rotation;
                    Vector3 murdererEuler = murdererRotation.eulerAngles;
                    
                    // Initialize our rotation tracking variables
                    cameraRotationY = murdererEuler.y; // Horizontal rotation (left/right)
                    cameraRotationX = 0f; // Start with a level view
                    
                    // Apply the initial rotation
                    murdererCameraObject.transform.rotation = Quaternion.Euler(cameraRotationX, cameraRotationY, 0);
                    KillerCam.Logger.LogInfo("Set initial camera rotation: X=" + cameraRotationX + ", Y=" + cameraRotationY);
                }
                
                // Copy settings from the game's camera if possible
                Camera sourceCam = null;
                
                if (CameraController.Instance != null)
                {
                    try
                    {
                        // Try to find the active camera
                        sourceCam = FindActiveCamera();
                        if (sourceCam != null)
                        {
                            KillerCam.Logger.LogInfo("Using found camera as source: " + sourceCam.name);
                        }
                    }
                    catch (Exception ex)
                    {
                        KillerCam.Logger.LogError("Failed to get camera: " + ex.Message);
                    }
                }
                else if (Camera.main != null)
                {
                    sourceCam = Camera.main;
                    KillerCam.Logger.LogInfo("Using Camera.main as source");
                }
                
                if (sourceCam != null)
                {
                    // Copy essential camera settings
                    murdererCamera.fieldOfView = sourceCam.fieldOfView;
                    murdererCamera.nearClipPlane = sourceCam.nearClipPlane;
                    murdererCamera.farClipPlane = sourceCam.farClipPlane;
                    murdererCamera.cullingMask = sourceCam.cullingMask;
                    
                    // Set up occlusion culling settings to prevent rendering issues
                    murdererCamera.useOcclusionCulling = false; // Disable occlusion culling
                    
                    // We won't try to copy components in IL2CPP as GetComponents<T>() is not available
                    KillerCam.Logger.LogInfo("Skipping component copying due to IL2CPP compatibility");
                }
                else
                {
                    // Fallback settings if no source camera is found
                    murdererCamera.fieldOfView = 60f;
                    murdererCamera.nearClipPlane = 0.1f;
                    murdererCamera.farClipPlane = 1000f;
                    murdererCamera.useOcclusionCulling = false; // Disable occlusion culling
                    KillerCam.Logger.LogWarning("No source camera found, using default settings");
                }
                
                // Disable it initially
                murdererCamera.enabled = false;
                
                // Don't destroy when loading new scenes
                GameObject.DontDestroyOnLoad(murdererCameraObject);
                
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
        
        // Method to dump information about available cameras
        private static void DumpCameraInfo()
        {
            try
            {
                KillerCam.Logger.LogInfo("======= CAMERA INFORMATION DUMP =======");
                
                // Log Camera.main info
                if (Camera.main != null)
                {
                    KillerCam.Logger.LogInfo("Camera.main: " + Camera.main.name + ", enabled: " + Camera.main.enabled);
                }
                else
                {
                    KillerCam.Logger.LogInfo("Camera.main is null");
                }
                
                // Check for camera on Player
                if (Player.Instance != null)
                {
                    Camera playerCam = Player.Instance.GetComponentInChildren<Camera>();
                    if (playerCam != null)
                    {
                        KillerCam.Logger.LogInfo("Player camera: " + playerCam.name + ", enabled: " + playerCam.enabled);
                    }
                    else
                    {
                        KillerCam.Logger.LogInfo("No camera found on Player");
                    }
                }
                
                // Try to log CameraController info
                if (CameraController.Instance != null)
                {
                    KillerCam.Logger.LogInfo("CameraController.Instance exists");
                    
                    // Try to get camera component
                    Camera controllerCam = CameraController.Instance.GetComponent<Camera>();
                    if (controllerCam != null)
                    {
                        KillerCam.Logger.LogInfo("CameraController camera: " + controllerCam.name + ", enabled: " + controllerCam.enabled);
                    }
                    else
                    {
                        KillerCam.Logger.LogInfo("No camera component on CameraController");
                    }
                    
                    // Log all fields in CameraController
                    var fields = typeof(CameraController).GetFields();
                    KillerCam.Logger.LogInfo("CameraController fields: " + fields.Length);
                    
                    foreach (var field in fields)
                    {
                        KillerCam.Logger.LogInfo("Field: " + field.Name + ", Type: " + field.FieldType.Name);
                    }
                    
                    // Try to log the cameraObj field if it exists
                    var cameraObjField = typeof(CameraController).GetField("cameraObj");
                    if (cameraObjField != null)
                    {
                        var cameraObj = cameraObjField.GetValue(CameraController.Instance) as GameObject;
                        if (cameraObj != null)
                        {
                            KillerCam.Logger.LogInfo("CameraController.cameraObj: " + cameraObj.name);
                            
                            // Check if it has a camera component
                            Camera camComponent = cameraObj.GetComponent<Camera>();
                            if (camComponent != null)
                            {
                                KillerCam.Logger.LogInfo("Camera on cameraObj: " + camComponent.name + ", enabled: " + camComponent.enabled);
                            }
                        }
                    }
                }
                else
                {
                    KillerCam.Logger.LogInfo("CameraController.Instance is null");
                }
                
                KillerCam.Logger.LogInfo("======= END CAMERA INFORMATION DUMP =======");
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error dumping camera info: " + ex.Message);
            }
        }
        
        // Variables for camera rotation control
        private static float cameraRotationX = 0f;
        private static float cameraRotationY = 0f;
        private static float cameraYOffset = 0f; // Manual offset for left/right rotation
        private static float cameraRotationSpeed = 3.5f;
        
        // Camera collision variables
        private static float defaultCameraDistance = 1.5f;  // Default distance behind the murderer
        private static float minCameraDistance = 0.5f;     // Minimum distance when colliding with objects
        private static float collisionRadius = 0.2f;       // Radius of collision detection
        private static float smoothDampTime = 0.1f;        // Smoothing time for camera movement
        private static Vector3 currentCameraVelocity = Vector3.zero; // For SmoothDamp function
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
                
                Vector3 murdererPos;
                Quaternion murdererRot = Quaternion.identity;
                
                if (murderController != null && murderController.currentMurderer != null)
                {
                    murdererPos = murderController.currentMurderer.transform.position;
                    murdererRot = murderController.currentMurderer.transform.rotation;
                    
                    // Update the camera's Y rotation to match the murderer's rotation
                    // This makes the camera rotate with the murderer when they turn
                    Vector3 murdererEuler = murdererRot.eulerAngles;
                    cameraRotationY = murdererEuler.y;
                }
                else
                {
                    // Default position if we can't find the murderer
                    murdererPos = new Vector3(0, 70, 0);
                    KillerCam.Logger.LogInfo("UpdateMurdererCamera - Using default position: " + murdererPos.ToString());
                }
                
                if (murdererPos != Vector3.zero)
                {
                    // Position the camera behind and above the murderer (third-person style)
                    float heightOffset = 2.5f;  // Higher up for better view
                    
                    // Apply the rotation first (combining murderer's Y rotation with manual X rotation and Y offset)
                    Quaternion cameraRotation = Quaternion.Euler(cameraRotationX, cameraRotationY + cameraYOffset, 0);
                    murdererCamera.transform.rotation = cameraRotation;
                    
                    // Calculate the desired camera position without collision
                    Vector3 cameraDirection = -murdererCamera.transform.forward; // Direction from murderer to camera
                    Vector3 targetPosition = murdererPos + new Vector3(0, heightOffset, 0); // Position above murderer
                    Vector3 desiredPosition = targetPosition + (cameraDirection * defaultCameraDistance);
                    
                    // Perform collision detection using raycast
                    float adjustedDistance = defaultCameraDistance;
                    RaycastHit hit;
                    
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
                    
                    // Calculate the final camera position with collision avoidance
                    Vector3 finalPosition = targetPosition + (cameraDirection * currentDistance);
                    
                    // Smoothly move the camera to the new position
                    murdererCameraObject.transform.position = Vector3.SmoothDamp(
                        murdererCameraObject.transform.position,
                        finalPosition,
                        ref currentCameraVelocity,
                        smoothDampTime
                    );
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error updating murderer camera: " + ex.Message);
            }
        }
    }
}
