namespace CheekiRebreeki.Core
{
    /// <summary>
    /// Holds state information for a player who is currently in the downed state.
    /// </summary>
    internal class DownedPlayerInfo
    {
        /// <summary>
        /// The amount of time, in seconds, the player has been downed.
        /// </summary>
        public float DownedTime { get; set; }

        /// <summary>
        /// Whether the player is in the downed state.
        /// </summary>
        public bool IsDowned { get; set; }

        /// <summary>
        /// Whether the player has bled out and is now considered truly dead.
        /// </summary>
        public bool IsTrulyDead { get; set; }
        
        public DownedPlayerInfo()
        {
            DownedTime = 0f;
            IsDowned = true;
            IsTrulyDead = false;
        }
    }
}