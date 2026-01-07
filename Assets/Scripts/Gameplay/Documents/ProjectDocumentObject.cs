// Assets/Scripts/Gameplay/Documents/ProjectDocumentObject.cs
using UnityEngine;
using TMPro;            // Для работы с текстом
using Data.Documents;   // Наши данные (ProjectDocumentDefinition)
using Managers;         // Чтобы найти StartOfDayPanel (если она в этом namespace)

namespace Gameplay.Documents
{
    /// <summary>
    /// Компонент физической "Красной Папки" на столе.
    /// Отвечает за отображение названия, печатей и регистрацию себя в UI директора.
    /// </summary>
    public class ProjectDocumentObject : MonoBehaviour
    {
        [Header("Данные документа (Read Only)")]
        // Сюда сохраняются данные, переданные при спавне (из DocumentStack)
        public ProjectDocumentDefinition documentData;

        [Header("Визуальные ссылки (Назначить в префабе)")]
        [Tooltip("Текст на обложке папки (World Space TextMeshPro)")]
        [SerializeField] private TextMeshPro documentTitleText; 
        
        [Tooltip("Спрайт синей печати/подписи (должен быть выключен по умолчанию)")]
        [SerializeField] private GameObject signatureSeal; 
        
        [Tooltip("Спрайт финального штампа 'Оплачено/Готово' (должен быть выключен по умолчанию)")]
        [SerializeField] private GameObject approvedStamp; 

        /// <summary>
        /// Главный метод инициализации. Вызывается из DocumentStack при создании.
        /// </summary>
        /// <param name="data">Данные приказа (регион, апгрейд и т.д.)</param>
        public void Initialize(ProjectDocumentDefinition data)
        {
            if (data == null)
            {
                Debug.LogError($"[ProjectDocumentObject] Попытка инициализации {name} с пустыми данными (null)!");
                return;
            }

            documentData = data;

            // 1. Обновляем внешний вид (текст)
            UpdateVisuals();

            // 2. Регистрируемся в UI кабинета, чтобы игрок мог подписать этот документ через меню
            // (Проверяем наличие панели, чтобы не было ошибок при тестах)
            if (StartOfDayPanel.Instance != null)
            {
                // Мы передаем данные документа и метод обратного вызова (OnSignedByPlayer),
                // который сработает, когда игрок нажмет кнопку "Подписать" в UI.
                StartOfDayPanel.Instance.RegisterProjectDocument(documentData, OnSignedByPlayer);
                Debug.Log($"[ProjectDocumentObject] Документ '{documentData.documentName}' зарегистрирован в UI.");
            }
            else
            {
                Debug.LogWarning("[ProjectDocumentObject] StartOfDayPanel.Instance не найден! Документ нельзя будет подписать через UI.");
            }
        }

        /// <summary>
        /// Метод обратного вызова. Срабатывает, когда игрок нажимает "Подписать" в UI панели.
        /// </summary>
        private void OnSignedByPlayer()
        {
            Debug.Log($"[ProjectDocumentObject] Игрок подписал документ '{documentData.documentName}'. Обновляем визуал.");
            
            // Данные уже обновлены внутри UI (signedByDirector = true),
            // нам нужно только обновить визуал физического объекта.
            UpdateVisuals();

            // Здесь можно добавить звук "Чирк пером" или партикл подписи
            // if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundID.UI_Stamp_Approve);
        }

        /// <summary>
        /// Обновляет текстовые поля и видимость печатей на основе данных.
        /// </summary>
        public void UpdateVisuals()
        {
            if (documentData == null) return;

            // 1. Устанавливаем название
            if (documentTitleText != null)
            {
                documentTitleText.text = documentData.documentName;
            }

            // 2. Печать Директора (Появляется, если документ подписан)
            if (signatureSeal != null)
            {
                bool isSigned = documentData.signedByDirector;
                // Включаем/выключаем объект печати
                if (signatureSeal.activeSelf != isSigned)
                {
                    signatureSeal.SetActive(isSigned);
                }
            }
            
            // 3. Финальный штамп (Появляется, если документ прошел все инстанции)
            if (approvedStamp != null)
            {
                // Логика: документ считается полностью готовым визуально, если он прошел регистрацию и оплату
                // (или архивацию, в зависимости от того, когда вы хотите показывать штамп)
                bool isReady = documentData.processedByRegistrar && documentData.paidAtCashier;
                
                if (approvedStamp.activeSelf != isReady)
                {
                    approvedStamp.SetActive(isReady);
                }
            }
        }
        
        // Автоматически удаляем иконку из UI, если физическая папка уничтожена (например, сгорела или удалена багом)
        private void OnDestroy()
        {
            if (StartOfDayPanel.Instance != null && documentData != null)
            {
                StartOfDayPanel.Instance.RemoveProjectDocumentIcon(documentData);
            }
        }
    }
}