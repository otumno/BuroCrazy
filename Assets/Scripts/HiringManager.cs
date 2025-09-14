using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

// Вспомогательный класс для хранения данных о кандидате.
// Он не является MonoBehaviour, поэтому может находиться в этом же файле.
[System.Serializable]
public class Candidate
{
    public string Name;
    public Gender Gender;
    public StaffController.Role Role = StaffController.Role.Intern; // Все кандидаты - стажеры
    public CharacterSkills Skills;
    public int HiringCost;
}

public class HiringManager : MonoBehaviour
{
    public static HiringManager Instance { get; set; }

    [Header("Префабы сотрудников")]
    public GameObject internPrefab; // Нам нужен только префаб стажера

    // Список точек, который будет заполняться автоматически при загрузке сцены
    private List<Transform> unassignedStaffPoints = new List<Transform>();
    private Dictionary<Transform, StaffController> occupiedPoints = new Dictionary<Transform, StaffController>();

    [Header("Настройки генерации")]
    public int minCandidatesPerDay = 2;
    public int maxCandidatesPerDay = 4;
    public int baseCost = 100;
    public int costPerSkillPoint = 150;
    
    // Списки имен для генерации (для обоих полов)
    private List<string> firstNamesMale = new List<string> { "Виктор", "Иван", "Петр", "Семён", "Аркадий", "Борис", "Геннадий" };
    private List<string> firstNamesFemale = new List<string> { "Анна", "Мария", "Ольга", "Светлана", "Ирина", "Валентина", "Галина" };
    private List<string> lastNames = new List<string> { "Скрепкин", "Бланков", "Циркуляров", "Печаткин", "Архивариусов", "Формуляров" };
    private List<string> patronymicsMale = new List<string> { "Радеонович", "Петрович", "Иванович", "Семёнович", "Аркадьевич", "Борисович", "Геннадьевич" };
    private List<string> patronymicsFemale = new List<string> { "Радеоновна", "Петровна", "Ивановна", "Семёновна", "Аркадьевна", "Борисовна", "Геннадьевна" };

    public List<Candidate> AvailableCandidates { get; private set; } = new List<Candidate>();

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Если загрузилась именно игровая сцена, ищем нужные объекты
        if (scene.name == "GameScene")
        {
            FindSceneSpecificReferences();
        }
    }

    private void FindSceneSpecificReferences()
    {
        unassignedStaffPoints.Clear();
        occupiedPoints.Clear();

        InternPointsRegistry registry = FindObjectOfType<InternPointsRegistry>();
        if (registry != null)
        {
            unassignedStaffPoints = registry.points;
            Debug.Log($"[HiringManager] Успешно найдено {unassignedStaffPoints.Count} точек для стажеров.");
        }
        else
        {
            Debug.LogError("[HiringManager] НЕ УДАЛОСЬ найти InternPointsRegistry на сцене GameScene! Наем новых сотрудников не будет работать.");
        }
    }

    public void GenerateNewCandidates()
    {
        AvailableCandidates.Clear();
        int count = Random.Range(minCandidatesPerDay, maxCandidatesPerDay + 1);
        for (int i = 0; i < count; i++)
        {
            AvailableCandidates.Add(CreateRandomCandidate());
        }
        Debug.Log($"[HiringManager] Сгенерировано {AvailableCandidates.Count} новых кандидатов.");
    }

    private Candidate CreateRandomCandidate()
    {
        Candidate candidate = new Candidate();
        candidate.Gender = (Random.value > 0.5f) ? Gender.Male : Gender.Female;
        
        if (candidate.Gender == Gender.Male)
        {
            candidate.Name = $"{lastNames[Random.Range(0, lastNames.Count)]} {firstNamesMale[Random.Range(0, firstNamesMale.Count)]} {patronymicsMale[Random.Range(0, patronymicsMale.Count)]}";
        }
        else
        {
            candidate.Name = $"{lastNames[Random.Range(0, lastNames.Count)]}а {firstNamesFemale[Random.Range(0, firstNamesFemale.Count)]} {patronymicsFemale[Random.Range(0, patronymicsFemale.Count)]}";
        }

        candidate.Skills = ScriptableObject.CreateInstance<CharacterSkills>();
        
        candidate.Skills.paperworkMastery = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        candidate.Skills.sedentaryResilience = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        candidate.Skills.pedantry = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        candidate.Skills.softSkills = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        candidate.Skills.corruption = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;

        float totalSkillPoints = candidate.Skills.paperworkMastery + candidate.Skills.sedentaryResilience +
                                 candidate.Skills.pedantry + candidate.Skills.softSkills;
        candidate.HiringCost = baseCost + (int)(totalSkillPoints * costPerSkillPoint);

        return candidate;
    }

    public bool HireCandidate(Candidate candidate)
    {
        if (PlayerWallet.Instance.GetCurrentMoney() < candidate.HiringCost)
        {
            Debug.Log("Недостаточно средств для найма!");
            return false;
        }

        if (unassignedStaffPoints.Count == 0)
        {
             Debug.LogError("[HiringManager] Не найдено ни одной точки для размещения стажера!");
             return false;
        }

        Transform freePoint = unassignedStaffPoints.FirstOrDefault(p => !occupiedPoints.ContainsKey(p));
        if (freePoint == null)
        {
            Debug.Log("Нет свободных мест в кабинете директора для новых стажеров!");
            return false;
        }
        
        PlayerWallet.Instance.AddMoney(-candidate.HiringCost, Vector3.zero);

        GameObject newStaffGO = Instantiate(internPrefab, freePoint.position, Quaternion.identity);
        
        // Получаем любой контроллер, т.к. пол есть в базовом StaffController
        StaffController staffController = newStaffGO.GetComponent<StaffController>();
        if (staffController != null)
        {
            staffController.skills = candidate.Skills;
            staffController.gender = candidate.Gender;
        }
        newStaffGO.name = candidate.Name;

        occupiedPoints.Add(freePoint, staffController);
        AvailableCandidates.Remove(candidate);
        Debug.Log($"Нанят сотрудник {candidate.Name} за ${candidate.HiringCost}");
        return true;
    }

    public void FireStaff(StaffController staffToFire)
    {
        if (staffToFire == null) return;

        Debug.Log($"Сотрудник {staffToFire.name} уволен!");

        // Если уволенный сотрудник занимал место в кабинете, освобождаем его
        if (occupiedPoints.ContainsValue(staffToFire))
        {
            Transform pointToFree = occupiedPoints.FirstOrDefault(kvp => kvp.Value == staffToFire).Key;
            if (pointToFree != null)
            {
                occupiedPoints.Remove(pointToFree);
            }
        }
        
        Destroy(staffToFire.gameObject);
    }
}