using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KillerCam
{
    // Static class to track murderer room information
    public static class MurdererRoomTracker
    {
        public static NewRoom MurdererRoom { get; private set; }
        public static List<NewRoom> AdjacentRooms { get; private set; } = new List<NewRoom>();
        public static bool IsActive { get; set; } = false;
        
        // Track rooms that we've manually added to the culling tree
        private static HashSet<NewRoom> addedRooms = new HashSet<NewRoom>();
        
        public static void UpdateMurdererRoom(NewRoom room)
        {
            MurdererRoom = room;
            AdjacentRooms.Clear();
            addedRooms.Clear();
            
            if (room != null && room.adjacentRooms != null)
            {
                foreach (var adjacentRoom in room.adjacentRooms)
                {
                    if (adjacentRoom != null)
                    {
                        AdjacentRooms.Add(adjacentRoom);
                    }
                }
            }
            
            KillerCam.Logger.LogInfo($"MurdererRoomTracker: Updated murderer room to {(room != null ? room.name : "null")}");
            KillerCam.Logger.LogInfo($"MurdererRoomTracker: Tracking {AdjacentRooms.Count} adjacent rooms");
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
            
            // Add to visible rooms list
            if (!CityData.Instance.visibleRooms.Contains(room))
            {
                CityData.Instance.visibleRooms.Add(room);
                KillerCam.Logger.LogInfo($"MurdererRoomTracker: Added room {room.name} to visible rooms");
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
        
        // Postfix runs after the original method
        [HarmonyPostfix]
        public static void Postfix(GeometryCullingController __instance)
        {
            if (!MurdererRoomTracker.IsActive || MurdererRoomTracker.MurdererRoom == null)
                return;

            try
            {
                // Add murderer room to culling structures
                MurdererRoomTracker.AddRoomToCullingTree(__instance, MurdererRoomTracker.MurdererRoom);
                
                // Add adjacent rooms to culling structures
                foreach (var adjacentRoom in MurdererRoomTracker.AdjacentRooms)
                {
                    MurdererRoomTracker.AddRoomToCullingTree(__instance, adjacentRoom);
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
