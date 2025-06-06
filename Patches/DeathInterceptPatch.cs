using EFT;
using EFT.HealthSystem;
using HarmonyLib;

namespace CheekiRebreeki.Patches
{
    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill))]
    internal static class DeathInterceptPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ActiveHealthController __instance)
        {
            // Prevent the original Kill() method from running if the death should be intercepted.
            // The return value is inverted: if PreventKill is true, Prefix must be false.
            return !PatchCommons.PreventKillIfApplicable(__instance.Player, nameof(DeathInterceptPatch));
        }
    }
}