using UnityEngine;
using System.Collections.Generic;
using Managers;

public class SaveLoadPanelController : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;
    [Tooltip("Перетащите сюда пустые объекты, в которых должны появиться слоты")]
    [SerializeField] private List<Transform> slotPositions;

    private readonly List<SaveSlotUI> _saveSlots = new();

    private void OnEnable()
    {
        if (SaveLoadManager.Instance.numberOfSlots != slotPositions.Count)
        {
            Debug.LogError($"Число логических слотов для сохранений не равно количеству слотов на экране!" +
                           $" {SaveLoadManager.Instance.numberOfSlots} != {slotPositions.Count}");
            return;
        }
        
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        // clear instantiated slot prefabs
        _saveSlots.ForEach(t => Destroy(t.gameObject));
        _saveSlots.Clear();

        if (SaveLoadManager.Instance == null)
        {
            Debug.LogError("SaveLoadManager НЕ НАЙДЕН. Смотри порядок инициализации!");
            return;
        }

        _saveSlots.Capacity = SaveLoadManager.Instance.numberOfSlots;
        for (int i = 0; i < slotPositions.Count; i++)
        {
            var slotGo = Instantiate(slotPrefab, slotPositions[i], worldPositionStays: false);
            var saveSlotUi = slotGo.GetComponent<SaveSlotUI>();
            if (saveSlotUi == null)
            {
                Debug.LogError("...ОШИБКА! На префабе слота отсутствует скрипт SaveSlotUI!");
                Destroy(slotGo);
                continue;
            }
            
            saveSlotUi.Setup(i);
            _saveSlots.Add(saveSlotUi);
        }
    }
}