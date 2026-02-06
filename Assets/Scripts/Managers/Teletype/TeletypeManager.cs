//Assets/Scripts/Managers/Teletype/TeletypeManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UI.Teletype;

namespace Managers.Teletype
{
    public class TeletypeManager : MonoBehaviour
    {
        public static TeletypeManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private TeletypeTerminalUI terminalUI;

        [Header("Настройки")]
        [SerializeField] private int maxHistoryCount = 50;
        [SerializeField] private int visibleCount = 3;

        // Флаг готовности: система начнет выводить сообщения только когда день начат
        private bool isSystemActive = false; 
        private bool isProcessing = false;

        private readonly Queue<TeletypeMessage> _messageQueue = new();
        private readonly List<TeletypeMessage> _messageHistory = new();

        public void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        // Вызывайте этот метод из MainUIManager.cs в StartOrResumeGameplay()
        public void ActivateSystem()
        {
            isSystemActive = true;
            Debug.Log("[TeletypeManager] Система активирована. Начинаю обработку очереди.");
            if (!isProcessing && _messageQueue.Count > 0)
            {
                StartCoroutine(ProcessQueue());
            }
        }

        public void Log(string message, bool persist = false, TeletypeMessageType messageType = TeletypeMessageType.Info)
        {
            EnqueueMessage(message, messageType, persist);
        }

        public void LogImportant(string message, bool persist = true)
        {
            EnqueueMessage(message, TeletypeMessageType.Important, persist);
        }

        public void LogPolicy(string policyName, bool isActivation)
        {
            string action = isActivation ? "ВВЕДЕН" : "ОТМЕНЕН";
            EnqueueMessage($"УКАЗ {action}: {policyName}", TeletypeMessageType.Policy, persist: true);
        }

        public void LogStaffWork(string name, string role, bool isStartShift)
        {
            string action = isStartShift ? "вышел на работу" : "закончил смену";
            EnqueueMessage($"{name} ({role}) {action}", TeletypeMessageType.StaffWork, persist: false);
        }

        private void EnqueueMessage(string text, TeletypeMessageType type, bool persist)
        {
            var msg = new TeletypeMessage(text, type, DateTime.Now, persist);
            _messageQueue.Enqueue(msg);

            // Запускаем обработку только если система активна
            if (!isProcessing && isSystemActive)
            {
                StartCoroutine(ProcessQueue());
            }
        }

        private System.Collections.IEnumerator ProcessQueue()
        {
            isProcessing = true;

            while (_messageQueue.Count > 0)
            {
                // Если по какой-то причине систему выключили (например, конец дня)
                if (!isSystemActive) break;

                var msg = _messageQueue.Dequeue();
                AddToHistory(msg);

                if (terminalUI)
                    terminalUI.AddMessage(msg);

                yield return new WaitForSeconds(0.4f); // Небольшая задержка между сообщениями в пачке
            }

            isProcessing = false;
        }

        private void AddToHistory(TeletypeMessage msg)
        {
            _messageHistory.Add(msg);
            if (_messageHistory.Count > maxHistoryCount)
            {
                var oldest = _messageHistory.FirstOrDefault(m => !m.Persistant);
                if (oldest != null) _messageHistory.Remove(oldest);
                else _messageHistory.RemoveAt(0);
            }
        }

        // Очистка при смене дня
        public void DeactivateAndClear()
        {
            isSystemActive = false;
            _messageQueue.Clear();
            if (terminalUI) terminalUI.ClearAll();
        }
    }
}