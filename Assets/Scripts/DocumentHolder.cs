using UnityEngine;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(CharacterStateLogger))]
public class DocumentHolder : MonoBehaviour
{
    [Tooltip("Точка, где будет появляться префаб документа")]
    public Transform handPoint;
    
    [Header("Префабы документов")]
    public GameObject form1Prefab;
    public GameObject form2Prefab;
    public GameObject certificate1Prefab;
    public GameObject certificate2Prefab;

    private GameObject currentDocumentObject;
    private DocumentType currentDocumentType = DocumentType.None;
    private CharacterStateLogger logger;

    void Awake()
    {
        logger = GetComponent<CharacterStateLogger>();
    }

    public DocumentType GetCurrentDocumentType() => currentDocumentType;

    // Этот метод используется, когда документ появляется в руке "из ниоткуда"
    public void SetDocument(DocumentType newType)
    {
        if (currentDocumentObject != null)
        {
            Destroy(currentDocumentObject);
        }

		GetComponent<ClientPathfinding>().documentChecked = false;


        currentDocumentType = newType;
        GameObject prefabToSpawn = GetPrefabForType(newType);
        string logMessage = "";

        if(newType != DocumentType.None)
        {
            logMessage = $"Получен документ: {newType}";
        }

        if (prefabToSpawn != null)
        {
            currentDocumentObject = Instantiate(prefabToSpawn, handPoint.position, handPoint.rotation, handPoint);
        }
        
        if (!string.IsNullOrEmpty(logMessage) && logger != null)
        {
            logger.LogState(logMessage);
        }
    }

    // --- НОВЫЙ МЕТОД: для "принятия" прилетевшего документа ---
    public void ReceiveTransferredDocument(DocumentType newType, GameObject transferredObject)
    {
        // Уничтожаем старый документ, если он вдруг остался
        if (currentDocumentObject != null)
        {
            Destroy(currentDocumentObject);
        }

        // Обновляем состояние и сохраняем ссылку на новый объект
        currentDocumentType = newType;
        currentDocumentObject = transferredObject;
        
        // Прикрепляем прилетевший документ к руке
        if (currentDocumentObject != null)
        {
            currentDocumentObject.transform.SetParent(handPoint, true);
            currentDocumentObject.transform.localPosition = Vector3.zero;
            currentDocumentObject.transform.localRotation = Quaternion.identity;
        }

        // Логируем получение
        string logMessage = $"Получен документ: {newType}";
        if (!string.IsNullOrEmpty(logMessage) && logger != null)
        {
            logger.LogState(logMessage);
        }
    }

    // Вспомогательный метод для получения нужного префаба
    public GameObject GetPrefabForType(DocumentType docType)
    {
        switch (docType)
        {
            case DocumentType.Form1: return form1Prefab;
            case DocumentType.Form2: return form2Prefab;
            case DocumentType.Certificate1: return certificate1Prefab;
            case DocumentType.Certificate2: return certificate2Prefab;
            default: return null;
        }
    }
}