// Файл: Assets/Scripts/Utilities/ActionDiary.cs
// Система логирования действий персонажей с дельта-таймингами для отладки Utility AI
using UnityEngine;
using System.Collections.Generic;

public class ActionDiary : MonoBehaviour
{
    /// <summary>
    /// Структура записи в дневнике действий
    /// </summary>
    public struct DiaryEntry
    {
        public string message;
        public float timeSinceLastEvent;
        public string absoluteTime;

        public DiaryEntry(string msg, float delta, string absTime)
        {
            message = msg;
            timeSinceLastEvent = delta;
            absoluteTime = absTime;
        }

        /// <summary>
        /// Форматированная строка для отображения: [14:05:12] (+3.2 сек) -> Сообщение
        /// </summary>
        public string ToDisplayString()
        {
            string deltaStr = timeSinceLastEvent >= 0f ? $"+{timeSinceLastEvent:F1} сек" : "0.0 сек";
            return $"[{absoluteTime}] ({deltaStr}) -> {message}";
        }

        /// <summary>
        /// Строка для экспорта в файл
        /// </summary>
        public string ToExportString()
        {
            string deltaStr = timeSinceLastEvent >= 0f ? $"+{timeSinceLastEvent:F2}" : "0.00";
            return $"[{absoluteTime}] (+{deltaStr}s) | {message}";
        }
    }

    /// <summary>
    /// Полная история всех событий за сессию
    /// </summary>
    [HideInInspector] public List<DiaryEntry> fullHistory = new List<DiaryEntry>();

    /// <summary>
    /// Время последнего записанного события
    /// </summary>
    private float lastEventTime = 0f;

    /// <summary>
    /// Записывает событие в дневник
    /// </summary>
    /// <param name="msg">Сообщение о событии</param>
    /// <remarks>
    /// - Не вызывает Debug.Log() - только накопление в памяти списка
    /// - Защита от спама: если сообщение идентично последнему, игнорируется
    /// - Рассчитывает дельту времени с момента последнего события
    /// </remarks>
    public void LogEvent(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;

        // Защита от спама - не дублируем подряд одинаковые сообщения
        if (fullHistory.Count > 0 && fullHistory[fullHistory.Count - 1].message == msg)
        {
            return;
        }

        // Рассчитываем дельту времени
        float delta = fullHistory.Count == 0 ? 0f : Time.time - lastEventTime;

        // Формируем запись
        string absoluteTime = System.DateTime.Now.ToString("HH:mm:ss");
        DiaryEntry entry = new DiaryEntry(msg, delta, absoluteTime);

        // Добавляем в историю
        fullHistory.Add(entry);

        // Обновляем время последнего события
        lastEventTime = Time.time;
    }

    /// <summary>
    /// Возвращает количество записей в дневнике
    /// </summary>
    public int EntryCount => fullHistory.Count;

    /// <summary>
    /// Возвращает все записи в виде списка
    /// </summary>
    public IReadOnlyList<DiaryEntry> GetAllEntries()
    {
        return fullHistory;
    }

    /// <summary>
    /// Возвращает последние N записей
    /// </summary>
    public List<DiaryEntry> GetLastEntries(int count)
    {
        if (count <= 0) return new List<DiaryEntry>();
        if (count >= fullHistory.Count) return new List<DiaryEntry>(fullHistory);

        int startIndex = fullHistory.Count - count;
        return fullHistory.GetRange(startIndex, count);
    }

    /// <summary>
    /// Возвращает всю историю в виде одной экспорто-строки (базовый метод)
    /// </summary>
    public string GetExportString()
    {
        if (fullHistory.Count == 0) return "История пуста.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== ДНЕВНИК ДЕЙСТВИЙ: {gameObject.name} ===");
        sb.AppendLine($"Всего записей: {fullHistory.Count}");
        sb.AppendLine($"Начало логирования: {fullHistory[0].absoluteTime}");
        sb.AppendLine(new string('-', 60));

        foreach (var entry in fullHistory)
        {
            sb.AppendLine(entry.ToExportString());
        }

        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"Конец записи: {System.DateTime.Now:HH:mm:ss}");

        return sb.ToString();
    }

    /// <summary>
    /// Возвращает всю историю в виде одной экспорто-строки с указанным заголовком
    /// </summary>
    /// <param name="characterName">Имя персонажа для заголовка</param>
    /// <param name="roleInfo">Дополнительная информация (роль, талон и т.д.)</param>
    public string GetExportString(string characterName, string roleInfo)
    {
        if (fullHistory.Count == 0) return "История пуста.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== ДНЕВНИК ДЕЙСТВИЙ: {characterName} ({roleInfo}) ===");
        sb.AppendLine($"Всего записей: {fullHistory.Count}");
        sb.AppendLine($"Начало логирования: {fullHistory[0].absoluteTime}");
        sb.AppendLine(new string('-', 60));

        foreach (var entry in fullHistory)
        {
            // Формат: [14:05:12] (+3.20s) | Сообщение
            string deltaStr = entry.timeSinceLastEvent >= 0f ? $"+{entry.timeSinceLastEvent:F2}" : "0.00";
            sb.AppendLine($"[{entry.absoluteTime}] ({deltaStr}s) | {entry.message}");
        }

        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"Конец записи: {System.DateTime.Now:HH:mm:ss}");

        return sb.ToString();
    }

    /// <summary>
    /// Очищает всю историю
    /// </summary>
    public void ClearHistory()
    {
        fullHistory.Clear();
        lastEventTime = 0f;
    }

    /// <summary>
    /// Возвращает суммарное время всех дельт (общая продолжительность сессии)
    /// </summary>
    public float GetTotalSessionTime()
    {
        if (fullHistory.Count == 0) return 0f;
        return fullHistory[fullHistory.Count - 1].timeSinceLastEvent + 
               (fullHistory.Count > 1 ? GetSumOfDeltas() - fullHistory[fullHistory.Count - 1].timeSinceLastEvent : 0f);
    }

    private float GetSumOfDeltas()
    {
        float sum = 0f;
        for (int i = 1; i < fullHistory.Count; i++)
        {
            sum += fullHistory[i].timeSinceLastEvent;
        }
        return sum;
    }
}
