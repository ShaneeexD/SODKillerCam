using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Added for potential LINQ usage in replicated logic

namespace KillerCam
{
    // Manages the state for spectating and target room tracking
    public static class SpectatorRoomTracker
    {
        public static NewRoom TargetRoom { get; private set; }
        public static bool IsActive { get; private set; } = false;

        // Store player's room for restoration
        private static NewRoom playerRoom = null;


        // Call this when starting to spectate (murderer or victim)
        public static void StartSpectating(NewRoom target)
        {
            if (target == null)
            {
                KillerCam.Logger.LogError("StartSpectating called with null target room!");
                return;
            }

            if (!IsActive) // Only save player room once when starting spectate mode
            {
                SavePlayerRoom();
            }

            TargetRoom = target;
            IsActive = true;
            KillerCam.Logger.LogInfo($"SpectatorRoomTracker: Started spectating room {TargetRoom?.name}");

            // Trigger an immediate culling update for the new target room
            if (GeometryCullingController.Instance != null)
            {
                // We directly call our custom update logic here
                UpdateCullingForTarget(GeometryCullingController.Instance, TargetRoom, true);
            }
        }

        // Call this when stopping spectating and returning to player view
        public static void StopSpectating()
        {
            if (!IsActive) return;

            IsActive = false;
            TargetRoom = null;
            KillerCam.Logger.LogInfo("SpectatorRoomTracker: Stopped spectating.");
            RestorePlayerRoom(); // This will trigger the game's original culling
        }

        // Call this to update the target room if the spectated entity moves
        public static void UpdateTargetRoom(NewRoom newTargetRoom)
        {
            if (!IsActive || newTargetRoom == null || newTargetRoom == TargetRoom)
                return;

            TargetRoom = newTargetRoom;
            KillerCam.Logger.LogInfo($"SpectatorRoomTracker: Updated target room to {TargetRoom?.name}");

            if (AudioController.Instance != null && AudioController.Instance.playerListener != null && TargetRoom != null)
            {
                // Option 1: Move to spectator camera position (if you have a reference)
                // AudioController.Instance.playerListener.transform.position = spectatorCamera.transform.position;

                // Option 2: Move to target room center (simpler)
                AudioController.Instance.playerListener.transform.position = TargetRoom.middleRoomPosition;
                KillerCam.Logger.LogInfo($"Moved playerListener transform to {TargetRoom.name} center for audio calculation.");
            }

            // Trigger culling update for the new room
            if (GeometryCullingController.Instance != null)
            {
                 // We directly call our custom update logic here
                UpdateCullingForTarget(GeometryCullingController.Instance, TargetRoom, true);
            }
        }


        private static void SavePlayerRoom()
        {
            try
            {
                if (Player.Instance != null)
                {
                    playerRoom = Player.Instance.currentRoom;
                    KillerCam.Logger.LogInfo($"SpectatorRoomTracker: Saved player room: {playerRoom?.name}");
                }
                else {
                    playerRoom = null;
                     KillerCam.Logger.LogWarning("SpectatorRoomTracker: Player instance not found, cannot save room.");
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"SpectatorRoomTracker: Error saving player room: {ex.Message}");
                playerRoom = null;
            }
        }

        private static void RestorePlayerRoom()
        {
            try
            {
                 // Let the original game logic handle restoring the player's view
                if (GeometryCullingController.Instance != null && playerRoom != null)
                {
                    KillerCam.Logger.LogInfo($"SpectatorRoomTracker: Requesting game to restore culling for player room: {playerRoom.name}");
                    // The original method needs all parameters, even if not used for player
                    bool inAirVent = Player.Instance != null && Player.Instance.inAirVent;
                    // Reverting to this access method. This likely causes CS1061.
                    // User needs to find the correct property/method to get the AirDuctGroup when inAirVent is true.
                    // AirDuctGroup currentDuct = Player.Instance?.currentDuctGroup;
                    GeometryCullingController.Instance.UpdateCullingForRoom(playerRoom, true, inAirVent, null, true);
                }
                 else if (GeometryCullingController.Instance != null)
                {
                    // If we couldn't save the player room, try using the player's current room if available
                    NewRoom currentPlyRoom = Player.Instance?.currentRoom;
                    if (currentPlyRoom != null) {
                        KillerCam.Logger.LogWarning($"SpectatorRoomTracker: No saved player room, using current: {currentPlyRoom.name}");
                        bool inAirVent = Player.Instance.inAirVent;
                        // Reverting to this access method. This likely causes CS1061.
                        // User needs to find the correct property/method.
                        // AirDuctGroup currentDuct = Player.Instance.currentDuctGroup;
                        GeometryCullingController.Instance.UpdateCullingForRoom(currentPlyRoom, true, inAirVent, null, true);
                    } else {
                         KillerCam.Logger.LogError("SpectatorRoomTracker: Cannot restore player room - no saved room and player has no current room.");
                    }
                }
                else {
                     KillerCam.Logger.LogError("SpectatorRoomTracker: Cannot restore player room - GeometryCullingController instance not found.");
                }
                playerRoom = null; // Clear saved room after attempting restore
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"SpectatorRoomTracker: Error restoring player room: {ex.Message}");
                playerRoom = null; // Clear saved room on error
            }
        }

        // This method replicates the game's UpdateCullingForRoom logic, but uses targetRoom as the source
        public static void UpdateCullingForTarget(GeometryCullingController controller, NewRoom targetRoom, bool immediateLoad)
        {
             if (controller == null) {
                 KillerCam.Logger.LogError("UpdateCullingForTarget: GeometryCullingController instance is null!");
                 return;
             }
             if (targetRoom == null) {
                 KillerCam.Logger.LogError("UpdateCullingForTarget: targetRoom is null!");
                 return; // Cannot cull for a null room
             }

             KillerCam.Logger.LogInfo($"SpectatorRoomTracker: Updating culling for target room: {targetRoom.name}");

            try
            {
                // Mimic the start of the original UpdateCullingForRoom
                controller.currentRoomsCullingTree.Clear();
                controller.currentRoomsCullingWithImmediateStuffLoad.Clear();
                controller.currentDuctsCullingTree.Clear();

                // --- Replicate Logic from Original UpdateCullingForRoom ---
                // IMPORTANT: Replace references to Player.Instance.currentRoom (when used for culling basis)
                // with 'targetRoom'. Keep Player.Instance references for things like position checks if needed,
                // or potentially use the spectated character's position if available and relevant.

                // --- Active Murders ---
                if (!SessionData.Instance.isFloorEdit && !SessionData.Instance.isTestScene)
                {
                     if (MurderController.Instance != null && MurderController.Instance.activeMurders != null)
                     {
                        foreach (MurderController.Murder murder in MurderController.Instance.activeMurders)
                        {
                             if (murder?.cullingActiveRooms != null)
                             {
                                foreach (int key in murder.cullingActiveRooms)
                                {
                                    NewRoom room = null;
                                    if (CityData.Instance.roomDictionary.TryGetValue(key, out room) && room != null)
                                    {
                                        controller.currentRoomsCullingTree.Add(room);
                                        controller.currentRoomsCullingWithImmediateStuffLoad.Add(room);
                                    }
                                }
                             }
                        }
                     } else {
                          KillerCam.Logger.LogWarning("MurderController instance or activeMurders is null.");
                     }

                    // --- Active Ragdolls ---
                     if (GameplayController.Instance != null && GameplayController.Instance.activeRagdolls != null)
                     {
                        foreach (Human human in GameplayController.Instance.activeRagdolls)
                        {
                            if (human?.currentRoom != null)
                            {
                                if (!controller.currentRoomsCullingTree.Contains(human.currentRoom))
                                {
                                    controller.currentRoomsCullingTree.Add(human.currentRoom);
                                    controller.currentRoomsCullingWithImmediateStuffLoad.Add(human.currentRoom);
                                }
                                if (human.currentRoom.adjacentRooms != null) {
                                    foreach (NewRoom newRoom2 in human.currentRoom.adjacentRooms)
                                    {
                                        if (newRoom2 != null && !controller.currentRoomsCullingTree.Contains(newRoom2))
                                        {
                                            controller.currentRoomsCullingTree.Add(newRoom2);
                                            controller.currentRoomsCullingWithImmediateStuffLoad.Add(newRoom2);
                                        }
                                    }
                                }
                            }
                        }
                     } else {
                          KillerCam.Logger.LogWarning("GameplayController instance or activeRagdolls is null.");
                     }


                    // --- Active Physics Interactables ---
                     if (GameplayController.Instance != null && GameplayController.Instance.activePhysics != null)
                     {
                        foreach (Interactable interactable in GameplayController.Instance.activePhysics)
                        {
                             if (interactable != null) {
                                if (interactable.node == null)
                                {
                                    interactable.UpdateWorldPositionAndNode(false, false);
                                    if (interactable.node == null) continue; // Skip if still null
                                }

                                if (interactable.node?.room != null) {
                                    if (!controller.currentRoomsCullingTree.Contains(interactable.node.room))
                                    {
                                        controller.currentRoomsCullingTree.Add(interactable.node.room);
                                        controller.currentRoomsCullingWithImmediateStuffLoad.Add(interactable.node.room);
                                    }

                                    if (interactable.node.room.adjacentRooms != null) {
                                        foreach (NewRoom newRoom3 in interactable.node.room.adjacentRooms)
                                        {
                                            if (newRoom3 != null && !controller.currentRoomsCullingTree.Contains(newRoom3))
                                            {
                                                controller.currentRoomsCullingTree.Add(newRoom3);
                                                controller.currentRoomsCullingWithImmediateStuffLoad.Add(newRoom3);
                                            }
                                        }
                                    }
                                }
                             }
                        }
                     } else {
                          KillerCam.Logger.LogWarning("GameplayController instance or activePhysics is null.");
                     }


                    // --- Room Culling (Main Part) ---
                    // We skip the 'inAirVent' logic for spectators for now, assume they are not in vents.
                    // If the target *could* be in a vent, this needs adapting.

                    if (Game.Instance.enableNewRealtimeTimeCullingSystem)
                    {
                        // If the game uses the dynamic system, replicate that call for the target room
                        controller.GenerateDynamicCulling(targetRoom, 0);
                        KillerCam.Logger.LogInfo($"Using game's dynamic culling for target: {targetRoom.name}");
                    }
                    else
                    {
                        // Use the pre-calculated Culling Tree (most likely case)
                        if (!targetRoom.completedTreeCull && !targetRoom.loadedCullTreeFromSave)
                        {
                            // It's unlikely we should generate this on the fly here,
                            // it might be computationally expensive. Log a warning if needed.
                             KillerCam.Logger.LogWarning($"Target room {targetRoom.name} culling tree not generated/loaded. Visibility might be incomplete.");
                            // targetRoom.GenerateCullingTree(false); // Potentially very slow! Avoid if possible.
                        }

                        if (targetRoom.cullingTree == null) {
                             KillerCam.Logger.LogError($"Target room {targetRoom.name} has a null culling tree!");
                        }
                        else if (targetRoom.cullingTree.Count <= 0 && !SessionData.Instance.isTestScene)
                        {
                             KillerCam.Logger.LogWarning($"Target room {targetRoom.name} culling tree count is zero!");
                        }
                        else if (targetRoom.cullingTree != null)
                        {
                            // Process the culling tree like the original method
                            foreach (Il2CppSystem.Collections.Generic.KeyValuePair<NewRoom, Il2CppSystem.Collections.Generic.List<NewRoom.CullTreeEntry>> keyValuePair2 in targetRoom.cullingTree)
                            {
                                if (keyValuePair2.Key != null && !controller.currentRoomsCullingTree.Contains(keyValuePair2.Key))
                                {
                                    if (keyValuePair2.Value == null) continue; // Safety check

                                    foreach (NewRoom.CullTreeEntry cullTreeEntry2 in keyValuePair2.Value)
                                    {
                                        bool shouldAddRoom = false;
                                        if (cullTreeEntry2.requiredOpenDoors == null || cullTreeEntry2.requiredOpenDoors.Count <= 0)
                                        {
                                            shouldAddRoom = true;
                                        }
                                        else
                                        {
                                            bool flag2 = true;
                                            foreach (int key3 in cullTreeEntry2.requiredOpenDoors)
                                            {
                                                NewDoor newDoor2 = null;
                                                if (CityData.Instance.doorDictionary.TryGetValue(key3, out newDoor2))
                                                {
                                                    if (newDoor2.isClosed && !newDoor2.peekedUnder)
                                                    {
                                                        flag2 = false;
                                                        break;
                                                    }
                                                }
                                                else {
                                                     // Game logs error here, we can too
                                                     KillerCam.Logger.LogError($"Cannot find door {key3} required by culling tree of {targetRoom.name} for room {keyValuePair2.Key.name}");
                                                }
                                            }
                                            if (flag2) {
                                                shouldAddRoom = true;
                                            }
                                        }

                                        if (shouldAddRoom)
                                        {
                                            controller.currentRoomsCullingTree.Add(keyValuePair2.Key);

                                            // Handle Atriums
                                            if (keyValuePair2.Key.atriumTop != null && !controller.currentRoomsCullingTree.Contains(keyValuePair2.Key.atriumTop))
                                            {
                                                controller.currentRoomsCullingTree.Add(keyValuePair2.Key.atriumTop);
                                            }
                                            if (keyValuePair2.Key.atriumRooms != null) {
                                                foreach (NewRoom newRoom7 in keyValuePair2.Key.atriumRooms)
                                                {
                                                    if (newRoom7 != null && !controller.currentRoomsCullingTree.Contains(newRoom7))
                                                    {
                                                        controller.currentRoomsCullingTree.Add(newRoom7);
                                                    }
                                                }
                                            }

                                            // Handle Streets (Shared Ground Elements)
                                            if (keyValuePair2.Key.gameLocation?.thisAsStreet != null && keyValuePair2.Key.gameLocation.thisAsStreet.sharedGroundElements != null)
                                            {
                                                foreach (StreetController streetController2 in keyValuePair2.Key.gameLocation.thisAsStreet.sharedGroundElements)
                                                {
                                                    if (streetController2?.rooms != null) {
                                                        foreach (NewRoom newRoom8 in streetController2.rooms)
                                                        {
                                                            if (newRoom8 != null && !controller.currentRoomsCullingTree.Contains(newRoom8))
                                                            {
                                                                controller.currentRoomsCullingTree.Add(newRoom8);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            // Once added, no need to check other entries for the same room
                                            break;
                                        }
                                    } // End foreach CullTreeEntry
                                } // End if !Contains(Key)

                                // Add associated ducts regardless of door state? (Matches original)
                                if (keyValuePair2.Key?.ductGroups != null) {
                                    foreach (AirDuctGroup airDuctGroup3 in keyValuePair2.Key.ductGroups)
                                    {
                                        if (airDuctGroup3 != null && !controller.currentDuctsCullingTree.Contains(airDuctGroup3))
                                        {
                                            controller.currentDuctsCullingTree.Add(airDuctGroup3);
                                        }
                                    }
                                }
                            } // End foreach KeyValuePair in cullingTree
                        }
                         KillerCam.Logger.LogInfo($"Processed culling tree for target: {targetRoom.name}. Found {controller.currentRoomsCullingTree.Count} rooms.");
                    } // End else (use Culling Tree)


                    // --- Exterior Ducts ---
                    // Use targetRoom's position for distance check
                    if (HighlanderSingleton<CityBuildings>.Instance?.buildingDirectory != null) {
                        foreach (NewBuilding newBuilding in HighlanderSingleton<CityBuildings>.Instance.buildingDirectory)
                        {
                             if (newBuilding != null && newBuilding.displayBuildingModel && targetRoom?.middleRoomPosition != null && newBuilding.transform != null && CullingControls.Instance != null &&
                                Vector3.Distance(targetRoom.middleRoomPosition, newBuilding.transform.position) < CullingControls.Instance.exteriorDuctCullingRange)
                             {
                                 if (newBuilding.airDucts != null) {
                                    foreach (AirDuctGroup airDuctGroup4 in newBuilding.airDucts)
                                    {
                                        if (airDuctGroup4 != null && airDuctGroup4.isExterior && !controller.currentDuctsCullingTree.Contains(airDuctGroup4))
                                        {
                                            controller.currentDuctsCullingTree.Add(airDuctGroup4);
                                        }
                                    }
                                 }
                             }
                        }
                    } else {
                         KillerCam.Logger.LogWarning("CityBuildings instance or buildingDirectory is null.");
                    }


                    // --- Ensure Target Room is in Immediate Load List ---
                    // Add the primary target room itself for immediate loading if needed
                     if (ObjectPoolingController.Instance != null && ObjectPoolingController.Instance.allowGradualRoomLoading && !controller.currentRoomsCullingWithImmediateStuffLoad.Contains(targetRoom))
                     {
                        controller.currentRoomsCullingWithImmediateStuffLoad.Add(targetRoom);
                     } else if (ObjectPoolingController.Instance == null) {
                          KillerCam.Logger.LogWarning("ObjectPoolingController instance is null.");
                     }

                } // End if (!isFloorEdit && !isTestScene)
                else {
                     // Handle FloorEdit/TestScene culling if necessary (mirroring original)
                    if (SessionData.Instance.isFloorEdit && FloorEditController.Instance?.editFloor?.addresses != null)
                    {
                        foreach (NewAddress newAddress in FloorEditController.Instance.editFloor.addresses)
                        {
                            if (newAddress?.rooms != null) {
                                foreach (NewRoom newRoom9 in newAddress.rooms)
                                {
                                    if (newRoom9 != null) controller.currentRoomsCullingTree.Add(newRoom9);
                                }
                            }
                        }
                         KillerCam.Logger.LogInfo("Using FloorEdit culling logic.");
                    } else if (SessionData.Instance.isTestScene) {
                         KillerCam.Logger.LogInfo("TestScene: Culling might be simplified/disabled.");
                    }
                }

                // --- Execute Culling ---
                // Call the original method to apply the changes based on the populated lists
                 KillerCam.Logger.LogInfo($"Executing culling tree. Rooms: {controller.currentRoomsCullingTree.Count}, Ducts: {controller.currentDuctsCullingTree.Count}");
                controller.ExecuteCurrentCullingTree(immediateLoad);
                 KillerCam.Logger.LogInfo("Finished executing culling tree for target.");

            }
            catch (Exception ex)
            {
                 KillerCam.Logger.LogError($"Error in UpdateCullingForTarget for room {targetRoom?.name}: {ex.Message}\n{ex.StackTrace}");
                 // As a fallback, maybe try restoring player view? Or just log the error.
                 // RestorePlayerRoom(); // Potentially risky if called repeatedly on error
            }
        }
    }

    // Patches the game's GeometryCullingController.UpdateCullingForRoom method
    [HarmonyPatch(typeof(GeometryCullingController), "UpdateCullingForRoom")]
    public class GeometryCullingPatch
    {
        // Prefix runs before the original method
        [HarmonyPrefix]
        public static bool Prefix(GeometryCullingController __instance, ref NewRoom currentRoom, bool updateSound, bool inAirVent, AirDuctGroup currentDuct, bool immediateLoad)
        {
            // If we are actively spectating a target, intercept and handle culling ourselves
            if (SpectatorRoomTracker.IsActive && SpectatorRoomTracker.TargetRoom != null)
            {
                try
                {
                     KillerCam.Logger.LogInfo($"GeometryCullingPatch: Intercepting UpdateCullingForRoom for target: {SpectatorRoomTracker.TargetRoom.name}");
                     // Call our custom culling logic using the target room
                     SpectatorRoomTracker.UpdateCullingForTarget(__instance, SpectatorRoomTracker.TargetRoom, immediateLoad);

                     // Skip the original game method entirely
                    return false;
                }
                catch (Exception ex)
                {
                     KillerCam.Logger.LogError($"GeometryCullingPatch: Error in Prefix while spectating: {ex.Message}\n{ex.StackTrace}");
                     // Fallback: Let the original method run if our logic failed catastrophically
                    return true;
                }
            }

            // If not spectating, or target is null, let the original game method run normally
             // KillerCam.Logger.LogInfo($"GeometryCullingPatch: Allowing original UpdateCullingForRoom for player room: {currentRoom?.name}");
            return true;
        }
    }
    // No longer need to patch ExecuteCurrentCullingTree as we call it directly after our custom logic
}