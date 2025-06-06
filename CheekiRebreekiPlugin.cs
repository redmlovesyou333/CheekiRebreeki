using BepInEx;
using HarmonyLib;
using CheekiRebreeki.Config;
using CheekiRebreeki.Core;
using System;

namespace CheekiRebreeki
{
    [BepInPlugin("com.wekstex.cheekirebreeki", "CheekiRebreeki", "1.0.0")]
    public class CheekiRebreekiPlugin : BaseUnityPlugin
    {
        public static CheekiRebreekiPlugin Instance { get; private set; }
        
        private Harmony _harmony;
        
        // This component lives for the entire game session.
        internal PluginConfig PluginConfig { get; private set; }
        
        // This property holds the ReviveManager for the CURRENT raid.
        // It is set and managed by CheekiRebreekiGameComponent.
        internal static ReviveManager ReviveManager { get; set; }
        
        private void Awake()
        {
            Instance = this;
            
            Utils.Logger.Initialize(base.Logger); 
            Utils.Logger.LogInfo("CheekiRebreeki V1 Loading...");
            
            // Initialize session-long components.
            PluginConfig = new PluginConfig(Config);
            
            ApplyHarmonyPatches();
            
            Utils.Logger.LogInfo("Loaded successfully.");
        }
        
        private void ApplyHarmonyPatches()
        {
            try
            {
                _harmony = new Harmony("com.wekstex.cheekirebreeki");
                _harmony.PatchAll();
                Utils.Logger.LogInfo("Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Utils.Logger.LogError($"Failed to apply Harmony patches: {ex}");
            }
        }
        
        private void OnDestroy()
        {
            Utils.Logger.LogInfo("Shutting down...");
            
            _harmony?.UnpatchSelf();
            PluginConfig?.Dispose();
            
            Utils.Logger.LogInfo("Shutdown complete.");
        }
    }
} 