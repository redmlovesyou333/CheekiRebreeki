using EFT;
using EFT.HealthSystem;
using HarmonyLib;
using CheekiRebreeki.Core;
using CheekiRebreeki.Utils;

namespace CheekiRebreeki.Patches
{
    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage))]
    internal static class DamageInterceptPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ActiveHealthController __instance, EBodyPart bodyPart, ref float damage)
        {
            var player = __instance.Player;

            if (!PlayerUtils.IsPlayerValid(player) || !player.IsYourPlayer)
            {
                return true;
            }

            // Get the ReviveManager for the current raid.
            var reviveManager = CheekiRebreekiPlugin.ReviveManager;
            if (reviveManager == null)
            {
                return true;
            }

            if (reviveManager.IsPlayerBeingForceKilled(player.ProfileId))
            {
                return true;
            }

            if (reviveManager.IsPlayerDowned(player.ProfileId))
            {
                return false;
            }

            var currentHealth = __instance.GetBodyPartHealth(bodyPart);
            bool isLethalToVitalPart = (bodyPart == EBodyPart.Head || bodyPart == EBodyPart.Chest)
                                       && damage >= currentHealth.Current && currentHealth.Current > 0;

            if (isLethalToVitalPart)
            {
                Logger.LogInfo($"Intercepting lethal damage ({damage:F1}) to {bodyPart} for {PlayerUtils.GetPlayerName(player)}. Downing player instead.");
                reviveManager.SetPlayerDowned(player, nameof(DamageInterceptPatch));
                return false;
            }
            
            return true;
        }
    }
}