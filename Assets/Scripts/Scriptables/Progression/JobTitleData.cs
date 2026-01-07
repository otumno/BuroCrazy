// Assets/Scripts/Scriptables/Progression/JobTitleData.cs
using UnityEngine;

namespace Scriptables.Progression
{
    [CreateAssetMenu(fileName = "Job_New", menuName = "Bureau/Progression/Job Title Data")]
    public class JobTitleData : ScriptableObject
    {
        [Header("Основное")]
        public string jobID; // Например "HEAD_OF_CHAOS"
        public string titleName; // "Начальник отдела Хаоса"
        [TextArea(2, 4)]
        public string description; // Тултип: "Дает возможность орать на подчиненных"

        [Header("Структура Дерева")]
        [Tooltip("Уровень должности: 0 - Директор, 1 - Региональный, 5 - Министр")]
        public int tierLevel;
        
        [Tooltip("Какую должность нужно иметь перед этой (родительская нода). Если null — это стартовая должность.")]
        public JobTitleData requiredPreviousJob;

        [Header("Условия Открытия")]
        public int costInfluence;
        public int costMoney;
        
        [Tooltip("Сколько регионов должно быть захвачено для доступа к этому уровню карьеры")]
        public int requiredCapturedRegionsCount;

        [Header("Финал")]
        [Tooltip("Если галочка стоит, получение этой должности означает победу в игре.")]
        public bool isMinisterPosition; 
        
        // public List<PolicyData> unlockedPolicies; // Заготовка под Политики (Этап 2)
    }
}