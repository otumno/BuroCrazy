using UnityEngine;
using Zenject;
using Managers;
using Managers.Teletype;

namespace DI
{
    /// <summary>
    /// Валидатор DI-биндингов. Добавить на GameObject с SceneContext.
    /// Выводит в консоль статус всех привязок при старте.
    /// </summary>
    [RequireComponent(typeof(Zenject.SceneContext))]
    public class DIBindingValidator : MonoBehaviour
    {
        [Header("Настройки валидации")]
        [Tooltip("Включить логирование всех проверенных биндингов")]
        public bool logAllBindings = true;

        [Tooltip("Подсветить не найденные биндинги красным в консоли")]
        public bool highlightMissing = true;

        private void Start()
        {
            ValidateBindings();
        }

        private void ValidateBindings()
        {
            var sceneContext = GetComponent<Zenject.SceneContext>();
            if (sceneContext == null)
            {
                Debug.LogWarning("[DIBindingValidator] SceneContext не найден на этом объекте!");
                return;
            }
            
            var container = sceneContext.Container;
            int successCount = 0;
            int failCount = 0;

            // Список всех типов для проверки
            var typesToCheck = new System.Type[]
            {
                typeof(AudioManager),
                typeof(ClientQueueManager),
                typeof(SaveLoadManager),
                typeof(CalendarManager),
                typeof(TimeManager),
                typeof(WaveManager),
                typeof(HiringManager),
                typeof(ArchiveManager),
                typeof(PlayerWallet),
                typeof(DirectorManager),
                typeof(ProgressionManager),
                typeof(UpgradeManager),
                typeof(PolicyManager),
                typeof(DocumentManager),
                typeof(EquipmentManager),
                typeof(DurabilityManager),
                typeof(ExperienceManager),
                typeof(FinancialLedgerManager),
                typeof(OrderManager),
                typeof(PhoneManager),
                typeof(StoryStateManager),
                typeof(TransitionManager),
                typeof(MainUIManager),
                typeof(MusicPlayer),
                typeof(LightingManager),
                typeof(AchievementManager),
                typeof(TeletypeManager),
                typeof(DialogueUIManager),
                typeof(AssignmentManager),
                typeof(GuardManager),
                typeof(ArchiveRequestManager),
                typeof(ScenePointsRegistry),
                typeof(PayStaffManager),
                typeof(UIGlobalSettingsManager),
                typeof(ClientSpawner),
            };

            // Debug.Log($"<color=yellow>[DIBindingValidator] Начинаю проверку {typesToCheck.Length} биндингов...</color>");

            foreach (var type in typesToCheck)
            {
                bool hasBinding = container.HasBinding(type);
                bool instanceExists = CheckInstanceExists(type);

                if (hasBinding && instanceExists)
                {
                    successCount++;
                    // if (logAllBindings) Debug.Log($"<color=green>[OK]</color> {type.Name}");
                }
                else if (!hasBinding && !instanceExists)
                {
                    failCount++;
                    if (highlightMissing)
                        Debug.LogError($"<color=red>[MISSING]</color> {type.Name} - нет биндинга и нет Instance");
                    // else Debug.Log($"<color=red>[MISSING]</color> {type.Name}");
                }
                else if (hasBinding && !instanceExists)
                {
                    failCount++;
                    if (highlightMissing)
                        Debug.LogError($"<color=red>[BROKEN]</color> {type.Name} - есть биндинг, но Instance == null!");
                    // else Debug.Log($"<color=red>[BROKEN]</color> {type.Name}");
                }
                else // !hasBinding && instanceExists
                {
                    // Instance есть, но биндинга нет - это нормально для обратной совместимости
                    // if (logAllBindings) Debug.Log($"<color=cyan>[NO BINDING but Instance exists]</color> {type.Name}");
                }
            }

            if (failCount == 0)
            {
                // Debug.Log($"<color=green>[DIBindingValidator] Проверка завершена: {successCount} OK, {failCount} ошибок. Все критичные биндинги активны.</color>");
            }
            else
            {
                // Debug.LogError($"<color=red>[DIBindingValidator] Проверка завершена: {successCount} OK, {failCount} ошибок!</color>");
            }
        }

        private bool CheckInstanceExists(System.Type type)
        {
            // Используем reflection для проверки Instance
            var property = type.GetProperty("Instance",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.FlattenHierarchy);

            if (property != null)
            {
                var value = property.GetValue(null);
                return value != null;
            }

            return false;
        }
    }
}
