namespace BuroDebug
{
    public static class DebugSettings
    {
        public static bool IsDebug
        {
            get
            {
#if UNITY_EDITOR || DEBUG_ENABLED
                return true;
#else
                return false;
#endif
            }
        }
    }
}