using EFT;
using CheekiRebreeki.Utils;

namespace CheekiRebreeki.Patches
{
    internal static class PatchCommons
    {
        public static bool PreventKillIfApplicable(Player player, string patchName)
        {
            if (!PlayerUtils.IsPlayerValid(player) || !player.IsYourPlayer)
            {
                return false;
            }

            // Get the ReviveManager for the current raid.
            var reviveManager = CheekiRebreekiPlugin.ReviveManager;
            if (reviveManager == null)
            {
                return false;
            }

            if (reviveManager.IsPlayerBeingForceKilled(player.ProfileId))
            {
                return false;
            }

            if (reviveManager.IsPlayerDowned(player.ProfileId))
            {
                return true;
            }

            Logger.LogInfo($"Intercepting uncaught death event for {PlayerUtils.GetPlayerName(player)}. Downed by {patchName}.");
            reviveManager.SetPlayerDowned(player, patchName);
            return true;
        }
    }
}