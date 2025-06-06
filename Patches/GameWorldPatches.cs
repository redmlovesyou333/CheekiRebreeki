using HarmonyLib;
using EFT;
using CheekiRebreeki.Core;

namespace CheekiRebreeki.Patches
{
    [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.OnGameStarted))]
    internal static class GameWorldPatches
    {
        /// <summary>
        /// After the GameWorld has started, add our custom component to it.
        /// This ensures a new ReviveManager is created for every raid.
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(GameWorld __instance)
        {
            if (__instance.gameObject.GetComponent<CheekiRebreekiGameComponent>() == null)
            {
                __instance.gameObject.AddComponent<CheekiRebreekiGameComponent>();
            }
        }
    }
}