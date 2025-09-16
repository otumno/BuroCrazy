using UnityEngine;
using System.Collections.Generic;

public class SaveLoadPanelController : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;
    [Tooltip("Перетащите сюда пустые объекты, в которых должны появиться слоты")]
    [SerializeField] private List<Transform> slotPositions;

    private void OnEnable()
    {
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        foreach (Transform position in slotPositions)
        {
            if (position.childCount > 0)
            {
                Destroy(position.GetChild(0).gameObject);
            }
        }

        // --- ДИАГНОСТИКА ---
        if (SaveLoadManager.Instance == null)
        {
            Debug.LogError("SaveLoadManager НЕ НАЙДЕН! Слоты не могут быть созданы.");
            return;
        }
        if (slotPrefab == null)
        {
            Debug.LogError("ПРЕФАБ слота не назначен в инспекторе! Слоты не могут быть созданы.");
            return;
        }

        Debug.Log($"[SaveLoadPanel] Начинаем создание слотов. Количество слотов в менеджере: {SaveLoadManager.Instance.numberOfSlots}. Количество позиций: {slotPositions.Count}");
        // --- КОНЕЦ ДИАГНОСТИКИ ---

        for (int i = 0; i < slotPositions.Count; i++)
        {
            if (i >= SaveLoadManager.Instance.numberOfSlots)
            {
                Debug.LogWarning($"Позиция {i} пропущена, так как в SaveLoadManager указано только {SaveLoadManager.Instance.numberOfSlots} слотов.");
                break;
            }

            Debug.Log($"Создаем слот #{i} в позиции '{slotPositions[i].name}'...");
            GameObject newSlot = Instantiate(slotPrefab, slotPositions[i]);
            SaveSlotUI slotUI = newSlot.GetComponent<SaveSlotUI>();
            if (slotUI != null)
            {
                slotUI.Setup(i);
                Debug.Log($"...Слот #{i} успешно настроен.");
            }
            else
            {
                Debug.LogError($"...ОШИБКА! На префабе слота отсутствует скрипт SaveSlotUI!");
            }
        }
    }
}