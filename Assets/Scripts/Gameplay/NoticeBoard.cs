// Assets/Scripts/Gameplay/NoticeBoard.cs
using UnityEngine;
using Managers;
using Gameplay.Documents;
using System.Collections.Generic;
using UI; // Для ActivePoliciesPanelUI

namespace Gameplay
{
    public class NoticeBoard : MonoBehaviour
    {
        [Header("Настройки")]
        public Transform pinPoint; 
        public ParticleSystem pinEffect; 
        public Transform interactionPoint; 

        [Header("UI Связь")]
        [Tooltip("Панель, которая откроется при клике на доску")]
        public ActivePoliciesPanelUI uiPanel;

        // Визуальные объекты висящих указов (просто для красоты)
        private List<GameObject> visualDocs = new List<GameObject>();
        [Tooltip("Префаб висящей бумажки")]
        public GameObject visualPaperPrefab;

        private void Start()
        {
            if (interactionPoint == null) interactionPoint = transform;
            
            // Регистрируем себя в реестре (если еще не там)
            if (ScenePointsRegistry.Instance != null)
                ScenePointsRegistry.Instance.noticeBoard = this;
        }

        // Вызывается Офис-менеджером
        public void PinPolicy(ProjectDocumentObject doc)
        {
            if (doc == null || doc.documentData == null) return;

            string policyID = doc.documentData.targetUpgradeID;
            
            // 1. Активируем логику
            PolicyManager.Instance.ActivatePolicy(policyID);

            // 2. Эффекты
            if (pinEffect != null) pinEffect.Play();
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Stamp_Approve, transform.position);

            // 3. Создаем визуал на доске
            if (visualPaperPrefab != null)
            {
                GameObject paper = Instantiate(visualPaperPrefab, pinPoint);
                paper.transform.localPosition = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
                paper.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-10f, 10f));
                visualDocs.Add(paper);
            }

            // Уничтожаем папку, которую принес менеджер
            Destroy(doc.gameObject);
            
            // Нотификация
            var policy = PolicyManager.Instance.GetPolicyById(policyID);
            string name = policy != null ? policy.displayName : "Указ";
            TeletypeManager.Instance?.Log($"ВСТУПИЛ В СИЛУ: {name}");
        }

        // Взаимодействие игрока (Клик)
        void OnMouseDown()
        {
            // Если игрок кликнул на доску
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            // Отправляем директора к доске
            if (DirectorAvatarController.Instance != null)
            {
                DirectorAvatarController.Instance.GoToNoticeBoard(this);
            }
        }

        // Вызывается Директором, когда он подошел
        public void OpenUI()
        {
            if (uiPanel != null)
            {
                uiPanel.Show();
            }
        }
        
        // Метод для очистки визуала (вызывается из UI при срыве указа)
        public void RemoveVisualDoc()
        {
            if (visualDocs.Count > 0)
            {
                var last = visualDocs[visualDocs.Count - 1];
                visualDocs.RemoveAt(visualDocs.Count - 1);
                Destroy(last);
            }
        }
    }
}