using System;
using UnityEngine;
using Zenject;
using Managers;
using Managers.Teletype;
using Managers.Academy;
using Managers.Bureaucracy;
using UI.Notifications;

namespace DI
{
    /// <summary>
    /// Глобальный инсталлер для Zenject.
    /// Привязывает все существующие синглтоны к DI-контейнеру через FromMethod,
    /// чтобы избежать Race Condition - Zenject не будет искать объекты на сцене раньше времени.
    /// </summary>
    public class ProjectContextInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            // ==================== Менеджеры ====================
            BindSingleton<AudioManager>(() => AudioManager.Instance);
            BindSingleton<ClientQueueManager>(() => FindObjectOfType<ClientQueueManager>());
            BindSingleton<SaveLoadManager>(() => SaveLoadManager.Instance);
            BindSingleton<CalendarManager>(() => CalendarManager.Instance);
            BindSingleton<TimeManager>(() => TimeManager.Instance);
            BindSingleton<WaveManager>(() => WaveManager.Instance);
            BindSingleton<HiringManager>(() => HiringManager.Instance);
            BindSingleton<ArchiveManager>(() => ArchiveManager.Instance);
            BindSingleton<PlayerWallet>(() => PlayerWallet.Instance);
            BindSingleton<DirectorManager>(() => DirectorManager.Instance);
            BindSingleton<ProgressionManager>(() => ProgressionManager.Instance);
            BindSingleton<UpgradeManager>(() => UpgradeManager.Instance);
            BindSingleton<PolicyManager>(() => PolicyManager.Instance);
            BindSingleton<DocumentManager>(() => DocumentManager.Instance);
            BindSingleton<EquipmentManager>(() => EquipmentManager.Instance);
            BindSingleton<DurabilityManager>(() => DurabilityManager.Instance);
            BindSingleton<ExperienceManager>(() => ExperienceManager.Instance);
            BindSingleton<FinancialLedgerManager>(() => FinancialLedgerManager.Instance);
            BindSingleton<OrderManager>(() => OrderManager.Instance);
            BindSingleton<PhoneManager>(() => PhoneManager.Instance);
            BindSingleton<StoryStateManager>(() => StoryStateManager.Instance);
            BindSingleton<TransitionManager>(() => TransitionManager.Instance);
            BindSingleton<MainUIManager>(() => MainUIManager.Instance);
            BindSingleton<MusicPlayer>(() => MusicPlayer.Instance);
            BindSingleton<LightingManager>(() => LightingManager.Instance);
            BindSingleton<AchievementManager>(() => AchievementManager.Instance);
            BindSingleton<TeletypeManager>(() => TeletypeManager.Instance);
            BindSingleton<DialogueUIManager>(() => DialogueUIManager.Instance);
            BindSingleton<AssignmentManager>(() => AssignmentManager.Instance);
            BindSingleton<GuardManager>(() => GuardManager.Instance);
            BindSingleton<ArchiveRequestManager>(() => ArchiveRequestManager.Instance);
            BindSingleton<ScenePointsRegistry>(() => ScenePointsRegistry.Instance);
            BindSingleton<PayStaffManager>(() => PayStaffManager.Instance);
            BindSingleton<UIGlobalSettingsManager>(() => UIGlobalSettingsManager.Instance);
            BindSingleton<ClientSpawner>(() => ClientSpawner.Instance);
            BindSingleton<BureaucracyManager>(() => BureaucracyManager.Instance);
            BindSingleton<AcademyScenarioManager>(() => AcademyScenarioManager.Instance);
            BindSingleton<TutorialBureaucracyQuest>(() => TutorialBureaucracyQuest.Instance);
            BindSingleton<DocumentQualityManager>(() => DocumentQualityManager.Instance);
            BindSingleton<GameLifecycleManager>(() => GameLifecycleManager.Instance);
            BindSingleton<NotificationManager>(() => NotificationManager.Instance);

            Debug.Log("[ProjectContextInstaller] Все синглтоны привязаны к DI-контейнеру.");
        }

        /// <summary>
        /// Ленивая привязка к существующему синглтону через delegate.
        /// Это позволяет избежать проблем с порядком инициализации (Race Condition).
        /// </summary>
        private void BindSingleton<T>(Func<T> getter) where T : MonoBehaviour
        {
            Container.Bind<T>().FromMethod(_ => getter()).AsSingle();
        }
    }
}
