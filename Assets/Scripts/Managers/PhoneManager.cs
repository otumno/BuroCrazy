using UnityEngine;
using System.Collections.Generic;
using DialogueSystem.Data;
using System.Linq;
using Managers.Teletype;

namespace Managers
{
    public class PhoneManager : MonoBehaviour
    {
        public static PhoneManager Instance { get; private set; }

        [Header("Настройки")]
        public AudioSource phoneAudioSource;
        public AudioClip ringSound;
        
        [Header("UI Ссылки")]
        public GameObject hudNotificationIcon; 
        public PhonePanelUI phonePanelUI;

        [Header("Исходящие контакты")]
        public List<PhoneContact> outgoingContacts = new List<PhoneContact>();

        private List<DialogueGraph> incomingCalls = new List<DialogueGraph>();
        private bool _isInitialized = false;

        public bool HasActiveCalls => incomingCalls.Count > 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            if (hudNotificationIcon != null) hudNotificationIcon.SetActive(false);
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged += OnDayChanged;
                // Сбрасываем лимиты при старте игры (на случай, если день не менялся)
                ResetDailyLimits();
            }
            else
            {
                Debug.LogWarning("[PhoneManager] TimeManager.Instance не найден! Лимиты звонков не будут сбрасываться.");
            }
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
        }

        private void OnDayChanged(int newDay)
        {
            ResetDailyLimits();
        }

        public void ResetDailyLimits()
        {
            foreach (var c in outgoingContacts)
                c.remainingUsesToday = c.maxDailyUses;
            Debug.Log("[PhoneManager] Лимиты звонков сброшены на новый день.");
        }

        // --- ВХОДЯЩИЕ (без изменений) ---
        public void RegisterIncomingCall(DialogueGraph dialogue)
        {
            incomingCalls.Add(dialogue);
            TeletypeManager.Instance?.LogImportant("Входящий звонок на линии!");
            if (phoneAudioSource != null && ringSound != null)
                phoneAudioSource.PlayOneShot(ringSound);
            UpdateNotificationState();
        }

        public void AnswerCall(DialogueGraph dialogue)
        {
            if (incomingCalls.Contains(dialogue))
            {
                incomingCalls.Remove(dialogue);
                UpdateNotificationState();
                DialogueUIManager.Instance.StartDialogue(dialogue, null, null);
                phonePanelUI.Hide();
            }
        }

        public void RemoveCall(DialogueGraph dialogue)
        {
            if (incomingCalls.Contains(dialogue))
            {
                incomingCalls.Remove(dialogue);
                UpdateNotificationState();
            }
        }

        // --- ИСХОДЯЩИЕ ---
        public void UnlockContact(string contactID)
        {
            var contact = outgoingContacts.FirstOrDefault(c => c.id == contactID);
            if (contact != null)
            {
                contact.isUnlocked = true;
                Debug.Log($"[PhoneManager] Контакт {contact.displayName} разблокирован.");
            }
        }

        public void CallContact(PhoneContact contact)
        {
            if (contact == null) return;

            // Проверка лимитов
            if (!contact.CanCallToday() && contact.limitDialogue != null)
            {
                DialogueUIManager.Instance.StartDialogue(contact.limitDialogue, null);
                phonePanelUI.Hide();
                return;
            }

            // Для новых типов сервисов
            if (contact.serviceType != ServiceType.CallStaff)
            {
                if (contact.mainDialogue != null)
                {
                    DialogueUIManager.Instance.StartDialogue(contact.mainDialogue, null, () =>
                    {
                        // После завершения диалога (если услуга оказана) уменьшаем лимит
                        contact.remainingUsesToday--;
                        if (phonePanelUI.gameObject.activeInHierarchy)
                            phonePanelUI.Refresh();
                    });
                }
                else
                {
                    Debug.LogWarning($"[PhoneManager] У контакта {contact.id} нет основного диалога!");
                }
            }
            else // Старый тип – вызов сотрудника
            {
                var staff = HiringManager.Instance.AllStaff.FirstOrDefault(s => s.currentRole == contact.associatedRole && s.IsOnDuty());
                if (staff != null)
                    DirectorAvatarController.Instance.thoughtBubble.ShowPriorityMessage($"Вызываю {staff.characterName}...", 2f, Color.white);
                else
                    DirectorAvatarController.Instance.thoughtBubble.ShowPriorityMessage("Абонент недоступен...", 2f, Color.red);
            }
            
            phonePanelUI.Hide();
        }

        public List<DialogueGraph> GetActiveCalls() => incomingCalls;

        private void UpdateNotificationState()
        {
            if (hudNotificationIcon != null)
                hudNotificationIcon.SetActive(HasActiveCalls);
            if (phonePanelUI.gameObject.activeInHierarchy)
                phonePanelUI.Refresh();
        }

        public void ClearAllCalls()
        {
            incomingCalls.Clear();
            UpdateNotificationState();
        }

        // --- СОХРАНЕНИЕ ---
        public List<string> GetUnlockedContactIDs()
        {
            return outgoingContacts.Where(c => c.isUnlocked).Select(c => c.id).ToList();
        }

        public void LoadUnlockedContacts(List<string> ids)
        {
            if (ids == null) return;
            foreach (var id in ids)
                UnlockContact(id);
        }
    }
}