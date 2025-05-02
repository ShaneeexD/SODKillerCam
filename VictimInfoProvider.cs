using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;

namespace KillerCam
{
    public class VictimInfoProvider
    {
        // Important: Initialize this when needed, not at class declaration time
        private MurderController murderController;
        private Human victim = null;
        private FieldInfo currentVictimField = null;
        
        // Constructor to properly initialize the controller
        public VictimInfoProvider()
        {
            // Get the instance at the time of creation, not at class declaration
            murderController = MurderController.Instance;
            KillerCam.Logger.LogInfo("VictimInfoProvider initialized, MurderController: " + (murderController != null ? "Not null" : "Null"));
            
            // Try to get the currentVictim field using reflection
            try
            {
                currentVictimField = typeof(MurderController).GetField("currentVictim", BindingFlags.Public | BindingFlags.Instance);
                KillerCam.Logger.LogInfo("Found currentVictim field: " + (currentVictimField != null ? "Yes" : "No"));
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error getting currentVictim field: " + ex.Message);
            }
        }

        public Vector3Int GetVictimLocation()
        {
            try
            {
                // Re-get the controller instance to ensure it's current
                murderController = MurderController.Instance;
                KillerCam.Logger.LogInfo("GetVictimLocation - MurderController: " + (murderController != null ? "Not null" : "Null"));
                
                if (murderController != null)
                {
                    try
                    {
                        KillerCam.Logger.LogInfo("GetVictimLocation - Attempting to access currentVictim");
                        Human currentVictim = murderController.currentVictim;
                        
                        if (currentVictim != null && currentVictim.transform != null)
                        {
                            Vector3 victimLocation = currentVictim.transform.position;
                            KillerCam.Logger.LogInfo("GetVictimLocation - Got position: " + victimLocation);
                            return new Vector3Int(Mathf.RoundToInt(victimLocation.x),
                                               Mathf.RoundToInt(victimLocation.y),
                                               Mathf.RoundToInt(victimLocation.z));
                        }
                        else
                        {
                            KillerCam.Logger.LogWarning("GetVictimLocation - currentVictim or transform is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        KillerCam.Logger.LogError("GetVictimLocation - Error accessing currentVictim: " + ex.Message);
                        
                        // Try using reflection if direct access fails
                        if (currentVictimField != null)
                        {
                            try
                            {
                                KillerCam.Logger.LogInfo("GetVictimLocation - Trying to get currentVictim via reflection");
                                Human reflectedVictim = currentVictimField.GetValue(murderController) as Human;
                                
                                if (reflectedVictim != null && reflectedVictim.transform != null)
                                {
                                    Vector3 victimLocation = reflectedVictim.transform.position;
                                    KillerCam.Logger.LogInfo("GetVictimLocation - Got position via reflection: " + victimLocation);
                                    return new Vector3Int(Mathf.RoundToInt(victimLocation.x),
                                                       Mathf.RoundToInt(victimLocation.y),
                                                       Mathf.RoundToInt(victimLocation.z));
                                }
                            }
                            catch (Exception reflectionEx)
                            {
                                KillerCam.Logger.LogError("GetVictimLocation - Error using reflection: " + reflectionEx.Message);
                            }
                        }
                    }
                }
                else
                {
                    KillerCam.Logger.LogWarning("GetVictimLocation - MurderController is null");
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("GetVictimLocation - Unexpected error: " + ex.Message);
            }
            
            // Default position if we couldn't get the victim location
            KillerCam.Logger.LogInfo("GetVictimLocation - Returning default position (0, 70, 0)");
            return new Vector3Int(0, 70, 0);
        }

        public Human GetVictim()
        {
            try
            {
                // Re-get the controller instance to ensure it's current
                murderController = MurderController.Instance;
                KillerCam.Logger.LogInfo("GetVictim - MurderController: " + (murderController != null ? "Not null" : "Null"));
                
                if (murderController != null)
                {
                    try
                    {
                        KillerCam.Logger.LogInfo("GetVictim - Attempting to access currentVictim");
                        victim = murderController.currentVictim;
                        
                        if (victim != null)
                        {
                            KillerCam.Logger.LogInfo("GetVictim - Got victim: " + victim.name);
                            return victim;
                        }
                        else
                        {
                            KillerCam.Logger.LogWarning("GetVictim - currentVictim is null");
                            
                            // Try using reflection if direct access returns null
                            if (currentVictimField != null)
                            {
                                try
                                {
                                    KillerCam.Logger.LogInfo("GetVictim - Trying to get currentVictim via reflection");
                                    victim = currentVictimField.GetValue(murderController) as Human;
                                    
                                    if (victim != null)
                                    {
                                        KillerCam.Logger.LogInfo("GetVictim - Got victim via reflection: " + victim.name);
                                        return victim;
                                    }
                                    else
                                    {
                                        KillerCam.Logger.LogWarning("GetVictim - Reflection returned null for currentVictim");
                                    }
                                }
                                catch (Exception reflectionEx)
                                {
                                    KillerCam.Logger.LogError("GetVictim - Error using reflection: " + reflectionEx.Message);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        KillerCam.Logger.LogError("GetVictim - Error accessing currentVictim: " + ex.Message);
                    }
                }
                else
                {
                    KillerCam.Logger.LogWarning("GetVictim - MurderController is null");
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("GetVictim - Unexpected error: " + ex.Message);
            }
            
            // Return null if we couldn't get the victim
            KillerCam.Logger.LogInfo("GetVictim - Returning null");
            return null;
        }
    }
}
