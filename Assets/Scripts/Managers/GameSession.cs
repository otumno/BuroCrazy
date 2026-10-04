// Assets/Scripts/Managers/GameSession.cs
using CinematicSystem;
using Data.Creation;
using StorySystem;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// Единая точка старта игровой сессии (каждой загрузки GameScene). Порядок жёсткий:
    /// 1. BindPersistentManagers — постоянные менеджеры [SYSTEMS] связываются со свежей сценой;
    /// 2. ResetGameState — состояние партии сбрасывается во всех менеджерах, и постоянных, и сценовых;
    /// 3. ApplySave — применяется сейв слота, для новой игры — ещё и настройки создания Директора;
    /// 4. подготовка дня.
    /// Менеджеры не должны сами инициализировать состояние партии в Start/sceneLoaded:
    /// [SYSTEMS] переживает смену сцен, и такая инициализация срабатывает только один раз за запуск игры.
    /// </summary>
    // Статический класс, а не компонент: ему не нужны ни кадры, ни сериализуемые поля,
    // а компонент пришлось бы класть в [SYSTEMS], который создаётся в главном меню.
    public static class GameSession
    {
        /// <summary>
        /// Старт сессии после загрузки GameScene (вызывает MainUIManager до заставки дня).
        /// </summary>
        public static void Begin()
        {
            var saveLoad = SaveLoadManager.Instance;
            bool isNewGame = saveLoad != null && saveLoad.pendingNewGameSetup;
            Debug.Log($"[GameSession] Старт сессии: {(isNewGame ? "новая игра" : "загрузка сейва")}, слот {saveLoad?.GetCurrentSlot()}");

            BindPersistentManagers();
            ResetGameState();
            ApplySave(isNewGame);
            DirectorManager.Instance?.PrepareDay();
        }

        /// <summary>
        /// Перезагрузка сейва текущего слота поверх уже идущей сцены (после отстранения).
        /// Сцена та же, поэтому привязка постоянных менеджеров не нужна.
        /// </summary>
        public static void ReloadCurrentSlot()
        {
            Debug.Log("[GameSession] Перезагрузка текущего слота поверх сцены");
            ResetGameState();
            ApplySave(isNewGame: false);
            DirectorManager.Instance?.PrepareDay();
        }

        private static void BindPersistentManagers()
        {
            ProgressionManager.Instance?.BindToScene();
            MusicPlayer.Instance?.BindToScene();
        }

        // Сбрасываем всё, даже то, что потом перезапишет сейв: в старых сейвах может не быть части полей,
        // а постоянные менеджеры иначе унесли бы состояние прошлой партии в новую.
        private static void ResetGameState()
        {
            CalendarManager.Instance?.StartNewGame();
            PlayerWallet.Instance?.ResetState();
            ArchiveManager.Instance?.ResetState();
            DirectorManager.Instance?.ResetState();
            HiringManager.Instance?.ResetState();
            ProgressionManager.Instance?.ResetState();
            OrderManager.Instance?.ResetState();
            StoryStateManager.Instance?.ResetState();
            CinematicTriggerManager.Instance?.ResetAllTriggers();
        }

        private static void ApplySave(bool isNewGame)
        {
            var saveLoad = SaveLoadManager.Instance;
            if (saveLoad == null)
            {
                Debug.LogError("[GameSession] SaveLoadManager не найден — сессия начнётся со сброшенного состояния.");
                return;
            }

            // Для новой игры здесь лежит стартовый сейв, записанный при её создании (деньги, день и т.п.).
            if (!saveLoad.LoadGame(saveLoad.GetCurrentSlot()))
            {
                Debug.LogWarning("[GameSession] Сейв не найден — сессия начнётся со сброшенного состояния.");
            }

            if (!isNewGame) return;

            ApplyDirectorCreationSettings();
            ArcManager.Instance?.Initialize();

            // Флаг потребляется: настройка новой игры нужна один раз.
            saveLoad.pendingNewGameSetup = false;
        }

        private static void ApplyDirectorCreationSettings()
        {
            var initialState = MainUIManager.GetPendingDirectorInitialState();
            if (initialState == null)
            {
                Debug.Log("[GameSession] Нет данных о создании директора, используем настройки по умолчанию.");
                return;
            }

            Debug.Log($"[GameSession] Применяем настройки создания директора: {MainUIManager.GetPendingDirectorCreationCode()}");

            PlayerWallet.Instance?.ResetState(initialState.startingMoney);

            if (ProgressionManager.Instance != null)
            {
                // SetInfluence, а не AddInfluence: стартовый сейв уже применил то же влияние, прибавка удвоила бы его.
                ProgressionManager.Instance.SetInfluence(initialState.startingInfluence);

                foreach (var regionID in initialState.unlockedRegions)
                {
                    ProgressionManager.Instance.UnlockRegion(regionID);
                }
            }

            DirectorManager.Instance?.SetStrikes(initialState.startingStrikes);

            if (initialState.startingStaff.Count > 0 && HiringManager.Instance != null)
            {
                foreach (var staffData in initialState.startingStaff)
                {
                    HiringManager.Instance.SpawnStaff(staffData.role, staffData.customName, staffData.skillLevel);
                }
            }

            // Применяем внешность всегда: пол задаётся книгой (SetGender), а spriteCollectionID
            // ни один выбор пока не устанавливает — поэтому по нему гейтить вызов нельзя.
            ApplyDirectorAppearance(initialState);

            MainUIManager.ClearPendingDirectorData();
        }

        private static void ApplyDirectorAppearance(DirectorInitialState initialState)
        {
            var director = DirectorAvatarController.Instance;
            if (director == null) return;

            EmotionSpriteCollection collection = null;
            if (!string.IsNullOrEmpty(initialState.spriteCollectionID))
            {
                collection = Resources.Load<EmotionSpriteCollection>($"DirectorSprites/{initialState.spriteCollectionID}");
                if (collection == null)
                {
                    Debug.LogWarning($"[GameSession] Коллекция спрайтов '{initialState.spriteCollectionID}' не найдена в Resources/DirectorSprites.");
                }
            }

            director.ApplyAppearance(initialState.startingGender, collection);
        }
    }
}
