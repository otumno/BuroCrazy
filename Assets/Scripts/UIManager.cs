// Файл: UIManager.cs
using UnityEngine;
using TMPro;
using System.Linq; // Добавлено для работы со списками
using Managers;

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

    private float nextRefresh;

    void Update() 
    { 
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        totalClientsText.text = UI.GameText.Get("desk.total_visitors", ClientPathfinding.totalClients);
        clientsExitedText.text = UI.GameText.Get("desk.exited", ClientPathfinding.clientsExited);
        clientsInWaitingText.text = UI.GameText.Get("desk.waiting", ClientPathfinding.clientsInWaiting);
        clientsToToiletText.text = UI.GameText.Get("desk.toilet", ClientPathfinding.clientsToToilet);
        clientsToRegistrationText.text = UI.GameText.Get("desk.at_counters", ClientPathfinding.clientsToRegistration);
        clientsConfusedText.text = UI.GameText.Get("desk.confused", ClientPathfinding.clientsConfused);
        if(clientsExitedAngryText != null) clientsExitedAngryText.text = UI.GameText.Get("desk.angry", ClientPathfinding.clientsExitedAngry);
        if(clientsExitedProcessedText != null) clientsExitedProcessedText.text = UI.GameText.Get("desk.processed", ClientPathfinding.clientsExitedProcessed);
        
        // --- ИЗМЕНЕНИЕ: Новая логика отображения списка номеров ---
        if(nowServingText != null && ClientQueueManager.Instance != null)
        {
            var registrarWorker = ClientSpawner.GetServiceProviderAtDesk(0);
            if (registrarWorker == null)
            {
                nowServingText.text = UI.GameText.Get("desk.registration_closed");
            }
            else
            {
                // Берем только нормальные номера (меньше 10000)
                var numbers = ClientQueueManager.Instance.currentlyCalledNumbers.Where(n => n < 10000).ToList();
                if (numbers.Count > 0)
                {
                    numbers.Sort();
                    nowServingText.text = UI.GameText.Get("desk.calling", string.Join(", ", numbers));
                }
                else
                {
                    nowServingText.text = UI.GameText.Get("desk.registration_free");
                }
            }
        }
    }
}