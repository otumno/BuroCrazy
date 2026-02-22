using System.Collections.Generic;
using UnityEngine;
using Characters;
using Enums;

namespace Data.Creation
{
    [CreateAssetMenu(fileName = "DirectorCreationData", menuName = "Bureau/Creation/Director Initial State")]
    public class DirectorInitialState : ScriptableObject
    {
        [Header("Ресурсы")]
        public int startingMoney = 100;
        public int startingInfluence = 0;

        [Header("Директор")]
        public Gender startingGender = Gender.Male;
        public int startingStrikes = 0;
        public string spriteCollectionID = "";

        [Header("Стартовые работники")]
        public List<StaffSpawnData> startingStaff = new List<StaffSpawnData>();

        [System.Serializable]
        public class StaffSpawnData
        {
            public StaffController.Role role;
            public string customName;
            public int skillLevel = 1;
            public float punctuality = 0.5f;      // 0-1, педантичность
            public float stressResistance = 0.5f;
            public float workSpeed = 1f;
            public List<string> positiveTraits = new List<string>();
            public List<string> negativeTraits = new List<string>();
        }

        [Header("Стартовые апгрейды")]
        public List<string> unlockedUpgradeNames = new List<string>();

        [Header("Стартовые политики")]
        public List<string> activePolicyIDs = new List<string>();

        [Header("Сценарий/Предыстория")]
        public string scenarioID;
        public List<string> unlockedPhoneContacts = new List<string>();
        public List<string> unlockedRegions = new List<string>();

        [Header("Настройки смены")]
        public int workDayDuration = 120;      // Длительность рабочего дня в секундах
        public int workDayStartHour = 8;       // Начало рабочего дня (8 = 8:00)
        public int lunchBreakDuration = 15;    // Длительность обеда в секундах

        [Header("Особые флаги")]
        public List<string> flags = new List<string>();
    }
}
