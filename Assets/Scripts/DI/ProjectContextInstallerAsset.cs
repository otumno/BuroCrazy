using System;
using UnityEngine;
using UI.Tooltips;
using Zenject;
using Managers;
using Managers.Teletype;
using Managers.Academy;
using Managers.Bureaucracy;
using UI.Notifications;
using UI;

namespace DI
{
    /// <summary>
    /// ScriptableObject-инсталлер для Zenject.
    /// Привязывает все существующие синглтоны к DI-контейнеру через FromMethod.
    /// Создаётся через Assets > Create > BuroCrazy > DI > Project Context Installer
    /// </summary>
    [CreateAssetMenu(menuName = "BuroCrazy/DI/Project Context Installer")]
    public class ProjectContextInstallerAsset : ScriptableObjectInstaller
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

            // Tooltip system
            Container.Bind<TooltipManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();

            Debug.Log("[ProjectContextInstallerAsset] Все синглтоны привязаны к DI-контейнеру.");
        }

        private void BindSingleton<T>(System.Func<T> getter) where T : MonoBehaviour
        {
            Container.Bind<T>().FromMethod(_ => getter()).AsSingle();
        }
    }
}
