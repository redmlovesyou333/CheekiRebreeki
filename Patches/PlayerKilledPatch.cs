using EFT;
using HarmonyLib;

namespace CheekiRebreeki.Patches
{
    [HarmonyPatch(typeof(Player), nameof(Player.OnBeenKilledByAggressor))]
    internal static class PlayerKilledPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance)
        {
            // Prevent the original OnBeenKilledByAggressor() method from running if the death should be intercepted.
            return !PatchCommons.PreventKillIfApplicable(__instance, nameof(PlayerKilledPatch));
        }
    }
}