using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace KillerCam
{
    // Static class to track murderer room information
    public static class MurdererRoomTracker
    {
        public static NewRoom MurdererRoom { get; private set; }
        public static List<NewRoom> AdjacentRooms { get; private set; } = new List<NewRoom>();
        public static List<NewRoom> ExtendedVisibilityRooms { get; private set; } = new List<NewRoom>();
        public static bool IsActive { get; set; } = false;
        
        // Track rooms that we've manually added to the culling tree
        private static HashSet<NewRoom> addedRooms = new HashSet<NewRoom>();
        
        // Store player room information for restoration when switching back
        private static NewRoom playerRoom = null;
        private static List<NewRoom> playerAdjacentRooms = new List<NewRoom>();
        private static HashSet<NewRoom> playerVisibleRooms = new HashSet<NewRoom>();
        
        // Maximum depth for room exploration (how many rooms away to include)
        private static int maxRoomDepth = 3;
        
        public static void UpdateMurdererRoom(NewRoom room)
        {
            MurdererRoom = room;
            AdjacentRooms.Clear();
            ExtendedVisibilityRooms.Clear();
            addedRooms.Clear();
            
            if (room == null)
                return;
            
            // Add adjacent rooms (direct connections)
            if (room.adjacentRooms != null)
            {
                foreach (var adjacentRoom in room.adjacentRooms)
                {
                    if (adjacentRoom != null && !AdjacentRooms.Contains(adjacentRoom))
                    {
                        AdjacentRooms.Add(adjacentRoom);
                    }
                }
            }
            
            // Add extended visibility rooms (rooms further away)
            CollectExtendedRooms(room, 2);
            
            // Add rooms above and below (vertical adjacency)
            CollectVerticallyAdjacentRooms(room);
            
            KillerCam.Logger.LogInfo($"MurdererRoomTracker: Updated murderer room to {(room != null ? room.name : "null")}");
            KillerCam.Logger.LogInfo($"MurdererRoomTracker: Tracking {AdjacentRooms.Count} adjacent rooms and {ExtendedVisibilityRooms.Count} extended visibility rooms");
        }
        
        // Checks if a destination room should be visible from an adjacent room (based on game's logic)
        private static bool IsRoomRenderableFromThisRoom(NewRoom adjacentRoom, NewRoom originRoom, NewRoom destinationRoom, NewNode.NodeAccess access)
        {
            if (adjacentRoom == null || originRoom == null || destinationRoom == null || access == null)
                return true;
                
            try
            {
                // Check if rooms have nodes
                bool adjacentHasNodes = adjacentRoom.nodes != null && adjacentRoom.nodes.Count > 0;
                
                // Get first node from each room using foreach to avoid LINQ extension methods
                NewNode originNode = null;
                if (originRoom.nodes != null && originRoom.nodes.Count > 0)
                {
                    foreach (var node in originRoom.nodes)
                    {
                        originNode = node;
                        break;
                    }
                }
                
                NewNode destNode = null;
                if (destinationRoom.nodes != null && destinationRoom.nodes.Count > 0)
                {
                    foreach (var node in destinationRoom.nodes)
                    {
                        destNode = node;
                        break;
                    }
                }
                
                if (!adjacentHasNodes || destNode == null)
                    return true;
                    
                // Check outside/inside relationships
                bool adjacentIsOutside = adjacentRoom.IsOutside();
                bool destIsOutside = destinationRoom.IsOutside();
                
                // Special case: Looking from outside to inside through window/door on different floor
                if (adjacentIsOutside && !destIsOutside && 
                    (access.accessType == NewNode.NodeAccess.AccessType.window || access.accessType == NewNode.NodeAccess.AccessType.door) && 
                    originNode != null && originNode.nodeCoord.z != destNode.nodeCoord.z)
                {
                    KillerCam.Logger.LogInfo($"Room {destinationRoom.name} not renderable from {adjacentRoom.name} through {access.accessType}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error in IsRoomRenderableFromThisRoom: {ex.Message}");
                return true; // Default to visible in case of error
            }
        }
        
        // Collect rooms that are further away (up to maxDepth rooms away)
        private static void CollectExtendedRooms(NewRoom startRoom, int maxDepth)
        {
            if (startRoom == null || maxDepth <= 0)
                return;
                
            HashSet<NewRoom> visited = new HashSet<NewRoom>();
            Queue<RoomDepthPair> queue = new Queue<RoomDepthPair>();
            Dictionary<NewRoom, NewNode.NodeAccess> roomAccessPoints = new Dictionary<NewRoom, NewNode.NodeAccess>();
            
            // Start with the adjacent rooms and find their access points
            visited.Add(startRoom);
            if (startRoom.entrances != null)
            {
                foreach (var entrance in startRoom.entrances)
                {
                    if (entrance != null)
                    {
                        NewRoom otherRoom = entrance.GetOtherRoom(startRoom);
                        if (otherRoom != null && !visited.Contains(otherRoom))
                        {
                            visited.Add(otherRoom);
                            AdjacentRooms.Add(otherRoom);
                            roomAccessPoints[otherRoom] = entrance;
                            queue.Enqueue(new RoomDepthPair(otherRoom, 1));
                        }
                    }
                }
            }
            
            // BFS to find rooms up to maxDepth away
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                if (current.depth >= maxDepth)
                    continue;
                    
                if (current.room.entrances != null)
                {
                    foreach (var entrance in current.room.entrances)
                    {
                        if (entrance != null)
                        {
                            NewRoom nextRoom = entrance.GetOtherRoom(current.room);
                            if (nextRoom != null && !visited.Contains(nextRoom))
                            {
                                // Check if this room should be visible based on game's rules
                                bool isRenderable = true;
                                if (roomAccessPoints.ContainsKey(current.room))
                                {
                                    isRenderable = IsRoomRenderableFromThisRoom(
                                        current.room, 
                                        startRoom, 
                                        nextRoom, 
                                        entrance);
                                }
                                
                                if (isRenderable)
                                {
                                    visited.Add(nextRoom);
                                    ExtendedVisibilityRooms.Add(nextRoom);
                                    roomAccessPoints[nextRoom] = entrance;
                                    queue.Enqueue(new RoomDepthPair(nextRoom, current.depth + 1));
                                }
                            }
                        }
                    }
                }
            }
        }
        
        // Helper class for BFS room exploration
        private class RoomDepthPair
        {
            public NewRoom room;
            public int depth;
            
            public RoomDepthPair(NewRoom room, int depth)
            {
                this.room = room;
                this.depth = depth;
            }
        }
        
        // Find rooms that are vertically adjacent (above/below)
        private static void CollectVerticallyAdjacentRooms(NewRoom room)
        {
            if (room == null)
                return;
                
            try
            {
                // Look for rooms that might be staircases or vertically connected
                foreach (var potentialRoom in CityData.Instance.roomDirectory)
                {
                    if (potentialRoom == null || potentialRoom == room || 
                        AdjacentRooms.Contains(potentialRoom) || 
                        ExtendedVisibilityRooms.Contains(potentialRoom))
                        continue;
                    
                    // Check if this room is a staircase or has "stair" in its name
                    bool isStaircase = potentialRoom.name.ToLower().Contains("stair") || 
                                      (potentialRoom.gameLocation != null && 
                                       potentialRoom.gameLocation.name.ToLower().Contains("stair"));
                    
                    // Check if this room is vertically aligned with our room
                    bool isVerticallyAligned = false;
                    
                    // Vector3 is a struct and can't be null, so we don't need to check for null
                    Vector2 roomPos2D = new Vector2(room.middleRoomPosition.x, room.middleRoomPosition.z);
                    Vector2 potentialPos2D = new Vector2(potentialRoom.middleRoomPosition.x, potentialRoom.middleRoomPosition.z);
                    
                    // Check if they're roughly in the same X,Z position (vertically aligned)
                    float horizontalDistance = Vector2.Distance(roomPos2D, potentialPos2D);
                    float verticalDistance = Mathf.Abs(room.middleRoomPosition.y - potentialRoom.middleRoomPosition.y);
                    
                    // If they're close horizontally but separated vertically
                    isVerticallyAligned = horizontalDistance < 5f && verticalDistance > 0.5f && verticalDistance < 10f;
                    
                    // If it's a staircase or vertically aligned, add it
                    if (isStaircase || isVerticallyAligned)
                    {
                        ExtendedVisibilityRooms.Add(potentialRoom);
                        KillerCam.Logger.LogInfo($"Added vertical room: {potentialRoom.name} (Staircase: {isStaircase}, Vertical: {isVerticallyAligned})");
                    }
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error collecting vertical rooms: {ex.Message}");
            }
        }
        
        public static void AddRoomToCullingTree(GeometryCullingController controller, NewRoom room)
        {
            if (room == null || controller == null)
                return;
                
            // Add to culling tree
            if (!controller.currentRoomsCullingTree.Contains(room))
            {
                controller.currentRoomsCullingTree.Add(room);
                addedRooms.Add(room);
                KillerCam.Logger.LogInfo($"MurdererRoomTracker: Added room {room.name} to culling tree");
            }
            
            // Add to immediate stuff load list to ensure furniture and objects are loaded
            if (controller.currentRoomsCullingWithImmediateStuffLoad != null && 
                !controller.currentRoomsCullingWithImmediateStuffLoad.Contains(room))
            {
                controller.currentRoomsCullingWithImmediateStuffLoad.Add(room);
                KillerCam.Logger.LogInfo($"MurdererRoomTracker: Added room {room.name} to immediate stuff load list");
            }
            
            // Add to visible rooms list
            if (!CityData.Instance.visibleRooms.Contains(room))
            {
                CityData.Instance.visibleRooms.Add(room);
                KillerCam.Logger.LogInfo($"MurdererRoomTracker: Added room {room.name} to visible rooms");
            }
        }
        
        // Save the player's current room information and clear player rooms for optimization
        public static void SaveAndClearPlayerRooms()
        {
            try
            {
                // Save player's current room
                if (Player.Instance != null)
                {
                    playerRoom = Player.Instance.currentRoom;
                    playerAdjacentRooms.Clear();
                    playerVisibleRooms.Clear();
                    
                    // Save adjacent rooms
                    if (playerRoom != null && playerRoom.adjacentRooms != null)
                    {
                        foreach (var room in playerRoom.adjacentRooms)
                        {
                            if (room != null)
                            {
                                playerAdjacentRooms.Add(room);
                            }
                        }
                    }
                    
                    // Save currently visible rooms
                    if (CityData.Instance != null && CityData.Instance.visibleRooms != null)
                    {
                        foreach (var room in CityData.Instance.visibleRooms)
                        {
                            if (room != null && room != MurdererRoom && 
                                !AdjacentRooms.Contains(room) && 
                                !ExtendedVisibilityRooms.Contains(room))
                            {
                                playerVisibleRooms.Add(room);
                            }
                        }
                    }
                    
                    // Clear player rooms from visibility
                    ClearPlayerRooms();
                    
                    KillerCam.Logger.LogInfo($"Saved player room info: {playerRoom?.name}, {playerAdjacentRooms.Count} adjacent rooms, {playerVisibleRooms.Count} visible rooms");
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error saving player room info: {ex.Message}");
            }
        }
        
        // Clear player rooms from visibility for optimization
        private static void ClearPlayerRooms()
        {
            try
            {
                // Hide player's room and adjacent rooms if they're not part of murderer's view
                if (playerRoom != null && playerRoom != MurdererRoom && 
                    !AdjacentRooms.Contains(playerRoom) && 
                    !ExtendedVisibilityRooms.Contains(playerRoom))
                {
                    playerRoom.SetVisible(false, false, "KillerCam optimization", true, true);
                    KillerCam.Logger.LogInfo($"Hid player room {playerRoom.name} for optimization");
                }
                
                foreach (var room in playerAdjacentRooms)
                {
                    if (room != null && room != MurdererRoom && 
                        !AdjacentRooms.Contains(room) && 
                        !ExtendedVisibilityRooms.Contains(room))
                    {
                        room.SetVisible(false, false, "KillerCam optimization", true, true);
                        KillerCam.Logger.LogInfo($"Hid player adjacent room {room.name} for optimization");
                    }
                }
                
                // Clear other rooms that were visible due to player but aren't needed for murderer view
                foreach (var room in playerVisibleRooms)
                {
                    if (room != null)
                    {
                        room.SetVisible(false, false, "KillerCam optimization", true, true);
                    }
                }
                
                // If we have access to the GeometryCullingController, update it
                if (GeometryCullingController.Instance != null && MurdererRoom != null)
                {
                    GeometryCullingController.Instance.UpdateCullingForRoom(MurdererRoom, true, false, null, true);
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error clearing player rooms: {ex.Message}");
            }
        }
        
        // Restore player rooms when switching back to player view
        public static void RestorePlayerRooms()
        {
            try
            {
                if (playerRoom != null)
                {
                    // Make player room and adjacent rooms visible again
                    playerRoom.SetVisible(true, false, "KillerCam restore", true, true);
                    
                    foreach (var room in playerAdjacentRooms)
                    {
                        if (room != null)
                        {
                            room.SetVisible(true, false, "KillerCam restore", true, true);
                        }
                    }
                    
                    // Update culling for player's room to restore proper visibility
                    if (GeometryCullingController.Instance != null)
                    {
                        GeometryCullingController.Instance.UpdateCullingForRoom(playerRoom, true, false, null, true);
                    }
                    
                    KillerCam.Logger.LogInfo($"Restored player room visibility for {playerRoom.name}");
                }
                
                // Clear saved data
                playerRoom = null;
                playerAdjacentRooms.Clear();
                playerVisibleRooms.Clear();
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error restoring player rooms: {ex.Message}");
            }
        }
        
        // Comprehensive culling update that mimics the game's UpdateCullingForRoom method but for the murderer's perspective
        public static void UpdateComprehensiveCulling(GeometryCullingController controller, bool immediateLoad)
        {
            if (!IsActive || MurdererRoom == null || controller == null)
                return;
                
            try
            {
                // First, clear the culling trees
                controller.currentRoomsCullingTree.Clear();
                controller.currentRoomsCullingWithImmediateStuffLoad.Clear();
                controller.currentDuctsCullingTree.Clear();
                
                // Add the murderer's room and its adjacent rooms
                controller.currentRoomsCullingTree.Add(MurdererRoom);
                controller.currentRoomsCullingWithImmediateStuffLoad.Add(MurdererRoom);
                
                foreach (var adjacentRoom in AdjacentRooms)
                {
                    if (adjacentRoom != null)
                    {
                        controller.currentRoomsCullingTree.Add(adjacentRoom);
                        controller.currentRoomsCullingWithImmediateStuffLoad.Add(adjacentRoom);
                    }
                }
                
                foreach (var extendedRoom in ExtendedVisibilityRooms)
                {
                    if (extendedRoom != null)
                    {
                        controller.currentRoomsCullingTree.Add(extendedRoom);
                        controller.currentRoomsCullingWithImmediateStuffLoad.Add(extendedRoom);
                    }
                }
                
                // Add rooms with active murders (similar to the original method)
                if (MurderController.Instance != null && MurderController.Instance.activeMurders != null)
                {
                    foreach (var murder in MurderController.Instance.activeMurders)
                    {
                        if (murder != null && murder.cullingActiveRooms != null)
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
                }
                
                // Add rooms with active ragdolls (dead bodies)
                if (GameplayController.Instance != null && GameplayController.Instance.activeRagdolls != null)
                {
                    foreach (var human in GameplayController.Instance.activeRagdolls)
                    {
                        if (human != null && human.currentRoom != null)
                        {
                            if (!controller.currentRoomsCullingTree.Contains(human.currentRoom))
                            {
                                controller.currentRoomsCullingTree.Add(human.currentRoom);
                                controller.currentRoomsCullingWithImmediateStuffLoad.Add(human.currentRoom);
                            }
                            
                            // Also add adjacent rooms
                            if (human.currentRoom.adjacentRooms != null)
                            {
                                foreach (var room in human.currentRoom.adjacentRooms)
                                {
                                    if (room != null && !controller.currentRoomsCullingTree.Contains(room))
                                    {
                                        controller.currentRoomsCullingTree.Add(room);
                                        controller.currentRoomsCullingWithImmediateStuffLoad.Add(room);
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Add rooms with active interactable objects
                if (GameplayController.Instance != null && GameplayController.Instance.activePhysics != null)
                {
                    foreach (var interactable in GameplayController.Instance.activePhysics)
                    {
                        if (interactable != null)
                        {
                            // Make sure the node is updated
                            if (interactable.node == null)
                            {
                                interactable.UpdateWorldPositionAndNode(false, false);
                            }
                            
                            if (interactable.node != null && interactable.node.room != null)
                            {
                                if (!controller.currentRoomsCullingTree.Contains(interactable.node.room))
                                {
                                    controller.currentRoomsCullingTree.Add(interactable.node.room);
                                    controller.currentRoomsCullingWithImmediateStuffLoad.Add(interactable.node.room);
                                }
                                
                                // Also add adjacent rooms
                                if (interactable.node.room.adjacentRooms != null)
                                {
                                    foreach (var room in interactable.node.room.adjacentRooms)
                                    {
                                        if (room != null && !controller.currentRoomsCullingTree.Contains(room))
                                        {
                                            controller.currentRoomsCullingTree.Add(room);
                                            controller.currentRoomsCullingWithImmediateStuffLoad.Add(room);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Process the murderer's culling tree (similar to how the game processes the player's culling tree)
                if (MurdererRoom.cullingTree != null && MurdererRoom.cullingTree.Count > 0)
                {
                    foreach (var pair in MurdererRoom.cullingTree)
                    {
                        if (pair.Key != null && !controller.currentRoomsCullingTree.Contains(pair.Key))
                        {
                            foreach (var entry in pair.Value)
                            {
                                if (entry.requiredOpenDoors == null || entry.requiredOpenDoors.Count <= 0)
                                {
                                    controller.currentRoomsCullingTree.Add(pair.Key);
                                    controller.currentRoomsCullingWithImmediateStuffLoad.Add(pair.Key);
                                    break;
                                }
                                else
                                {
                                    bool allDoorsOpen = true;
                                    foreach (int doorKey in entry.requiredOpenDoors)
                                    {
                                        NewDoor door = null;
                                        if (CityData.Instance.doorDictionary.TryGetValue(doorKey, out door))
                                        {
                                            if (door.isClosed && !door.peekedUnder)
                                            {
                                                allDoorsOpen = false;
                                                break;
                                            }
                                        }
                                    }
                                    
                                    if (allDoorsOpen)
                                    {
                                        controller.currentRoomsCullingTree.Add(pair.Key);
                                        controller.currentRoomsCullingWithImmediateStuffLoad.Add(pair.Key);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Execute the culling tree
                controller.ExecuteCurrentCullingTree(immediateLoad);
                
                KillerCam.Logger.LogInfo($"Comprehensive culling update completed for murderer's view with {controller.currentRoomsCullingTree.Count} rooms");
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"Error in UpdateComprehensiveCulling: {ex.Message}");
            }
        }
        
        public static void EnsureRoomsAreVisible()
        {
            if (!IsActive || MurdererRoom == null)
                return;
                
            try
            {
                // Make sure the murderer room is visible
                if (MurdererRoom != null && !MurdererRoom.isVisible)
                {
                    MurdererRoom.SetVisible(true, false, "KillerCam", true, true);
                    KillerCam.Logger.LogInfo($"MurdererRoomTracker: Forced murderer room {MurdererRoom.name} to be visible");
                }
                
                // Make sure adjacent rooms are visible
                foreach (var room in AdjacentRooms)
                {
                    if (room != null && !room.isVisible)
                    {
                        room.SetVisible(true, false, "KillerCam", true, true);
                        KillerCam.Logger.LogInfo($"MurdererRoomTracker: Forced adjacent room {room.name} to be visible");
                    }
                }
                
                // Make sure extended visibility rooms are visible
                foreach (var room in ExtendedVisibilityRooms)
                {
                    if (room != null && !room.isVisible)
                    {
                        room.SetVisible(true, false, "KillerCam", true, true);
                        KillerCam.Logger.LogInfo($"MurdererRoomTracker: Forced extended visibility room {room.name} to be visible");
                    }
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"MurdererRoomTracker: Error ensuring rooms are visible: {ex.Message}");
            }
        }
    }

    // Patch for GeometryCullingController.UpdateCullingForRoom method
    [HarmonyPatch(typeof(GeometryCullingController), "UpdateCullingForRoom")]
    public class GeometryCullingPatch
    {
        // Prefix runs before the original method
        [HarmonyPrefix]
        public static void Prefix(GeometryCullingController __instance)
        {
            // We don't need to do anything in the prefix, just let the original method run
        }
        
        // Prefix runs before the original method to prevent it from clearing our rooms if we're in murderer view
        [HarmonyPrefix]
        public static bool Prefix(GeometryCullingController __instance, NewRoom currentRoom, bool updateSound, bool inAirVent, AirDuctGroup currentDuct, bool immediateLoad)
        {
            if (!MurdererRoomTracker.IsActive || MurdererRoomTracker.MurdererRoom == null)
                return true; // Let the original method run normally

            try
            {
                // We'll handle culling ourselves for the murderer's view
                MurdererRoomTracker.UpdateComprehensiveCulling(__instance, immediateLoad);
                return false; // Skip the original method
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"GeometryCullingPatch: Error in Prefix: {ex.Message}");
                return true; // Let the original method run as fallback
            }
        }
        
        // Postfix runs after the original method
        [HarmonyPostfix]
        public static void Postfix(GeometryCullingController __instance)
        {
            if (!MurdererRoomTracker.IsActive || MurdererRoomTracker.MurdererRoom == null)
                return;

            try
            {
                // If the original method ran, make sure our rooms are still in the tree
                // Add murderer room to culling structures
                MurdererRoomTracker.AddRoomToCullingTree(__instance, MurdererRoomTracker.MurdererRoom);
                
                // Add adjacent rooms to culling structures
                foreach (var adjacentRoom in MurdererRoomTracker.AdjacentRooms)
                {
                    MurdererRoomTracker.AddRoomToCullingTree(__instance, adjacentRoom);
                }
                
                // Add extended visibility rooms to culling structures
                foreach (var extendedRoom in MurdererRoomTracker.ExtendedVisibilityRooms)
                {
                    MurdererRoomTracker.AddRoomToCullingTree(__instance, extendedRoom);
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError($"GeometryCullingPatch: Error in Postfix: {ex.Message}");
            }
        }
    }
    
    // Patch for GeometryCullingController.ExecuteCurrentCullingTree method
    [HarmonyPatch(typeof(GeometryCullingController), "ExecuteCurrentCullingTree")]
    public class ExecuteCullingTreePatch
    {
        // Postfix runs after the original method
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Ensure our rooms are visible after culling is executed
            MurdererRoomTracker.EnsureRoomsAreVisible();
        }
    }
}
