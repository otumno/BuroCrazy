using System.Collections.Generic;
using UnityEngine;

namespace Data.Creation
{
    [CreateAssetMenu(fileName = "BookPage", menuName = "Bureau/Creation/Book Page")]
    public class BookPageData : ScriptableObject
    {
        [Header("Идентификация")]
        public string pageID;
        public int pageNumber;

        [Header("Содержание")]
        [TextArea(3, 8)]
        public string storyText;

        [Header("Визуал")]
        public Sprite backgroundImage;
        public Sprite characterIllustration;
        public Color textColor = Color.black;

        [Header("Выборы")]
        public List<BookChoice> choices;

        [System.Serializable]
        public class BookChoice
        {
            public string choiceID;
            [TextArea(2, 4)]
            public string choiceText;
            public Sprite choiceIcon;

            [Header("Эффекты выбора")]
            public List<ChoiceEffect> effects;

            [Tooltip("ID следующей страницы (оставить пустым для автоперехода)")]
            public string nextPageID;
        }

        [System.Serializable]
        public class ChoiceEffect
        {
            public EffectType type;
            public string targetID;        // ID ресурса, роли, апгрейда и т.д.
            public int value;              // Количество
            public bool addInsteadOfSet;   // Добавить к существующему или установить
        }

        public enum EffectType
        {
            AddMoney,
            SetMoney,
            AddInfluence,
            SetInfluence,
            AddStaff,
            RemoveStaff,
            UnlockUpgrade,
            LockUpgrade,
            SetPolicy,
            AddTrait,
            RemoveTrait,
            SetStartingScenario
        }
    }
}
