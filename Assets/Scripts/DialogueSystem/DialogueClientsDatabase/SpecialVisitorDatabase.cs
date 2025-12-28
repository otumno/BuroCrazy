using UnityEngine;
using System.Collections.Generic;
using DialogueSystem.Data;

// Файл: Assets/Scripts/DialogueSystem/DialogueClientsDatabase/SpecialVisitorDatabase.cs
[CreateAssetMenu(fileName = "SpecialVisitors", menuName = "Bureau/Special Visitor Database")]
public class SpecialVisitorDatabase : ScriptableObject
{
    [System.Serializable]
    public class ScheduledVisitor
    {
        [Header("Основное")]
        public string name;
        [Tooltip("День, в который должен прийти посетитель.")]
        public int dayToSpawn;
        [Tooltip("Диалог, который запустится при клике.")]
        public DialogueGraph dialogue;
        [Range(0f, 1f)] public float spawnChance = 1f;

        [Header("Условия Появления (Сюжет)")]
        [Tooltip("Имя флага, который должен быть установлен (через EventNode: SetFlag). Если пусто — условие игнорируется.")]
        public string requiredFlagKey;
        [Tooltip("Значение флага, необходимое для появления (обычно 1).")]
        public int requiredFlagValue = 1;

        [Header("Цель Визита")]
        [Tooltip("Какую цель насильно поставить этому клиенту?")]
        public ClientGoal forcedGoal = ClientGoal.DirectorApproval;

        [Header("Настройки Времени")]
        [Tooltip("Если True, появится МГНОВЕННО при загрузке дня (до нажатия 'Начать').")]
        public bool spawnAtStartOfDay;

        [Header("Настройки Звонка / Удаленного визита")]
        [Tooltip("Если True, персонаж не пойдет к столу, а появится как предмет (телефон/письмо).")]
        public bool isRemoteInteraction; 
        
        [Tooltip("Иконка для стола (Телефон, Конверт). Обязательно для удаленных визитов.")]
        public Sprite deskIconOverride;
		
		// --- НОВЫЕ ПОЛЯ ---
        [Tooltip("Сколько периодов (Утро, День, Вечер) звонок будет активен. Если 0 - вечно.")]
        public int lifetimePeriods = 2; 

        [Tooltip("Звук при появлении (например, звонок телефона).")]
        public AudioClip arrivalSound;
        // ------------------
		
    }

    public List<ScheduledVisitor> visitors;
}