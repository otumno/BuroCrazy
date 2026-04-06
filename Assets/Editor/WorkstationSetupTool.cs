using UnityEngine;
using UnityEditor;

public class WorkstationSetupTool
{
    [MenuItem("GameObject/Bureau/Авто-настройка рабочего места", false, 10)]
    public static void SetupWorkstations()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Сначала выделите объекты столов/стульев в иерархии!");
            return;
        }

        Material highlightMat = null;
        string[] guids = AssetDatabase.FindAssets("Mat_HighlightAdditive t:Material");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            highlightMat = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        else
        {
            Debug.LogError("Материал 'Mat_HighlightAdditive' не найден в проекте! Создайте его для корректной работы подсветки.");
        }

        int successCount = 0;

        foreach (GameObject obj in selectedObjects)
        {
            Undo.RegisterFullObjectHierarchyUndo(obj, "Setup Workstation");

            // 1. Проверяем родительский SpriteRenderer
            SpriteRenderer parentSr = obj.GetComponent<SpriteRenderer>();
            if (parentSr == null)
            {
                Debug.LogWarning($"Объект {obj.name} пропущен: нет SpriteRenderer.");
                continue;
            }

            // 2. Устанавливаем Z = -1 и Ордер = 1 родителю
            Vector3 pos = obj.transform.position;
            pos.z = -1f;
            obj.transform.position = pos;
            parentSr.sortingOrder = 1;

            // 3. Добавляем скрипт-триггер (BoxCollider2D добавится автоматически)
            if (obj.GetComponent<WorkstationClickTrigger>() == null)
            {
                Undo.AddComponent<WorkstationClickTrigger>(obj);
            }

            BoxCollider2D col = obj.GetComponent<BoxCollider2D>();
            if (col != null) col.isTrigger = true;

            // 4. Очищаем старую дочку, если она была
            Transform existingHighlight = obj.transform.Find("highlight");
            if (existingHighlight != null)
            {
                Undo.DestroyObjectImmediate(existingHighlight.gameObject);
            }

            // 5. Создаем дочку "highlight"
            GameObject childObj = new GameObject("highlight");
            Undo.RegisterCreatedObjectUndo(childObj, "Create Highlight Child");
            
            childObj.transform.SetParent(obj.transform);
            
            // <--- ИЗМЕНЕНИЕ: Жестко задаем локальный Z = -1 для дочки
            childObj.transform.localPosition = new Vector3(0f, 0f, -1f); 
            childObj.transform.localRotation = Quaternion.identity;
            childObj.transform.localScale = Vector3.one;

            // 6. Настраиваем SpriteRenderer дочки
            SpriteRenderer childSr = childObj.AddComponent<SpriteRenderer>();
            childSr.sprite = parentSr.sprite; 
            childSr.sortingLayerID = parentSr.sortingLayerID; 
            childSr.sortingOrder = 2; // Ордер 2
            childSr.color = new Color(1f, 1f, 1f, 1f);

            if (highlightMat != null)
            {
                childSr.material = highlightMat;
            }

            // <--- ИЗМЕНЕНИЕ: Оставляем включенным для удобства в редакторе!
            childSr.enabled = true; 

            successCount++;
        }

        Debug.Log($"<color=green>Авто-настройка завершена! Успешно обработано объектов: {successCount}</color>");
    }
}
