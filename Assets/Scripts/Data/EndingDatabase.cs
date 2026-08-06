using System;
using System.Collections.Generic;
using CinematicSystem;
using DialogueSystem.Data;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// База данных концовок игры (ScriptableObject).
    /// Заполняется в инспекторе: каждая концовка — отдельная EndingEntry.
    /// </summary>
    [CreateAssetMenu(fileName = "EndingDatabase", menuName = "Bureau/Ending System/Database")]
    public class EndingDatabase : ScriptableObject
    {
        public List<EndingEntry> endings = new List<EndingEntry>();

        public EndingEntry Find(string endingID)
        {
            if (string.IsNullOrEmpty(endingID)) return null;
            return endings.Find(e => e != null && e.endingID == endingID);
        }
    }

    [Serializable]
    public class EndingEntry
    {
        [Tooltip("Уникальный ID концовки. Соответствует ключу доминирующей черты (Law/Empathy/Mask/Ambition) или 'Dismissal' для отстранения.")]
        public string endingID;

        [Tooltip("Отображаемое название (например, 'Железная рука')")]
        public string displayName;

        [TextArea(3, 6)]
        [Tooltip("Описание концовки, показываемое на последней странице книги учёта.")]
        public string description;

        [Tooltip("Финальная иллюстрация")]
        public Sprite endingImage;

        [Tooltip("Музыка для этой концовки (проигрывается через MusicPlayer.PlayOneShotTrack)")]
        public AudioClip endingMusic;

        [Tooltip("Опциональный дополнительный диалог после книги учёта")]
        public DialogueGraph endingDialogue;

        [Tooltip("Опциональная катсцена после книги учёта")]
        public CinematicGraph endingCinematic;

        [Tooltip("true для экрана отстранения (отдельный UI, не книга)")]
        public bool isDismissal;
    }
}
