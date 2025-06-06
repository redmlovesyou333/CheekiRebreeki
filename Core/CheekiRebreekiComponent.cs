using CheekiRebreeki.Networking;
using UnityEngine;

namespace CheekiRebreeki.Core
{
    /// <summary>
    /// A MonoBehaviour that manages the lifecycle of all raid-specific components.
    /// It is automatically added to the GameWorld when a raid starts.
    /// </summary>
    internal class CheekiRebreekiGameComponent : MonoBehaviour
    {
        private NetworkHandler _networkHandler;
        private ReviveManager _reviveManager;

        private void Awake()
        {
            var plugin = CheekiRebreekiPlugin.Instance;

            // 1. Create a new NetworkHandler for this raid.
            _networkHandler = new NetworkHandler();

            // 2. Create a new ReviveManager and give it the new NetworkHandler.
            _reviveManager = new ReviveManager(plugin.PluginConfig, _networkHandler);
            
            // 3. Subscribe the new manager's methods to the config events.
            plugin.PluginConfig.OnForceDownToggled += _reviveManager.OnForceDownToggled;
            plugin.PluginConfig.OnForceTrulyKillToggled += _reviveManager.OnForceTrulyKillToggled;

            // 4. Make the ReviveManager globally accessible for the duration of the raid.
            CheekiRebreekiPlugin.ReviveManager = _reviveManager;
            Utils.Logger.LogInfo("CheekiRebreekiGameComponent created. All raid components initialized.");
        }

        private void Update()
        {
            // Update both raid-specific components.
            _networkHandler?.Update();
            _reviveManager?.Update();
        }

        private void OnDestroy()
        {
            var plugin = CheekiRebreekiPlugin.Instance;

            // Unsubscribe from events to prevent memory leaks.
            if (plugin != null && _reviveManager != null)
            {
                plugin.PluginConfig.OnForceDownToggled -= _reviveManager.OnForceDownToggled;
                plugin.PluginConfig.OnForceTrulyKillToggled -= _reviveManager.OnForceTrulyKillToggled;
            }

            // Dispose of both components in reverse order of creation.
            _reviveManager?.Dispose();
            _networkHandler?.Dispose();
            
            CheekiRebreekiPlugin.ReviveManager = null;
            Utils.Logger.LogInfo("CheekiRebreekiGameComponent destroyed. All raid components disposed.");
        }
    }
}