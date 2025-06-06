using EFT;
using Fika.Core.Coop.Players;
using Fika.Core.Coop.Components;
using Comfort.Common;
using Fika.Core.Networking;

namespace CheekiRebreeki.Utils
{
    internal static class PlayerUtils
    {
        /// <summary>
        /// Finds a CoopPlayer instance by their profile ID.
        /// Note: This iterates through all players, which is acceptable for the small player counts in co-op.
        /// </summary>
        public static CoopPlayer GetCoopPlayerById(string profileId)
        {
            if (string.IsNullOrEmpty(profileId) || !Singleton<IFikaNetworkManager>.Instantiated)
            {
                return null;
            }
            
            var coopHandler = Singleton<IFikaNetworkManager>.Instance.CoopHandler;
            if (coopHandler?.Players == null)
            {
                return null;
            }

            // Fika's CoopHandler.Players dictionary is not keyed by ProfileId, so we must iterate.
            foreach (var player in coopHandler.Players.Values)
            {
                if (player?.ProfileId == profileId)
                {
                    return player;
                }
            }
            
            return null;
        }
        
        public static CoopHandler GetCoopHandler()
        {
            return Singleton<IFikaNetworkManager>.Instantiated 
                ? Singleton<IFikaNetworkManager>.Instance.CoopHandler 
                : null;
        }
        
        /// <summary>
        /// Checks if a player instance is a valid human player.
        /// </summary>
        public static bool IsPlayerValid(Player player)
        {
            return player != null && !string.IsNullOrEmpty(player.ProfileId) && !player.IsAI;
        }
        
        /// <summary>
        /// Gets a player's name, falling back to their ID if the name is not available.
        /// </summary>
        public static string GetPlayerName(Player player)
        {
            if (player == null) return "Unknown";
            return player.Profile?.Nickname ?? player.ProfileId ?? "Unknown";
        }
    }
}