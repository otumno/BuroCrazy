// Assets/Scripts/Scriptables/Progression/JobTitleData.cs
using UnityEngine;
using System.Collections.Generic;
// StaffController находится в global namespace (без явного namespace), 
// поэтому обращаемся к нему напрямую как StaffController.Role.

namespace Scriptables.Progression
{
    [CreateAssetMenu(fileName = "Job_New", menuName = "Bureau/Progression/Job Title Data")]
    public class JobTitleData : ScriptableObject
    {
        [Header("Основное")]
        public string jobID; 
        public string titleName; 
        [TextArea(2, 4)]
        public string description;

        [Header("Структура Дерева")]
        [Tooltip("Уровень должности: 0 - Директор, 1 - Региональный, 5 - Министр")]
        public int tierLevel;
        
        [Tooltip("Какую должность нужно иметь перед этой (родительская нода).")]
        public JobTitleData requiredPreviousJob;

        [Header("Условия Открытия")]
        public int costInfluence;
        public int costMoney;
        
        [Tooltip("Сколько регионов должно быть захвачено для доступа к этому уровню карьеры")]
        public int requiredCapturedRegionsCount;

        [Header("Финал")]
        [Tooltip("Если галочка стоит, получение этой должности означает победу в игре.")]
        public bool isMinisterPosition; 

        // ----- Новые поля расширения -----

        [Header("Нарратив")]
        [Tooltip("Название дела для UI (например, 'Дело о печатях').")]
        public string caseTitle;

        [Header("Прогрессия")]
        [Tooltip("Обязательная ли должность для пути к Министру (если false — побочная ветка).")]
        public bool isMandatory;

        [Header("Визуал Дерева")]
        [Tooltip("Чёрно-белая иконка для заблокированной должности.")]
        public Sprite iconLocked;
        [Tooltip("Цветная иконка для открытой/доступной должности.")]
        public Sprite iconUnlocked;

        [Header("Политика")]
        [Tooltip("ID политики, которая активируется при получении этой должности (может быть пустым).")]
        public string policyID;

        [Header("Разблокировки")]
        [Tooltip("Роли, которые становятся доступны для найма после получения должности.")]
        public List<StaffController.Role> unlockedRoles = new List<StaffController.Role>();

        [Tooltip("ID апгрейдов, которые активируются при получении должности.")]
        public List<string> unlockedUpgrades = new List<string>();

        [Header("Пути Вступления")]
        [Tooltip("Условия 'правильного' (честного) пути вступления в должность.")]
        public PathData correctPath = new PathData();

        [Tooltip("Условия 'обходного' пути (за деньги и/или коррупцию). Может быть недоступен.")]
        public PathData alternatePath = new PathData();

        /// <summary>
        /// Условия вступления в должность по одному из путей (правильный или обходной).
        /// </summary>
        [System.Serializable]
        public class PathData
        {
            [Tooltip("Доступен ли этот путь. false = путь нельзя выбрать (например, у стартовой должности или Министра).")]
            public bool isValid = true;

            [Tooltip("Сколько игровых дней должно пройти.")]
            public int requiredDays;

            [Tooltip("Сколько клиентов должно быть обслужено.")]
            public int requiredClients;

            [Tooltip("Сколько сотрудников должно быть нанято.")]
            public int requiredStaff;

            [Tooltip("Сколько регионов должно быть захвачено.")]
            public int requiredRegions;

            [Tooltip("Сколько апгрейдов должно быть куплено.")]
            public int requiredUpgrades;

            [Tooltip("Сколько документов должно быть обработано.")]
            public int requiredDocuments;

            [Tooltip("Требуемая сумма денег на счёте.")]
            public int requiredMoney;

            [Tooltip("Деньги, которые нужно ЗАПЛАТИТЬ за обходной путь (только alternatePath).")]
            public int moneyCost;

            [Tooltip("Коррупция в процентах, которую нужно ЗАПЛАТИТЬ за обходной путь (только alternatePath).")]
            public int corruptionCost;
        }
    }
}
