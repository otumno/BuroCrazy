// Файл: MessManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MessManager : MonoBehaviour
{
    public static MessManager Instance { get; private set; }

    [Header("Ограничения")]
    [Tooltip("Максимальное количество объектов беспорядка на сцене.")]
    public int maxTotalMesses = 100;

    private List<MessPoint> allMesses = new List<MessPoint>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    /// <summary>
    /// Проверяет, можно ли создать еще один объект беспорядка.
    /// </summary>
    public bool CanCreateMess()
    {
        return allMesses.Count < maxTotalMesses;
    }

    public void RegisterMess(MessPoint mess)
    {
        if (!allMesses.Contains(mess))
        {
            allMesses.Add(mess);
        }
    }

    public void UnregisterMess(MessPoint mess)
    {
        if (allMesses.Contains(mess))
        {
            allMesses.Remove(mess);
        }
    }

    public List<MessPoint> GetSortedMessList(Vector3 position)
    {
        if (allMesses.Count == 0) return new List<MessPoint>();

        return allMesses
            .Where(m => m != null) // Добавим проверку на случай, если объект был уничтожен некорректно
            .OrderBy(m => Vector3.Distance(position, m.transform.position))
            .ToList();
    }
}