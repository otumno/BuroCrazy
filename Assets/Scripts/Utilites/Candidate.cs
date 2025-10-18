// Файл: Candidate.cs
using System.Collections.Generic;

[System.Serializable]
public class Candidate
{
    public string Name;
    public Gender Gender;
    public CharacterSkills Skills;
    public int HiringCost;
    public string Bio;
    public List<StaffAction> UniqueActionsPool = new List<StaffAction>();

    // --- НОВЫЕ ПОЛЯ ---
    public StaffController.Role Role; // Будущая роль
    public RankData Rank;             // Начальный ранг
    public int Experience;           // Начальный опыт
}