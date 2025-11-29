using UnityEngine;

namespace Data.Saves
{
    [System.Serializable]
    public struct StaffData
    {
        public string characterName;
        public float stressLevel;
        public Vector3 position;
    
        // Added fields for saving
        public StaffController.Role role;
        public Gender gender;
        public int salary;
        public int experience;
        // Saving skills individually
        public float paperworkMastery;
        public float sedentaryResilience;
        public float pedantry;
        public float softSkills;
        public float corruption;
        public int assignedWorkstationId;
    }
}