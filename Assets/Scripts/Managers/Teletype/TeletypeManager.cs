using System.Collections.Generic;
using System.Linq;
using UI;
using UnityEngine;

namespace Managers.Teletype
{
    public class TeletypeManager : MonoBehaviour
    {
        public static TeletypeManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private TeletypeStripUI stripUI;

        [Header("Настройки")]
        [SerializeField] private int maxHistoryCount = 50;
        [SerializeField] private int visibleCount = 3;

        private bool isProcessing = false;

        private readonly Queue<TeletypeMessage> messageQueue = new();
        private readonly List<TeletypeMessage> messageHistory = new();

        public void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
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
                
                if (stripUI)
                    stripUI.AddMessage(msg);

                yield return new WaitForSeconds(0.3f);
            }

            isProcessing = false;
        }

        private void AddToHistory(TeletypeMessage msg)
        {
            messageHistory.Add(msg);
            
            // вайл не нужзе
            if (messageHistory.Count > maxHistoryCount)
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
