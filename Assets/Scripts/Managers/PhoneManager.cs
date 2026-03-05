using UnityEngine;
using System.Collections.Generic;
using DialogueSystem.Data;
using System.Linq;

namespace Managers
{
    public class PhoneManager : MonoBehaviour
    {
        public static PhoneManager Instance { get; private set; }

        [Header("Настройки")]
        public AudioSource phoneAudioSource;
        public AudioClip ringSound;
        
        [Header("UI Ссылки")]
        [Tooltip("Иконка в HUD, которая мигает/появляется при звонке")]
        public GameObject hudNotificationIcon; 
        [Tooltip("Сама панель телефона с кнопками")]
        public PhonePanelUI phonePanelUI;

        [Header("Исходящие контакты (Настройка в инспекторе)")]
        public List<PhoneContact> outgoingContacts = new List<PhoneContact>();

        // Список активных входящих звонков (диалоги)
        private List<DialogueGraph> incomingCalls = new List<DialogueGraph>();

        public bool HasActiveCalls => incomingCalls.Count > 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (hudNotificationIcon != null) hudNotificationIcon.SetActive(false);
        }

        // --- ВХОДЯЩИЕ ---

        public void RegisterIncomingCall(DialogueGraph dialogue)
        {
            incomingCalls.Add(dialogue);
            Debug.Log($"[PhoneManager] Входящий вызов! Всего на линии: {incomingCalls.Count}");

            Managers.Teletype.TeletypeManager.Instance?.LogImportant("Входящий звонок на линии!");

            // Визуальные/Аудио эффекты
            if (phoneAudioSource != null && ringSound != null)
            {
                phoneAudioSource.PlayOneShot(ringSound);
            }
            UpdateNotificationState();
        }

        public void AnswerCall(DialogueGraph dialogue)
        {
            if (incomingCalls.Contains(dialogue))
            {
                incomingCalls.Remove(dialogue);
                UpdateNotificationState();

                // Запуск диалога
                DialogueUIManager.Instance.StartDialogue(dialogue, null, null);
                
                // Закрываем панель телефона после ответа
                phonePanelUI.Hide();
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
		
		public void RemoveCall(DialogueGraph dialogue)
        {
            if (incomingCalls.Contains(dialogue))
            {
                incomingCalls.Remove(dialogue);
                UpdateNotificationState();
            }
        }

        public void CallContact(PhoneContact contact)
        {
            // Логика вызова службы
            Debug.Log($"[PhoneManager] Звонок исходящему: {contact.displayName}");
            
            // Пример логики вызова (можно расширить)
            var staff = HiringManager.Instance.AllStaff.FirstOrDefault(s => s.currentRole == contact.associatedRole && s.IsOnDuty());
            if (staff != null)
            {
                DirectorAvatarController.Instance.thoughtBubble.ShowPriorityMessage($"Вызываю {staff.characterName}...", 2f, Color.white);
            }
            else
            {
                DirectorAvatarController.Instance.thoughtBubble.ShowPriorityMessage("Абонент недоступен...", 2f, Color.red);
            }
            
            phonePanelUI.Hide();
        }

        // --- ОБЩЕЕ ---

        public List<DialogueGraph> GetActiveCalls() => incomingCalls;

        private void UpdateNotificationState()
        {
            if (hudNotificationIcon != null)
            {
                hudNotificationIcon.SetActive(HasActiveCalls);
            }
            
            // Если панель открыта, обновляем её содержимое в реальном времени
            if (phonePanelUI.gameObject.activeInHierarchy)
            {
                phonePanelUI.Refresh();
            }
        }
        
        // Метод для очистки звонков (например, ночью)
        public void ClearAllCalls()
        {
            incomingCalls.Clear();
            UpdateNotificationState();
        }
    }
}