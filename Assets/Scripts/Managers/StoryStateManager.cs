// Файл: Assets/Scripts/Managers/StoryStateManager.cs
using UnityEngine;
using System.Collections.Generic;

namespace Managers
{
    public class StoryStateManager : MonoBehaviour
    {
        public static StoryStateManager Instance { get; private set; }

        // Основная память сюжета
        private Dictionary<string, int> storyFlags = new Dictionary<string, int>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Не делаем DontDestroyOnLoad, так как он будет висеть на [SYSTEMS], 
                // который уже бессмертен благодаря SystemsBootstrapper
            }
            else Destroy(gameObject);
        }

        // --- API для диалогов ---

        public void SetFlag(string key, int value)
        {
            if (storyFlags.ContainsKey(key)) storyFlags[key] = value;
            else storyFlags.Add(key, value);
            Debug.Log($"[Story] Флаг '{key}' = {value}");
        }

        public int GetFlag(string key)
        {
            return storyFlags.ContainsKey(key) ? storyFlags[key] : 0;
        }

        public bool CheckCondition(string key, string operation, int valueToCheck)
        {
            if (string.IsNullOrEmpty(key)) return true; // Нет условия = true

            int currentValue = GetFlag(key);

            // Системные переопределения
            if (key == "MONEY" && PlayerWallet.Instance != null) 
                currentValue = PlayerWallet.Instance.GetCurrentMoney();
            if (key == "CORRUPTION" && FinancialLedgerManager.Instance != null) 
                currentValue = FinancialLedgerManager.Instance.globalCorruptionScore;

            switch (operation)
            {
                case ">": return currentValue > valueToCheck;
                case "<": return currentValue < valueToCheck;
                case "==": return currentValue == valueToCheck;
                case "!=": return currentValue != valueToCheck;
                case ">=": return currentValue >= valueToCheck;
                case "<=": return currentValue <= valueToCheck;
                default: return false;
            }
        }

        // --- API для SaveLoadManager ---

        public void SaveToData(SaveData data)
        {
            data.storyFlagKeys = new List<string>(storyFlags.Keys);
            data.storyFlagValues = new List<int>(storyFlags.Values);
        }

        public void LoadFromData(SaveData data)
        {
            storyFlags.Clear();
            if (data.storyFlagKeys != null && data.storyFlagValues != null)
            {
                for (int i = 0; i < Mathf.Min(data.storyFlagKeys.Count, data.storyFlagValues.Count); i++)
                {
                    storyFlags.Add(data.storyFlagKeys[i], data.storyFlagValues[i]);
                }
            }
            Debug.Log($"[Story] Загружено {storyFlags.Count} сюжетных флагов.");
        }
        
        public void ResetState()
        {
            storyFlags.Clear();
        }
    }
}