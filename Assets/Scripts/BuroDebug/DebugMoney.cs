#if DEBUG_ENABLED
using Managers;

namespace BuroDebug
{
    /// <summary>
    /// Тестовые деньги для отладки: при старте новой игры и в начале каждого нового дня.
    /// Сумма задаётся в ассете Resources/Debug Settings, без DEBUG_ENABLED класса нет вовсе.
    /// </summary>
    public static class DebugMoney
    {
        // TimeManager живёт в сцене и очищает события в OnDestroy, поэтому подписываемся заново на каждый новый экземпляр.
        // Запоминаем его, чтобы при перезагрузке слота в той же сцене не подписаться дважды.
        private static TimeManager s_boundTimeManager;

        /// <summary>
        /// Вызывается из GameSession после загрузки сейва: раньше кошелёк ещё сбрасывается к стартовой сумме.
        /// </summary>
        public static void StartSession(bool isNewGame)
        {
            // При загрузке сейва в кошельке должно быть ровно то, что записано в сейве,
            // поэтому стартовая выдача — только для новой игры. Ежедневная работает всегда.
            if (isNewGame) Grant("старт новой игры");
            BindToDayChanges();
        }

        private static void BindToDayChanges()
        {
            var timeManager = TimeManager.Instance;
            if (timeManager == null || timeManager == s_boundTimeManager) return;

            timeManager.OnDayChanged += TimeManager_DayChanged;
            s_boundTimeManager = timeManager;
        }

        private static void TimeManager_DayChanged(int day) => Grant($"день {day}");

        private static void Grant(string reason)
        {
            var settings = DebugSettings.instance;
            int amount = settings != null ? settings.debugMoneyPerDay : 0;
            if (amount <= 0 || PlayerWallet.Instance == null) return;

            PlayerWallet.Instance.AddDebugMoney(amount, $"тестовые деньги, {reason}");
        }
    }
}
#endif
