using UnityEngine;
using System.Collections.Generic;

public class DirtGridManager : MonoBehaviour
{
    public static DirtGridManager Instance { get; private set; }

    [Header("Настройки сетки")]
    [Tooltip("Размер одной ячейки в мировых координатах. Обычно равен 1.")]
    public float cellSize = 1f;

    [Header("Настройки загрязнения")]
    [Tooltip("Список префабов грязи, из которых будет выбираться случайный.")]
    public List<GameObject> dirtOverlayPrefabs;
    [Tooltip("Сколько 'шагов' нужно сделать по ячейке, чтобы уровень грязи повысился. 4 значения = 5 уровней (чисто -> ужас).")]
    public int[] trafficThresholds = new int[] { 20, 50, 100, 200 };
    
    private Dictionary<Vector2Int, int> trafficCounts = new Dictionary<Vector2Int, int>();
    private Dictionary<Vector2Int, MessPoint> dirtObjects = new Dictionary<Vector2Int, MessPoint>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }
    
    public void AddTraffic(Vector3 worldPosition)
    {
        if (dirtOverlayPrefabs == null || dirtOverlayPrefabs.Count == 0) return;

        Vector2Int gridPosition = WorldToGrid(worldPosition);

        if (!trafficCounts.ContainsKey(gridPosition))
        {
            trafficCounts[gridPosition] = 0;
        }
        trafficCounts[gridPosition]++;

        int currentTraffic = trafficCounts[gridPosition];
        int newLevel = 0;
        
        for (int i = 0; i < trafficThresholds.Length; i++)
        {
            if (currentTraffic >= trafficThresholds[i])
            {
                newLevel = i + 1;
            }
        }
        
        if (newLevel > 0)
        {
            UpdateDirtVisuals(gridPosition, newLevel);
        }
    }

    private void UpdateDirtVisuals(Vector2Int gridPosition, int level)
    {
        if (dirtObjects.ContainsKey(gridPosition))
        {
            MessPoint dirtMess = dirtObjects[gridPosition];
            if (dirtMess != null)
            {
                dirtMess.dirtLevel = level;
            }
            else
            {
                dirtObjects.Remove(gridPosition);
            }
        }
        else
        {
            Vector3 cellCenter = GridToWorld(gridPosition);
            float randomRadius = cellSize / 2f;
            Vector2 randomOffset = Random.insideUnitCircle * randomRadius;
            Vector3 finalPosition = cellCenter + (Vector3)randomOffset;
            
            GameObject randomDirtPrefab = dirtOverlayPrefabs[Random.Range(0, dirtOverlayPrefabs.Count)];
            GameObject dirtGO = Instantiate(randomDirtPrefab, finalPosition, Quaternion.identity, transform);
            
            MessPoint newDirtMess = dirtGO.GetComponent<MessPoint>();
            if (newDirtMess != null)
            {
                newDirtMess.dirtLevel = level;
                dirtObjects[gridPosition] = newDirtMess;
            }
        }
    }
    
    private Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / cellSize);
        int y = Mathf.FloorToInt(worldPosition.y / cellSize);
        return new Vector2Int(x, y);
    }

    private Vector3 GridToWorld(Vector2Int gridPosition)
    {
        return new Vector3(gridPosition.x * cellSize + cellSize / 2, gridPosition.y * cellSize + cellSize / 2, 0);
    }
}