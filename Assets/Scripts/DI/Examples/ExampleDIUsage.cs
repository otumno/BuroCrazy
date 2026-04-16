using UnityEngine;
using Zenject;
using Managers;

namespace DI.Examples
{
    /// <summary>
    /// Пример использования DI-инжекции в новом коде.
    /// Показывает как использовать [Inject] вместо Singleton.Instance
    /// 
    /// ВАЖНО: Этот компонент можно добавить на любой GameObject на сцене.
    /// Zenject автоматически заполнит поля _audioManager и _queueManager
    /// через DI-контейнер (ProjectContext + SceneContext).
    /// </summary>
    public class ExampleDIUsage : MonoBehaviour
    {
        // Вместо AudioManager.Instance теперь можно использовать это поле
        // Zenject найдёт AudioManager через ProjectContextInstaller
        [Inject]
        private AudioManager _audioManager;

        [Inject]
        private ClientQueueManager _queueManager;

        [Inject]
        private MainUIManager _uiManager;

        [Inject]
        private TimeManager _timeManager;

        private void Start()
        {
            // ================================================
            // СТАРЫЙ СПОСОБ (ВСЁ ЕЩЁ РАБОТАЕТ):
            // ================================================
            // AudioManager.Instance.PlaySound(...);
            // var queue = ClientQueueManager.Instance.queue;
            // MainUIManager.Instance.ShowWindow();
            // ================================================
            
            // ================================================
            // НОВЫЙ СПОСОБ (чище, тестируемее):
            // ================================================
            if (_audioManager != null)
            {
                Debug.Log("[ExampleDIUsage] AudioManager успешно внедрён через DI!");
                // _audioManager.PlaySound(Scriptables.Audio.SoundID.UI_Click_Default);
            }
            else
            {
                Debug.LogWarning("[ExampleDIUsage] AudioManager НЕ внедрён - проверь контейнер!");
            }

            if (_queueManager != null)
            {
                Debug.Log($"[ExampleDIUsage] ClientQueueManager внедрён. Клиентов в очереди: {_queueManager.standingClients.Count}");
            }

            if (_uiManager != null)
            {
                Debug.Log("[ExampleDIUsage] MainUIManager внедрён!");
            }

            if (_timeManager != null)
            {
                Debug.Log("[ExampleDIUsage] TimeManager внедрён!");
            }
        }
    }
}
