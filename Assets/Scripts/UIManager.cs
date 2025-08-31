// Файл: UIManager.cs
using UnityEngine;
using TMPro;
using System.Linq; // Добавлено для работы со списками

public class UIManager : MonoBehaviour 
{ 
    [Header("Общие счетчики")] 
    public TextMeshProUGUI totalClientsText;
    public TextMeshProUGUI clientsExitedText; 
    public TextMeshProUGUI clientsInWaitingText; 
    public TextMeshProUGUI clientsToToiletText; 
    public TextMeshProUGUI clientsToRegistrationText; 
    public TextMeshProUGUI clientsConfusedText;
    [Header("Детальные счетчики ухода")] 
    public TextMeshProUGUI clientsExitedAngryText; 
    public TextMeshProUGUI clientsExitedProcessedText;
    [Header("UI Регистратуры")] 
    public TextMeshProUGUI nowServingText;

    void Update() 
    { 
        totalClientsText.text = $"Всего: {ClientPathfinding.totalClients}";
        clientsExitedText.text = $"Ушли: {ClientPathfinding.clientsExited}"; 
        clientsInWaitingText.text = $"Ожидают: {ClientPathfinding.clientsInWaiting}"; 
        clientsToToiletText.text = $"Туалет: {ClientPathfinding.clientsToToilet}"; 
        clientsToRegistrationText.text = $"У стоек: {ClientPathfinding.clientsToRegistration}";
        clientsConfusedText.text = $"Потеряшки: {ClientPathfinding.clientsConfused}"; 
        if(clientsExitedAngryText != null) clientsExitedAngryText.text = $"Ушли злыми: {ClientPathfinding.clientsExitedAngry}"; 
        if(clientsExitedProcessedText != null) clientsExitedProcessedText.text = $"Обслужено: {ClientPathfinding.clientsExitedProcessed}";
        
        // --- ИЗМЕНЕНИЕ: Новая логика отображения списка номеров ---
        if(nowServingText != null && ClientQueueManager.Instance != null) 
        { 
            var numbers = ClientQueueManager.Instance.currentlyCalledNumbers;
            if (numbers.Count > 0)
            {
                // Сортируем номера для красивого отображения
                numbers.Sort(); 
                // Объединяем все номера в одну строку через запятую
                nowServingText.text = $"Вызываются: {string.Join(", ", numbers)}";
            }
            else
            {
                nowServingText.text = "Регистратура свободна";
            }
        } 
    }
}