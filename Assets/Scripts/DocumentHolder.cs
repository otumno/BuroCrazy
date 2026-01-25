using UnityEngine;
using System.Collections.Generic;
using System;
using Data.Documents;
using Enums;
using Managers;

[RequireComponent(typeof(CharacterStateLogger))]
public class DocumentHolder : MonoBehaviour
{
    [Tooltip("Точка, где будет появляться префаб документа")]
    public Transform handPoint;
    
    [Header("Префабы документов (старые)")]
    public GameObject form1Prefab;
    public GameObject form2Prefab;
    public GameObject certificate1Prefab;
    public GameObject certificate2Prefab;

    [Header("Document Data Database")]
    public List<DocumentData> documentDatabase;

    private GameObject currentDocumentObject;
    private DocumentType currentDocumentType = DocumentType.None;
    private DocumentData currentDocumentData;
    private CharacterStateLogger logger;

    void Awake()
    {
        logger = GetComponent<CharacterStateLogger>();

        if (documentDatabase == null)
        {
            documentDatabase = new List<DocumentData>();
        }
    }

    public DocumentType GetCurrentDocumentType() => currentDocumentType;

    public DocumentData GetCurrentDocumentData()
    {
        return currentDocumentData;
    }

    // Этот метод используется, когда документ появляется в руке "из ниоткуда"
    public void SetDocument(DocumentType newType)
    {
        if (currentDocumentObject != null)
        {
            Destroy(currentDocumentObject);
        }

        GetComponent<ClientPathfinding>().documentChecked = false;

        currentDocumentType = newType;
        currentDocumentData = GetDocumentDataForType(newType);
        
        GameObject prefabToSpawn = GetPrefabForType(newType);
        string logMessage = "";

        if (newType != DocumentType.None)
        {
            logMessage = $"Получен документ: {newType}";
            if (currentDocumentData != null)
            {
                logMessage += $" ({currentDocumentData.subtype})";
            }
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

    // --- НОВЫЙ МЕТОД: Установка документа через DocumentData ---
    public void SetDocument(DocumentData docData)
    {
        if (currentDocumentObject != null)
        {
            Destroy(currentDocumentObject);
        }

        GetComponent<ClientPathfinding>().documentChecked = false;

        currentDocumentData = docData;
        currentDocumentType = docData != null ? docData.documentType : DocumentType.None;

        GameObject prefabToSpawn = GetPrefabForType(currentDocumentType);
        string logMessage = "";

        if (docData != null)
        {
            logMessage = $"Получен документ: {docData.displayName}";
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
        currentDocumentData = GetDocumentDataForType(newType);
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
        if (currentDocumentData != null)
        {
            logMessage += $" ({currentDocumentData.subtype})";
        }
        if (!string.IsNullOrEmpty(logMessage) && logger != null)
        {
            logger.LogState(logMessage);
        }
    }

    // --- НОВЫЙ МЕТОД: Проверка оборудования ---
    public bool HasRequiredEquipment()
    {
        if (currentDocumentData == null) return true;

        if (currentDocumentData.requiredEquipment == EquipmentType.None) return true;

        if (EquipmentManager.Instance != null)
        {
            return EquipmentManager.Instance.HasWorkingEquipment(currentDocumentData.requiredEquipment);
        }

        return false;
    }

    // --- НОВЫЙ МЕТОД: Получить DocumentData по типу ---
    public DocumentData GetDocumentDataForType(DocumentType type)
    {
        if (documentDatabase == null || documentDatabase.Count == 0)
        {
            return null;
        }

        foreach (var doc in documentDatabase)
        {
            if (doc != null && doc.documentType == type)
            {
                return doc;
            }
        }

        return null;
    }

    // --- НОВЫЙ МЕТОД: Получить DocumentData по ID ---
    public DocumentData GetDocumentDataByID(string id)
    {
        if (documentDatabase == null || documentDatabase.Count == 0)
        {
            return null;
        }

        foreach (var doc in documentDatabase)
        {
            if (doc != null && doc.name == id)
            {
                return doc;
            }
        }

        return null;
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