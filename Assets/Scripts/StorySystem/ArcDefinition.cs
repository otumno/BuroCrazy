using System.Collections.Generic;
using UnityEngine;
using DialogueSystem.Data;
using Characters;
// Gender определён в namespace Enums.

namespace StorySystem
{
    /// <summary>
    /// Описание одного этапа сюжетной арки.
    /// </summary>
    [System.Serializable]
    public class StageDefinition
    {
        [Tooltip("Номер дня от начала арки (0 = первый день арки).")]
        public int dayOffset;

        [Tooltip("Диалог, который будет показан на этом этапе (обязательно).")]
        public DialogueGraph dialogue;

        [Tooltip("Флаг, который должен быть установлен, чтобы этап стал доступен (обычно флаг завершения предыдущего этапа).")]
        public string requiredFlag;

        [Tooltip("Флаг, устанавливаемый при активации этапа (для отслеживания).")]
        public string onStartFlag;

        [Tooltip("Флаг, устанавливаемый после завершения этапа (должен ставиться диалогом через EventNode).")]
        public string onCompleteFlag;

        [Tooltip("Если указано, переопределяет цель для посетителя на этом этапе.")]
        public ClientGoal forcedGoal = ClientGoal.DirectorAudience;

        [Tooltip("Использовать ли forcedGoal из этого этапа. Если false — берётся цель из ArcDefinition.")]
        public bool useForcedGoal = false;

        [Tooltip("Если указано, переопределяет архетип для посетителя на этом этапе.")]
        public ClientArchetype forcedArchetype;

        [Tooltip("Если true, встреча происходит через телефон/иконку (без физического прихода посетителя).")]
        public bool isRemoteInteraction = false;

        [Tooltip("Имя персонажа, отображаемое в диалоге. Если пусто, используется имя по умолчанию.")]
        public string characterName;

        [Tooltip("Пол персонажа: 'Male', 'Female' или 'Any' (по умолчанию). Используется при спавне клиента " +
                 "через WaveManager, чтобы выставить client.gender и подобрать корректный архетип-аналог.")]
        public string characterGender = "Any";

        [Tooltip("Путь к Sprite, который будет использован как фон диалога (записывается в defaultBackground " +
                 "стартового узла DialogueGraph). Пример: 'Assets/Sprites/Backs/DirectorOfficeBack.png'. " +
                 "Если пусто — будет использован дефолтный фон DirectorOfficeBack.")]
        public string backgroundResource = "";

        [Tooltip("Помечает этап как эпилог. Поле опциональное — сейчас не влияет на логику ArcManager, " +
                 "сохраняется для будущей системы эпилогов и аналитики.")]
        public bool isEpilogue = false;
    }

    /// <summary>
    /// ScriptableObject, описывающий статические данные сюжетной арки.
    /// </summary>
    [CreateAssetMenu(fileName = "Arc_New", menuName = "Bureau/Story/Arc Definition")]
    public class ArcDefinition : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный идентификатор арки (например, 'Orwell_1984').")]
        public string arcID;

        [Tooltip("Отображаемое имя (для отладки).")]
        public string displayName;

        [Tooltip("Заголовок «дела» для титра в начале диалога. Если пусто — используется displayName с префиксом «Дело о ».")]
        public string caseTitle;

        [TextArea(3, 6)]
        [Tooltip("Описание сюжета.")]
        public string description;

        [Header("Параметры")]
        [Tooltip("Общая длительность арки в днях (от первого до последнего этапа).")]
        [Min(1)] public int durationDays = 2;

        [Tooltip("Приоритет для выбора (выше = важнее).")]
        [Range(1, 10)] public int priority = 1;

        [Tooltip("Если true, арка не может быть выбрана повторно после завершения.")]
        public bool isOneTimeOnly = true;

        [Tooltip("Тип арки, определяющий BGM во всех её диалогах. None = fallback на phone/world треки.")]
        public ArcMusicType arcType = ArcMusicType.None;

        [Header("Этапы арки (порядок важен)")]
        public List<StageDefinition> stages = new List<StageDefinition>();

        [Header("Параметры посетителя по умолчанию")]
        [Tooltip("Цель для посетителя по умолчанию.")]
        public ClientGoal defaultGoal = ClientGoal.DirectorAudience;

        [Tooltip("Архетип по умолчанию (если не указан в этапе).")]
        public ClientArchetype defaultArchetype;

        /// <summary>
        /// Возвращает целевую ClientGoal для конкретного этапа (с учётом переопределения).
        /// </summary>
        public ClientGoal GetGoalForStage(int stageIndex)
        {
            if (stages == null || stageIndex < 0 || stageIndex >= stages.Count)
                return defaultGoal;

            var stage = stages[stageIndex];
            return stage.useForcedGoal ? stage.forcedGoal : defaultGoal;
        }

        /// <summary>
        /// Возвращает архетип для конкретного этапа (с учётом переопределения).
        /// </summary>
        public ClientArchetype GetArchetypeForStage(int stageIndex)
        {
            if (stages == null || stageIndex < 0 || stageIndex >= stages.Count)
                return defaultArchetype;

            var stage = stages[stageIndex];
            return stage.forcedArchetype != null ? stage.forcedArchetype : defaultArchetype;
        }

        /// <summary>
        /// Возвращает, является ли этап удалённым взаимодействием.
        /// </summary>
        public bool IsStageRemote(int stageIndex)
        {
            if (stages == null || stageIndex < 0 || stageIndex >= stages.Count)
                return false;
            return stages[stageIndex].isRemoteInteraction;
        }

        /// <summary>
        /// Пол персонажа для конкретного этапа. Если в этапе указано "Any" или пусто —
        /// возвращается Gender.Male (стандартное значение enum). Иначе парсится строка.
        /// </summary>
        public Enums.Gender GetGenderForStage(int stageIndex)
        {
            if (stages == null || stageIndex < 0 || stageIndex >= stages.Count)
                return Enums.Gender.Male;

            return ParseGenderString(stages[stageIndex].characterGender);
        }

        /// <summary>
        /// Парсит строку 'Male'/'Female'/'Any' в enum Gender. Дефолт — Male.
        /// </summary>
        public static Enums.Gender ParseGenderString(string s)
        {
            if (string.IsNullOrEmpty(s)) return Enums.Gender.Male;
            if (s.Equals("Female", System.StringComparison.OrdinalIgnoreCase)) return Enums.Gender.Female;
            if (s.Equals("F", System.StringComparison.OrdinalIgnoreCase)) return Enums.Gender.Female;
            if (s.Equals("Ж", System.StringComparison.OrdinalIgnoreCase)) return Enums.Gender.Female;
            return Enums.Gender.Male;
        }
    }
}