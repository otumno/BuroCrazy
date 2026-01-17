// Assets/Scripts/Data/SmallTalkDatabase.cs
using UnityEngine;
using System.Collections.Generic;
using Data.Calendar; // Нужно для CalendarDayPeriodType

namespace Data
{
    [CreateAssetMenu(fileName = "SmallTalkDatabase", menuName = "Bureau/Small Talk Database")]
    public class SmallTalkDatabase : ScriptableObject
    {
        [Header("Приветствия: Утро")]
        public List<string> morningGreetings = new List<string> { "Доброе утро.", "Кофе бы...", "Началось...", "Свежо сегодня.", "Привет." };

        [Header("Приветствия: День")]
        public List<string> dayGreetings = new List<string> { "Добрый день.", "Привет.", "Как работа?", "Дел по горло.", "Салют." };

        [Header("Приветствия: Вечер/Ночь")]
        public List<string> eveningGreetings = new List<string> { "Доброй ночи.", "Тихо тут...", "Не спишь?", "Привет.", "Осторожнее в темноте." };

        [Header("Реакции на Директора (Всегда)")]
        public List<string> directorGreetings = new List<string> { "Шеф!", "Работаем, шеф!", "Здравствуйте, босс!", "Я занят делом!", "Всё под контролем." };

        [Header("Жалобы (Small Talk)")]
        public List<string> complaints = new List<string> { "Спина болит...", "Хочу кофе...", "Когда зарплата?", "Опять клиенты...", "Душно тут.", "Принтер заело." };
        
        [Header("Позитив (Small Talk)")]
        public List<string> positiveRemarks = new List<string> { "Хорошая смена.", "Вроде справляемся.", "Скоро перерыв!", "Нормально сидим." };

        /// <summary>
        /// Возвращает приветствие, соответствующее текущему времени суток.
        /// </summary>
        public string GetGreetingForTime(CalendarDayPeriodType period)
        {
            // Проверяем флаги времени
            if ((period & CalendarDayPeriodType.Morning) != 0) return GetRandom(morningGreetings);
            
            if ((period & CalendarDayPeriodType.EarlyDay) != 0 || 
                (period & CalendarDayPeriodType.Noon) != 0 || 
                (period & CalendarDayPeriodType.Day) != 0 ||
                (period & CalendarDayPeriodType.LateDay) != 0) 
                return GetRandom(dayGreetings);

            if ((period & CalendarDayPeriodType.Evening) != 0 || 
                (period & CalendarDayPeriodTypeExtensions.FullNight) != 0) 
                return GetRandom(eveningGreetings);

            return "Привет."; // Фолбэк
        }

        public string GetRandomDirectorReaction() => GetRandom(directorGreetings);
        
        public string GetRandomChatter()
        {
            return (Random.value < 0.7f) ? GetRandom(complaints) : GetRandom(positiveRemarks);
        }

        private string GetRandom(List<string> list)
        {
            if (list == null || list.Count == 0) return "...";
            return list[Random.Range(0, list.Count)];
        }
    }
}