// Assets/Scripts/DI/UpgradeHooksInstaller.cs
// Регистрирует хуки активации для ключевых апгрейдов.
// Вызывается из ProjectContextInstaller или вешается на GameObject [SYSTEMS] в сцене.
//
// Использование:
//   1. Хук вызывается ПОСЛЕ стандартных эффектов апгрейда (ApplyUpgradeEffects).
//   2. Если апгрейд не зарегистрирован через RegisterUpgradeHook — действий нет.

using UnityEngine;
using Managers;

namespace DI
{
    public class UpgradeHooksInstaller : MonoBehaviour
    {
        [Header("Зарегистрированные хуки (только логирование)")]
        [Tooltip("Зарегистрировать хук для диванов (Upgrade_Sofas).")]
        [SerializeField] private bool registerSofasHook = true;

        private void Start()
        {
            if (UpgradeManager.Instance == null)
            {
                Debug.LogWarning("[UpgradeHooksInstaller] UpgradeManager.Instance не найден.");
                return;
            }

            if (registerSofasHook)
            {
                UpgradeManager.Instance.RegisterUpgradeHook("Upgrade_Sofas", OnSofasActivated);
            }

            // TODO: добавить регистрацию других хуков по мере появления апгрейдов
            // Пример:
            // UpgradeManager.Instance.RegisterUpgradeHook("Upgrade_DocumentDesk", OnDocumentDeskActivated);
        }

        /// <summary>
        /// Хук активации диванов — TODO: реальный эффект (повышение комфорта клиентов, etc).
        /// </summary>
        private void OnSofasActivated()
        {
            Debug.Log("[UpgradeHook] Диваны активированы — повышен комфорт ожидания клиентов.");
            // TODO: реальный эффект — например, увеличить клиентскую удовлетворённость,
            // или разблокировать сценарий "VIP-клиенты на диванах".
        }
    }
}
