// Файл: Candidate.cs
using System.Collections.Generic; // Необходимо для использования List

// Атрибут [System.Serializable] позволяет Unity корректно работать с этим классом
[System.Serializable]
public class Candidate
{
    public string Name;
    public Gender Gender;
    public StaffController.Role Role = StaffController.Role.Intern;
    public CharacterSkills Skills;
    public int HiringCost;
    public string Bio;
    public List<StaffAction> UniqueActionsPool = new List<StaffAction>();
}