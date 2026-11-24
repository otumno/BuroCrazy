using UnityEngine;

namespace BuroDebug
{
    [CreateAssetMenu(fileName = "Debug Settings", menuName = "Scriptable Objects/Debug Settings")]
    public class DebugSettings : ScriptableObject
    {
        // serialize fields
        [SerializeField]
        private bool _enableBookKeeping;

        [SerializeField]
        [Tooltip("Тестовые деньги при старте сессии и в начале каждого дня (только с DEBUG_ENABLED). 0 — отключить.")]
        private int _debugMoneyPerDay = 10000;

        // getters which work with define
        public bool enableBookKeeping => GetValueOrDefault(_enableBookKeeping);
        public int debugMoneyPerDay => GetValueOrDefault(_debugMoneyPerDay);
        
        // singleton
        public static DebugSettings instance
        {
            get
            {
                if (!_instance)
                    _instance = Resources.Load<DebugSettings>("Debug Settings");

                return _instance;
            }
        }
        
        private static DebugSettings _instance;

        private static T GetValueOrDefault<T>(T value, T defaultValue = default)
        {
#if DEBUG_ENABLED
            return value;
#else
            return defaultValue;
#endif
        }
    }
}