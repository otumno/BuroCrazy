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
            Debug.Log("[DebugUIAutocreate] === BUROCRAZY DEBUG MENU ===");
            Debug.Log("[DebugUIAutocreate] Awake called");

            // Проверяем, есть ли уже Debug UI
            if (FindObjectOfType<ClientDebugUI>() != null)
            {
                Debug.Log("[DebugUIAutocreate] Debug UI already exists");
                return;
            }

            Debug.Log("[DebugUIAutocreate] Creating Debug UI...");

            // Создаём GameObject с компонентами
            var debugGO = new GameObject("DebugUI_AutoCreated");
            var menu = debugGO.AddComponent<ClientDebugMenu>();
            var ui = debugGO.AddComponent<ClientDebugUI>();

            Debug.Log("[DebugUIAutocreate] Components added. Menu: " + (menu != null) + ", UI: " + (ui != null));

            Debug.Log("[DebugUIAutocreate] ===========================================");
            Debug.Log("[DebugUIAutocreate] Debug UI created! Press [F1] to toggle menu.");
            Debug.Log("[DebugUIAutocreate] ===========================================");
        }

        /// <summary>
        /// Ручное создание Debug UI (вызовите из консоли: FindObjectOfType<DebugUIAutocreate>().ForceCreateUI())
        /// </summary>
        [ContextMenu("Create Debug UI Now")]
        public void ForceCreateUI()
        {
            if (FindObjectOfType<ClientDebugUI>() != null)
            {
                Debug.Log("Debug UI already exists");
                return;
            }

            var debugGO = new GameObject("DebugUI_Manual");
            var menu = debugGO.AddComponent<ClientDebugMenu>();
            debugGO.AddComponent<ClientDebugUI>();

            Debug.Log("Debug UI created manually!");
        }
    }
}
