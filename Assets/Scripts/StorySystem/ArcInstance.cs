using System.Collections.Generic;
using DialogueSystem.Data;
using Characters;

namespace StorySystem
{
    /// <summary>
    /// Runtime-данные активной арки.
    /// </summary>
    [System.Serializable]
    public class ArcInstance
    {
        /// <summary>Определение арки (ScriptableObject).</summary>
        public ArcDefinition definition;

        /// <summary>День игры, когда арка была начата.</summary>
        public int startDay;

        /// <summary>Индекс текущего этапа (0-based).</summary>
        public int currentStageIndex;

        /// <summary>Завершена ли арка целиком.</summary>
        public bool isCompleted;

        /// <summary>Массив, отмечающий, был ли этап активирован.</summary>
        public bool[] stageActivated;

        public ArcInstance() { }

        public ArcInstance(ArcDefinition def, int startDayIndex)
        {
            definition = def;
            startDay = startDayIndex;
            currentStageIndex = 0;
            isCompleted = false;
            int count = def != null && def.stages != null ? def.stages.Count : 0;
            stageActivated = new bool[count];
        }

        /// <summary>
        /// Общее число этапов арки.
        /// </summary>
        public int StageCount
        {
            get
            {
                if (definition == null || definition.stages == null) return 0;
                return definition.stages.Count;
            }
        }

        /// <summary>
        /// Есть ли ещё этапы для прохождения.
        /// </summary>
        public bool HasMoreStages => currentStageIndex < StageCount;

        /// <summary>
        /// Текущий день арки (от 0).
        /// </summary>
        public int GetCurrentArcDay(int currentGameDay)
        {
            return currentGameDay - startDay;
        }
    }

    /// <summary>
    /// Запланированная встреча в рамках арки (для очереди).
    /// </summary>
    [System.Serializable]
    public class ArcEncounter
    {
        public ArcInstance arc;
        public int stageIndex;
        public DialogueGraph dialogue;
        public ClientGoal goal;
        public ClientArchetype archetype;
        public bool isRemote;

        /// <summary>
        /// День, на который запланирована встреча (для WaveManager).
        /// </summary>
        public int scheduledDay;

        /// <summary>
        /// Отображаемое имя персонажа в диалоге (если пусто, используется имя по умолчанию).
        /// </summary>
        public string characterName;
    }

    /// <summary>
    /// Сериализуемая структура для сохранения состояния арок в SaveData.
    /// </summary>
    [System.Serializable]
    public class ArcSaveData
    {
        public string arcID;
        public int startDay;
        public int currentStageIndex;
        public bool isCompleted;
        public bool[] stageActivated;
    }
}