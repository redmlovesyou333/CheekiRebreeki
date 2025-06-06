namespace CheekiRebreeki.Core
{
    internal static class Constants
    {
        // A brief period after being downed where subsequent death events are ignored to prevent race conditions.
        public const float RECENTLY_DOWNED_PROTECTION_DURATION = 0.25f;
        
        // Default interval to prevent log spam for repeated messages.
        public const float DEFAULT_LOG_THROTTLE_INTERVAL = 2.0f;
        
        // A damage value high enough to guarantee a kill.
        public const float LETHAL_DAMAGE_AMOUNT = 9999f;
        
        // The minimum health a body part will have after being downed.
        public const float MIN_HEALTH_THRESHOLD = 1f;
        
        // A small delta to avoid floating point comparison issues with health values.
        public const float HEALTH_DELTA_THRESHOLD = 0.01f;
    }
}