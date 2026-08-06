using System;
using System.Collections.Generic;
using Gameplay;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// Менеджер черт личности Директора (Law, Empathy, Mask, Ambition).
    /// Накапливает очки по 4 чертам через выборы в диалогах/кинематиках.
    /// Определяет доминирующую черту → используется EndingManager'ом для выбора концовки.
    /// </summary>
    public class TraitManager : MonoBehaviour
    {
        public static TraitManager Instance { get; private set; }

        public const string TRAIT_LAW = "Law";
        public const string TRAIT_EMPATHY = "Empathy";
        public const string TRAIT_MASK = "Mask";
        public const string TRAIT_AMBITION = "Ambition";

        public const string TRAIT_DISMISSAL = "Dismissal";

        public static readonly string[] AllTraits = { TRAIT_LAW, TRAIT_EMPATHY, TRAIT_MASK, TRAIT_AMBITION };

        private Dictionary<string, int> traitScores = new Dictionary<string, int>();

        public event Action<Dictionary<string, int>> OnTraitsChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AddTraitPoints(string traitKey, int amount)
        {
            if (string.IsNullOrEmpty(traitKey)) return;

            int minStep = -1;
            int maxStep = 2;
            int maxScore = 100;
            if (AIBalanceConfig.Instance != null)
            {
                minStep = AIBalanceConfig.Instance.traitMinStep;
                maxStep = AIBalanceConfig.Instance.traitMaxStep;
                maxScore = AIBalanceConfig.Instance.maxTraitScore;
            }

            int clampedAmount = Mathf.Clamp(amount, minStep, maxStep);
            if (!traitScores.ContainsKey(traitKey)) traitScores[traitKey] = 0;
            traitScores[traitKey] = Mathf.Clamp(traitScores[traitKey] + clampedAmount, 0, maxScore);

            OnTraitsChanged?.Invoke(traitScores);
        }

        public int GetTraitScore(string traitKey)
        {
            if (string.IsNullOrEmpty(traitKey)) return 0;
            return traitScores.TryGetValue(traitKey, out var score) ? score : 0;
        }

        public Dictionary<string, int> GetAllScores()
        {
            return new Dictionary<string, int>(traitScores);
        }

        public string GetDominantTrait()
        {
            var list = GetDominantTraits();
            return list.Count == 1 ? list[0] : null;
        }

        public List<string> GetDominantTraits()
        {
            int max = int.MinValue;
            foreach (var kv in traitScores)
            {
                if (kv.Value > max) max = kv.Value;
            }

            var result = new List<string>();
            if (max <= 0)
            {
                foreach (var key in AllTraits)
                {
                    if (GetTraitScore(key) == max) result.Add(key);
                }
                return result;
            }

            foreach (var kv in traitScores)
            {
                if (kv.Value == max) result.Add(kv.Key);
            }
            return result;
        }

        public void ResetTraits()
        {
            traitScores.Clear();
            foreach (var t in AllTraits) traitScores[t] = 0;
            OnTraitsChanged?.Invoke(traitScores);
        }

        public void Save(ref SaveData data)
        {
            if (data == null) return;
            data.SetTraitScoresFromDictionary(traitScores);
        }

        public void Load(SaveData data)
        {
            traitScores.Clear();
            if (data == null)
            {
                foreach (var t in AllTraits) traitScores[t] = 0;
                return;
            }

            foreach (var t in AllTraits) traitScores[t] = 0;
            var dict = data.GetTraitScoresDictionary();
            foreach (var kv in dict)
            {
                traitScores[kv.Key] = Mathf.Clamp(kv.Value, 0, AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.maxTraitScore : 100);
            }
            OnTraitsChanged?.Invoke(traitScores);
        }
    }
}
