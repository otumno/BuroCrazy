// Файл: DailyMandates.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "DailyMandates", menuName = "My Game/Daily Mandates")]
public class DailyMandates : ScriptableObject
{
    [Header("Нормы дня")]
    [Tooltip("Допустимый процент ошибок в документах директора")]
    [Range(0f, 100f)]
    public float allowedDirectorErrorRate = 5f; // This is the required variable

    [Tooltip("Максимальная загруженность архива")]
    public int maxArchiveDocumentCount = 15;
    
    [Tooltip("Максимальная загруженность столов клерков")]
    public int maxDeskDocumentCount = 10;
    
    [Tooltip("Минимальное количество обслуженных клиентов")]
    public int minProcessedClients = 50;
    
    [Tooltip("Максимальное количество недовольных клиентов")]
    public int maxUpsetClients = 5;
    
    [Tooltip("Список типов документов, которые считаются 'приемлемыми'")]
    public List<DocumentType> acceptableDocumentTypes;
}