using System.Collections.Generic;
using UnityEngine;
using Enums;

namespace Data.Creation
{
    [CreateAssetMenu(fileName = "BookPage", menuName = "Bureau/Creation/Book Page")]
    public class BookPageData : ScriptableObject
    {
        [Header("Идентификация")]
        public string pageID;
        public int pageNumber;

        [Header("Тип книги")]
        [Tooltip("Achievement - ачивка с книгой, Director - книга директора, Manual - инструкция")]
        public BookType bookType = BookType.Achievement;

        [Header("Содержание")]
        [TextArea(3, 8)]
        public string storyText;

        [Header("Визуал")]
        public Sprite backgroundImage;
        public Sprite characterIllustration;
        public Color textColor = Color.black;

        [Header("Выборы")]
        public List<BookChoice> choices;

        [Header("Музыка")]
        [Tooltip("Музыка, которая играет на этой странице")]
        public AudioClip pageMusic;
        [Tooltip("Музыка после выбора на финальной странице")]
        public AudioClip finalMusic;

        [Header("Настройки")]
        [Tooltip("Это финальная страница (E)")]
        public bool isFinalPage = false;

        [System.Serializable]
        public class BookChoice
        {
            public string choiceID;
            [TextArea(2, 4)]
            public string choiceText;
            public Sprite choiceIcon;

            [Header("После выбора")]
            [Tooltip("Картинка, которая показывается ПОСЛЕ выбора (A0 → A1)")]
            public Sprite resultImage;
            [Tooltip("Текст, который показывается после выбора")]
            [TextArea(2, 6)]
            public string resultText;

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
            SetStartingScenario,
            SetGender,
            SetStrikes,
            UnlockRegion,
            SetSpriteCollection
        }
    }
}
