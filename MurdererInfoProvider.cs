using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;

namespace KillerCam
{
    public class MurdererInfoProvider
    {
        // Important: Initialize this when needed, not at class declaration time
        private MurderController murderController;
        private Human murderer = null;
        private FieldInfo currentMurdererField = null;
        
        // Constructor to properly initialize the controller
        public MurdererInfoProvider()
        {
            // Get the instance at the time of creation, not at class declaration
            murderController = MurderController.Instance;
            KillerCam.Logger.LogInfo("MurdererInfoProvider initialized, MurderController: " + (murderController != null ? "Not null" : "Null"));
            
            // Try to get the currentMurderer field using reflection
            try
            {
                currentMurdererField = typeof(MurderController).GetField("currentMurderer", BindingFlags.Public | BindingFlags.Instance);
                KillerCam.Logger.LogInfo("Found currentMurderer field: " + (currentMurdererField != null ? "Yes" : "No"));
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("Error getting currentMurderer field: " + ex.Message);
            }
        }

        public Vector3Int GetMurdererLocation()
        {
            try
            {
                // Re-get the controller instance to ensure it's current
                murderController = MurderController.Instance;
                KillerCam.Logger.LogInfo("GetMurdererLocation - MurderController: " + (murderController != null ? "Not null" : "Null"));
                
                if (murderController != null)
                {
                    try
                    {
                        KillerCam.Logger.LogInfo("GetMurdererLocation - Attempting to access currentMurderer");
                        Human currentMurderer = murderController.currentMurderer;
                        
                        if (currentMurderer != null && currentMurderer.transform != null)
                        {
                            Vector3 murdererLocation = currentMurderer.transform.position;
                            KillerCam.Logger.LogInfo("GetMurdererLocation - Got position: " + murdererLocation);
                            return new Vector3Int(Mathf.RoundToInt(murdererLocation.x),
                                               Mathf.RoundToInt(murdererLocation.y),
                                               Mathf.RoundToInt(murdererLocation.z));
                        }
                        else
                        {
                            KillerCam.Logger.LogWarning("GetMurdererLocation - currentMurderer or transform is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        KillerCam.Logger.LogError("GetMurdererLocation - Error accessing currentMurderer: " + ex.Message);
                    }
                }
                else
                {
                    KillerCam.Logger.LogWarning("GetMurdererLocation - MurderController is null");
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("GetMurdererLocation - Unexpected error: " + ex.Message);
            }
            
            // Default position if we couldn't get the murderer location
            KillerCam.Logger.LogInfo("GetMurdererLocation - Returning default position (0, 70, 0)");
            return new Vector3Int(0, 70, 0);
        }

        public Human GetMurderer()
        {
            try
            {
                // Re-get the controller instance to ensure it's current
                murderController = MurderController.Instance;
                KillerCam.Logger.LogInfo("GetMurderer - MurderController: " + (murderController != null ? "Not null" : "Null"));
                
                if (murderController != null)
                {
                    try
                    {
                        KillerCam.Logger.LogInfo("GetMurderer - Attempting to access currentMurderer");
                        murderer = murderController.currentMurderer;
                        
                        if (murderer != null)
                        {
                            KillerCam.Logger.LogInfo("GetMurderer - Got murderer: " + murderer.name);
                            return murderer;
                        }
                        else
                        {
                            KillerCam.Logger.LogWarning("GetMurderer - currentMurderer is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        KillerCam.Logger.LogError("GetMurderer - Error accessing currentMurderer: " + ex.Message);
                    }
                }
                else
                {
                    KillerCam.Logger.LogWarning("GetMurderer - MurderController is null");
                }
            }
            catch (Exception ex)
            {
                KillerCam.Logger.LogError("GetMurderer - Unexpected error: " + ex.Message);
            }
            
            return null;
        }
        public void SetMurdererLocation(Vector3 loc)
        {
            murderController.currentMurderer.transform.position = loc;
        }
        public string GetMurdererFullName()
        {
            if (murderController != null)
                {
                string firstName = murderController.currentMurderer.firstName.ToString();
                string lastName = murderController.currentMurderer.surName.ToString();
                string fullName = firstName + " " + lastName;
                return fullName;
            }

            return "murderController is null!";
        }
        public void AddPoisoned(float amount, Human who)
            {
                Player player = Player.Instance;
            murderController.currentMurderer.AddPoisoned(amount, player);
        }
        public void KillMurderer()
        {
            murderController.currentMurderer.RecieveDamage(99999f, Player.Instance, Vector2.zero, Vector2.zero, null, null, SpatterSimulation.EraseMode.useDespawnTime, true, false, 0f, 1f, true, true, 1f);
        }
        public void KOMurderer()
        {
            murderController.currentMurderer.RecieveDamage(99999f, Player.Instance, Vector2.zero, Vector2.zero, null, null, SpatterSimulation.EraseMode.useDespawnTime, true, false, 0f, 1f, false, true, 1f);
        }

        public string GetJob()
        {
            string noJob = "Citizen is jobless.";

            if (murderController.currentMurderer.job.employer != null)
                {
                string employer = murderController.currentMurderer.job.employer.name.ToString();
                string jobname = murderController.currentMurderer.job.name.ToString();
                string salary = murderController.currentMurderer.job.salaryString.ToString();

                string jobDec = "Employer: " + employer + Environment.NewLine + "Job: " + jobname + Environment.NewLine + "Salary: " + salary;

                    return jobDec;
                }
            else
            {
                return noJob;
            }
        }
    }
}
