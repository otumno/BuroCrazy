// Assets/Scripts/Gameplay/NoticeBoard.cs
using UnityEngine;
using Managers;
using Gameplay.Documents;

namespace Gameplay
{
    public class NoticeBoard : MonoBehaviour
    {
        [Header("Настройки")]
        public Transform pinPoint; // Куда лепить указ
        public ParticleSystem pinEffect; // Пыль/конфетти при прикалывании

        // Точка, куда встанет Офис-менеджер, чтобы повесить указ
        public Transform interactionPoint; 

        private void Start()
        {
            if (interactionPoint == null) interactionPoint = transform;
        }

        public void PinPolicy(ProjectDocumentObject doc)
        {
            if (doc == null || doc.documentData == null) return;

            // 1. Активируем политику в менеджере
            // ID политики берем из ID документа (targetUpgradeID используем как хранилище ID политики)
            string policyID = doc.documentData.targetUpgradeID; 
            PolicyManager.Instance.ActivatePolicy(policyID);

            // 2. Визуальный эффект
            if (pinEffect != null) pinEffect.Play();
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Stamp_Approve, transform.position); // Звук удара кнопкой

            // 3. Уничтожаем папку (она превратилась в закон)
            Destroy(doc.gameObject);
        }
    }
}