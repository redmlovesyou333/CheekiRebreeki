using System;
using BepInEx.Configuration;
using UnityEngine;
using Logger = CheekiRebreeki.Utils.Logger;

namespace CheekiRebreeki.Config
{
    internal class PluginConfig : IDisposable
    {
        // --- Gameplay Settings ---
        public ConfigEntry<KeyCode> ReviveKeybind { get; private set; }
        public ConfigEntry<float> ReviveRadius { get; private set; }
        public ConfigEntry<float> ReviveHoldDuration { get; private set; }
        public ConfigEntry<float> ReviveHealthPercent { get; private set; }

        // --- Debug Settings ---
        public ConfigEntry<bool> ForceDownLocalPlayer { get; private set; }
        public ConfigEntry<bool> ForceTrulyKillLocalPlayer { get; private set; }
        
        public event EventHandler OnForceDownToggled;
        public event EventHandler OnForceTrulyKillToggled;
        
        public PluginConfig(ConfigFile config)
        {
            InitializeConfig(config);
            SubscribeToConfigEvents();
        }
        
        private void InitializeConfig(ConfigFile config)
        {
            const string gameSettingsSection = "1. Gameplay Settings";
            const string debugSection = "2. DEBUG";

            ReviveKeybind = config.Bind(gameSettingsSection, "Revive Key", KeyCode.U,
                "The key to press to revive a nearby downed teammate.");

            ReviveRadius = config.Bind(gameSettingsSection, "Revive Radius", 2.5f,
                "How close you need to be to a downed teammate to revive them (in meters).");

            ReviveHoldDuration = config.Bind(gameSettingsSection, "Revive Duration", 4.0f,
                "How long you must hold the revive key to successfully revive a teammate (in seconds).");
            
            ReviveHealthPercent = config.Bind(gameSettingsSection, "Revive Health", 30.0f,
                "What percentage of max health a player is revived with.");

            // --- DEBUG ---
            ForceDownLocalPlayer = config.Bind(debugSection, "Force Down Local Player", false, 
                "Press F12 to open config menu. Toggle this to force your own player into the downed state for testing.");
                    
            ForceTrulyKillLocalPlayer = config.Bind(debugSection, "Force Truly Kill Local Player", false, 
                "Toggle this to force your own player to die permanently for testing.");

            Logger.LogInfo("Plugin configuration loaded.");
        }
        
        private void SubscribeToConfigEvents()
        {
            ForceDownLocalPlayer.SettingChanged += (sender, e) =>
            {
                if (ForceDownLocalPlayer.Value)
                {
                    OnForceDownToggled?.Invoke(sender, e);
                    ForceDownLocalPlayer.Value = false;
                }
            };
            
            ForceTrulyKillLocalPlayer.SettingChanged += (sender, e) =>
            {
                if (ForceTrulyKillLocalPlayer.Value)
                {
                    OnForceTrulyKillToggled?.Invoke(sender, e);
                    ForceTrulyKillLocalPlayer.Value = false;
                }
            };
        }
        
        public void Dispose()
        {
            // Unsubscribing is more complex with lambdas, but for BepInEx plugins that load once, this is generally safe.
        }
    }
}