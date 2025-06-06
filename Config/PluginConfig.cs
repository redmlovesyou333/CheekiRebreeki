using System;
using BepInEx.Configuration;
using UnityEngine;
using Logger = CheekiRebreeki.Utils.Logger;

namespace CheekiRebreeki.Config
{
    internal class PluginConfig : IDisposable
    {
        public ConfigEntry<float> ReviveHealthPercent { get; private set; }
        public ConfigEntry<float> ReviveRadius { get; private set; }
        public ConfigEntry<KeyCode> ReviveKeybind { get; private set; }
        
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
            const string gameSettingsSection = "1. Game Settings";
            const string debugSection = "2. DEBUG";

            ReviveHealthPercent = config.Bind(gameSettingsSection, "Revive Health Percent", 30f,
                new ConfigDescription("Percentage of max health to restore on revive.", 
                    new AcceptableValueRange<float>(10f, 100f)));
                        
            ReviveRadius = config.Bind(gameSettingsSection, "Revive Radius", 5f,
                new ConfigDescription("Radius in meters to search for downed players.", 
                    new AcceptableValueRange<float>(1f, 20f)));

            ReviveKeybind = config.Bind(gameSettingsSection, "Revive Key", KeyCode.U,
                "The key to press to revive a nearby downed teammate.");
            
            ForceDownLocalPlayer = config.Bind(debugSection, "Force Down Local Player", false, 
                "Press F12 to open config menu. Toggle this to force your own player into the downed state for testing.");
                    
            ForceTrulyKillLocalPlayer = config.Bind(debugSection, "Force Truly Kill Local Player", false, 
                "Toggle this to force your own player to die permanently for testing.");

            Logger.LogInfo($"Config loaded: Health={ReviveHealthPercent.Value}%, Radius={ReviveRadius.Value}m, ReviveKey={ReviveKeybind.Value}");
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