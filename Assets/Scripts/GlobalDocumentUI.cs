// Файл: GlobalDocumentUI.cs
using UnityEngine;
using TMPro;

/// <summary>
/// Обновляет UI элемент, отображающий общее количество документов в учреждении.
/// </summary>
public class GlobalDocumentUI : MonoBehaviour
{
    [Header("UI Компоненты")]
    [Tooltip("Текстовое поле для вывода статуса документов")]
    public TextMeshProUGUI documentStatusText;

    [Header("Настройки цвета")]
    public Color normalColor = Color.white;
    public Color limitReachedColor = Color.red;

    void Update()
    {
        if (documentStatusText == null || DocumentManager.Instance == null)
        {
            return;
        }

        int currentDocs = DocumentManager.Instance.CurrentTotalDocuments;
        int maxDocs = DocumentManager.Instance.maxTotalDocuments;
        string status = DocumentManager.Instance.GetDocumentStatusText();

        documentStatusText.text = $"{status} ({currentDocs}/{maxDocs})";

        // Подсвечиваем красным, если достигли или превысили лимит
        documentStatusText.color = (currentDocs >= maxDocs) ? limitReachedColor : normalColor;
    }
}