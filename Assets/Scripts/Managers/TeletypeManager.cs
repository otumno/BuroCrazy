using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using Data.Calendar;
using UI;

namespace Managers
{
    public class TeletypeManager : MonoBehaviour
    {
        public static TeletypeManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private TeletypeStripUI stripUI;

        [Header("Настройки")]
        [SerializeField] private int maxHistoryCount = 50;
        [SerializeField] private int visibleCount = 3;

        private Queue<TeletypeMessage> messageQueue = new Queue<TeletypeMessage>();
        private List<TeletypeMessage> messageHistory = new List<TeletypeMessage>();
        private bool isProcessing = false;

        public class TeletypeMessage
        {
            public string text;
            public TeletypeMessageType type;
            public System.DateTime timestamp;
            public bool persist;
        }

        public enum TeletypeMessageType { Info, Warning, Success, Important, Policy }

        public void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        public void Log(string message, bool persist = false)
        {
            EnqueueMessage(message, TeletypeMessageType.Info, persist);
        }

        public void LogWarning(string message, bool persist = false)
        {
            EnqueueMessage(message, TeletypeMessageType.Warning, persist);
        }

        public void LogSuccess(string message, bool persist = false)
        {
            EnqueueMessage(message, TeletypeMessageType.Success, persist);
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

        private void EnqueueMessage(string text, TeletypeMessageType type, bool persist)
        {
            var msg = new TeletypeMessage
            {
                text = text,
                type = type,
                timestamp = System.DateTime.Now,
                persist = persist
            };

            messageQueue.Enqueue(msg);
            
            if (!isProcessing)
            {
                StartCoroutine(ProcessQueue());
            }
        }

        private System.Collections.IEnumerator ProcessQueue()
        {
            isProcessing = true;

            while (messageQueue.Count > 0)
            {
                var msg = messageQueue.Dequeue();
                
                AddToHistory(msg);
                stripUI?.AddMessage(msg);

                yield return new WaitForSeconds(0.3f);
            }

            isProcessing = false;
        }

        private void AddToHistory(TeletypeMessage msg)
        {
            messageHistory.Add(msg);
            
            while (messageHistory.Count > maxHistoryCount)
            {
                var oldest = messageHistory.FirstOrDefault(m => !m.persist);
                if (oldest != null)
                {
                    messageHistory.Remove(oldest);
                }
                else
                {
                    messageHistory.RemoveAt(0);
                }
            }
        }

        public List<TeletypeMessage> GetVisibleMessages()
        {
            return messageHistory.TakeLast(visibleCount).ToList();
        }

        public List<TeletypeMessage> GetAllHistory()
        {
            return messageHistory.ToList();
        }
    }
}
