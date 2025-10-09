using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    // Global data
    public int day;
    public int money;
    public int archiveDocumentCount;

    // New fields for Director's Orders
    public List<string> activePermanentOrderNames;
    public List<string> completedOneTimeOrderNames;

    // Lists for storing data about individual objects
    public List<StaffSaveData> allStaffData;
    public List<DocumentStackSaveData> allDocumentStackData;
}

[System.Serializable]
public struct StaffSaveData
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

[System.Serializable]
public struct DocumentStackSaveData
{
    public string stackOwnerName;
    public int documentCount;
}