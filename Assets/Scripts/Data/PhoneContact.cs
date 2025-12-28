using UnityEngine;

[System.Serializable]
public class PhoneContact
{
    public string id;           // Уникальный ID (например "GUARD")
    public string displayName;  // Имя для кнопки ("Охрана")
    public bool isUnlocked;     // Доступен ли контакт
    
    // ИСПРАВЛЕНИЕ: Убрали "Managers.", так как StaffController лежит в корне
    public StaffController.Role associatedRole; 
}