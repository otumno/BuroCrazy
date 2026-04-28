using UnityEngine;

namespace BuroDebug
{
    /// <summary>
    /// Автоматически создаёт Debug UI при старте
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class DebugUIAutocreate : MonoBehaviour
    {
        private void Awake()
        {
            // Проверяем, есть ли уже Debug UI
            if (FindObjectOfType<ClientDebugUI>() != null)
            {
                return;
            }

            // Создаём GameObject с компонентами
            var debugGO = new GameObject("DebugUI_AutoCreated");
            debugGO.AddComponent<ClientDebugMenu>();
            debugGO.AddComponent<ClientDebugUI>();

            Debug.Log("[DebugUIAutocreate] Debug UI created! Press [F1] to toggle menu.");
        }

        /// <summary>
        /// Ручное создание Debug UI (вызовите из консоли: FindObjectOfType<DebugUIAutocreate>().ForceCreateUI())
        /// </summary>
        [ContextMenu("Create Debug UI Now")]
        public void ForceCreateUI()
        {
            if (FindObjectOfType<ClientDebugUI>() != null)
            {
                return;
            }

            var debugGO = new GameObject("DebugUI_Manual");
            debugGO.AddComponent<ClientDebugMenu>();
            debugGO.AddComponent<ClientDebugUI>();
        }
    }
}
