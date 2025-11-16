// Файл: DirtGridManager.cs
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Управляет невидимой сеткой на полу, отслеживая движение персонажей
/// и создавая объекты грязи в местах с высокой проходимостью.
/// </summary>
public class DirtGridManager : MonoBehaviour
{
    public static DirtGridManager Instance { get; private set; }

    [Header("Настройки сетки")]
    [Tooltip("Размер одной ячейки в мировых координатах. Обычно равен 1.")]
    public float cellSize = 1f;

    [Header("Настройки загрязнения")]
    [Tooltip("Префаб объекта 'Грязь', который будет появляться на полу.")]
    public GameObject dirtOverlayPrefab;
    [Tooltip("Сколько 'шагов' нужно сделать по ячейке, чтобы уровень грязи повысился. 4 значения = 5 уровней (чисто -> ужас).")]
    public int[] trafficThresholds = new int[] { 20, 50, 100, 200 };

    // Словарь для отслеживания "проходимости" каждой ячейки: ключ - координата, значение - кол-во шагов.
    private Dictionary<Vector2Int, int> trafficCounts = new Dictionary<Vector2Int, int>();
    // Словарь для хранения ссылок на уже созданные объекты грязи, чтобы не создавать их повторно.
    private Dictionary<Vector2Int, MessPoint> dirtObjects = new Dictionary<Vector2Int, MessPoint>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    /// <summary>
    /// Главный метод. Вызывается персонажами при ходьбе, чтобы "отметить" свой шаг.
    /// </summary>
    public void AddTraffic(Vector3 worldPosition)
    {
        if (dirtOverlayPrefab == null) return;

        Vector2Int gridPosition = WorldToGrid(worldPosition);

        if (!trafficCounts.ContainsKey(gridPosition))
        {
            trafficCounts[gridPosition] = 0;
        }
        trafficCounts[gridPosition]++;

        int currentTraffic = trafficCounts[gridPosition];
        int newLevel = 0;

        // Определяем текущий уровень грязи по порогам
        for (int i = 0; i < trafficThresholds.Length; i++)
        {
            if (currentTraffic >= trafficThresholds[i])
            {
                newLevel = i + 1;
            }
        }
        
        // Если ячейка стала грязной, обновляем или создаем объект грязи
        if (newLevel > 0)
        {
            UpdateDirtVisuals(gridPosition, newLevel);
        }
    }

    private void UpdateDirtVisuals(Vector2Int gridPosition, int level)
    {
        if (dirtObjects.ContainsKey(gridPosition))
        {
            // Если грязь уже есть, просто обновляем ее уровень
            MessPoint dirtMess = dirtObjects[gridPosition];
            if (dirtMess != null)
            {
                dirtMess.dirtLevel = level;
                // Тут можно добавить логику смены спрайта в зависимости от уровня, если нужно
            }
            else
            {
                // Если объект был уничтожен (уборщиком), но остался в словаре, удаляем его
                dirtObjects.Remove(gridPosition);
            }
        }
        else
        {
            // Если грязи нет, создаем новый объект
            Vector3 worldPos = GridToWorld(gridPosition);
            GameObject dirtGO = Instantiate(dirtOverlayPrefab, worldPos, Quaternion.identity, transform);
            MessPoint newDirtMess = dirtGO.GetComponent<MessPoint>();
            
            if (newDirtMess != null)
            {
                newDirtMess.dirtLevel = level;
                dirtObjects[gridPosition] = newDirtMess;
            }
        }
    }

    // Вспомогательные методы для конвертации мировых координат в координаты сетки и обратно
    private Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / cellSize);
        int y = Mathf.FloorToInt(worldPosition.y / cellSize);
        return new Vector2Int(x, y);
    }

    private Vector3 GridToWorld(Vector2Int gridPosition)
    {
        // Создаем объект в центре ячейки
        return new Vector3(gridPosition.x * cellSize + cellSize / 2, gridPosition.y * cellSize + cellSize / 2, 0);
    }
}