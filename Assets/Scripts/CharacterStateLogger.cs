using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CharacterStateLogger : MonoBehaviour
{
    public readonly List<string> stateHistory = new List<string>();
    public int maxHistory = 15;

    public void LogState(string stateInfo)
    {
        // Не добавляем в историю дубликаты подряд
        if (stateHistory.Count > 0 && stateHistory.Last().EndsWith(stateInfo))
        {
            return;
        }

        stateHistory.Add($"[{System.DateTime.Now:HH:mm:ss}] {stateInfo}");
        if (stateHistory.Count > maxHistory)
        {
            stateHistory.RemoveAt(0);
        }
    }

    /// <summary>
    /// Возвращает всю историю в виде одной строки.
    /// </summary>
    public string GetFormattedHistory()
    {
        // Вернем только 14 последних записей, так как последняя будет в "текущем статусе"
        var historyToShow = stateHistory.AsEnumerable().Reverse().Skip(1);
        return string.Join("\n", historyToShow);
    }

    /// <summary>
    /// Возвращает только самое последнее состояние.
    /// </summary>
    public string GetCurrentStatus()
    {
        if (stateHistory.Count > 0)
        {
            return stateHistory.Last();
        }
        return "Нет данных";
    }
}