using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Глобальный конфиг баланса для Utility AI системы.
    /// Содержит настройки метаболизма, весов действий и модификаторов.
    /// </summary>
    [CreateAssetMenu(fileName = "AIBalanceConfig", menuName = "Bureau/AI Balance Config")]
    public class AIBalanceConfig : ScriptableObject
    {
        #region Singleton
        private static AIBalanceConfig _instance;
        
        public static AIBalanceConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<AIBalanceConfig>("Databases/AIBalanceConfig");
                    
                    if (_instance == null)
                    {
                        Debug.LogWarning("[AIBalanceConfig] Файл не найден в Resources/Databases/. Создаю экземпляр со значениями по умолчанию.");
                        _instance = CreateInstance<AIBalanceConfig>();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Метаболизм (Дельты в секунду)
        [Header("⚡ Метаболизм (дельта в секунду)")]
        [Tooltip("Базовая потеря энергии за секунду")]
        [Range(0f, 2f)]
        public float baseEnergyLoss = 0.3f;

        [Tooltip("Базовый рост потребности в туалет за секунду")]
        [Range(0f, 3f)]
        public float baseBladderGain = 0.8f;

        [Tooltip("Базовая потеря морали за секунду")]
        [Range(0f, 2f)]
        public float baseMoraleLoss = 0.5f;

        [Tooltip("Базовый рост стресса за секунду")]
        [Range(0f, 2f)]
        public float baseStressGain = 0.2f;
        #endregion

        #region Веса Utility AI
        [Header("⚖️ Веса Utility AI")]
        [Tooltip("Бонус к сортировке бумаг (педантичность)")]
        [Range(0f, 30f)]
        public float pedantrySortBonus = 15f;

        [Tooltip("Множитель работы с клиентами (мастерство)")]
        [Range(1f, 2f)]
        public float masteryWorkMultiplier = 1.5f;

        [Tooltip("Бонус к помощи потерявшимся клиентам (soft skills)")]
        [Range(0f, 50f)]
        public float softSkillsHelpBonus = 25f;

        [Tooltip("Бонус к работе на кассе (коррупция)")]
        [Range(0f, 40f)]
        public float corruptionCashierBonus = 20f;

        [Tooltip("Вес желания уйти со смены от стресса")]
        [Range(0f, 60f)]
        public float stressHomeWeight = 30f;

        [Tooltip("Множитель стресса от наличия грязи рядом")]
        [Range(1f, 5f)]
        public float messStressMultiplier = 2.0f;
        #endregion

        #region Бюрократия
        [Header("🗄️ Бюрократия")]
        [Tooltip("Сколько секунд регистратор ждет ответа от архива")]
        public float archiveWaitTimeout = 60f;
        
        [Header("😤 Клиенты-Пролазы (Я только спросить!)")]
        [Tooltip("Шанс появления наглого клиента, игнорирующего аппарат талонов")]
        [Range(0f, 1f)] public float queueJumperChance = 0.1f;
        [Tooltip("Во сколько раз быстрее наглецы теряют терпение")]
        public float jumperStressMultiplier = 2.5f;
        #endregion

        #region Трейты (Особенности)
        [Header("🎭 Трейты (Особенности)")]
        public float traitCheckInterval = 10f;
        
        [Range(0f, 1f)] public float allergyChance = 0.4f;
        public float allergyPushForce = 200f;
        public float allergyRadius = 2.5f;

        [Range(0f, 1f)] public float loudmouthChance = 0.3f;
        
        public float sloppyDistance = 0.5f;
        [Range(0f, 1f)] public float sloppyChance = 0.2f;

        public float clumsyDistance = 3.0f;
        [Range(0f, 1f)] public float clumsyChance = 0.2f;

        public float sprinterDistance = 8.0f;
        public float gossipCooldown = 20f;
        #endregion

        #region Clinch System
        [Header("🤼 Clinch System (Прорыв)")]
        [Tooltip("Базовый шанс клиента инициировать клинч (прорыв)")]
        [Range(0f, 1f)]
        public float clientClinchBaseChance = 0.1f;
        
        [Tooltip("Базовый шанс персонала инициировать клинч (прорыв)")]
        [Range(0f, 1f)]
        public float staffClinchBaseChance = 0.1f;

        [Tooltip("Время в секундах на принятие решения в клинче")]
        [Range(1f, 20f)]
        public float clinchTimeoutDuration = 10f;

        [Tooltip("Штраф HP директора при провале клинча")]
        [Range(0f, 50f)]
        public float clinchPenaltyHP = 10f;

        [Tooltip("Награда HP директора при успешном клинче")]
        [Range(0f, 50f)]
        public float clinchRewardHP = 5f;

        [Tooltip("Штраф к стрессу персонажа при провале клинча")]
        [Range(0f, 100f)]
        public float clinchPenaltyStress = 20f;

        [Tooltip("Облегчение стресса персонажа при успешном клинче")]
        [Range(0f, 100f)]
        public float clinchRewardStress = 10f;
        #endregion

        #region Бухгалтер (Accountant)
        [Header("💼 Бухгалтер")]
        [Tooltip("Период между попытками найти деньги (сек)")]
        public float accountantMoneyGenInterval = 60f;

        [Tooltip("Базовый шанс найти деньги за один тик (0..1)")]
        [Range(0f, 1f)]
        public float accountantMoneyGenBaseChance = 0.4f;

        [Tooltip("Минимальная сумма находки (официально теневой)")]
        public int accountantMoneyGenMin = 10;

        [Tooltip("Максимальная сумма находки (официально теневой)")]
        public int accountantMoneyGenMax = 50;

        [Tooltip("Период между попытками покрыть схемы (сек)")]
        public float accountantCoverSchemesInterval = 120f;

        [Tooltip("Базовый шанс покрыть схемы (до модификации dirtyHands)")]
        [Range(0f, 1f)]
        public float accountantCoverSchemesBaseChance = 0.1f;

        [Tooltip("Множитель шанса покрытия от dirtyHands (0..1)")]
        [Range(0f, 1f)]
        public float accountantCoverSchemesDirtyHandsBonus = 0.8f;

        [Tooltip("Минимальное снижение globalCorruptionScore за успех")]
        public int accountantCoverSchemesReduceMin = 50;

        [Tooltip("Максимальное снижение globalCorruptionScore за успех")]
        public int accountantCoverSchemesReduceMax = 150;
        #endregion

        #region Debug
        [Header("🔧 Debug")]
        [Tooltip("Включить подробные логи")]
        public bool enableDebugLogs = false;

        public void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AIBalanceConfig] {message}");
            }
        }

        /// <summary>
        /// Рассчитывает бонус коррупции для работы на кассе.
        /// </summary>
        /// <param name="corruption">Уровень коррупции сотрудника (0-1)</param>
        /// <returns>Бонус к utility</returns>
        public float GetCorruptionCashierBonus(float corruption)
        {
            if (corruption <= 0f || corruptionCashierBonus <= 0f) return 0f;
            return corruption * corruptionCashierBonus;
        }
        #endregion

        #region Ending System (Система концовок)

        [Header("🎬 Ending System")]
        [Tooltip("День, на котором наступает финал игры (по умолчанию 30)")]
        public int finalDay = 30;

        [Tooltip("Лимит страйков, при превышении которого директор отстраняется (Dismissal)")]
        public int dismissalStrikeLimit = 5;

        [Tooltip("Максимально допустимое значение одной черты личности")]
        public int maxTraitScore = 100;

        [Tooltip("Минимальный шаг добавления очков черты за один выбор")]
        public int traitMinStep = -1;

        [Tooltip("Максимальный шаг добавления очков черты за один выбор")]
        public int traitMaxStep = 2;

        [Header("🎞️ Титры арок")]
        [Tooltip("Длительность появления титра арки (сек)")]
        public float arcTitleFadeIn = 1f;

        [Tooltip("Длительность показа титра арки (сек)")]
        public float arcTitleHold = 3f;

        [Tooltip("Длительность исчезновения титра арки (сек)")]
        public float arcTitleFadeOut = 2f;

        [Tooltip("Звук, проигрываемый при появлении титра арки. Если null — берётся из SoundLibrary по SoundID.Arc_Title")]
        public AudioClip arcTitleSoundClip;

        [Tooltip("Музыка для экрана отстранения (если null — используется endingMusic из EndingEntry или nightTrack)")]
        public AudioClip dismissalMusicOverride;

        [Header("🛠️ Debug Ending")]
        [Tooltip("Разрешить кнопку досрочного завершения карьеры в UI")]
        public bool enableManualEndButton = false;

        #endregion
    }
}
