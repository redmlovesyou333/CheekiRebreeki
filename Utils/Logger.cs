using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using CheekiRebreeki.Core;

namespace CheekiRebreeki.Utils
{
    internal static class Logger
    {
        private static ManualLogSource _logSource;
        private static readonly Dictionary<string, float> _lastLogTime = new Dictionary<string, float>();
        
        public static void Initialize(ManualLogSource logger) => _logSource = logger;
        
        public static void LogInfo(string message, string caller = null, string throttleKey = null, float interval = Constants.DEFAULT_LOG_THROTTLE_INTERVAL)
        {
            if (ShouldThrottle(throttleKey, interval)) return;
            _logSource?.LogInfo(FormatMessage(message, caller));
        }
        
        public static void LogWarning(string message, string caller = null, string throttleKey = null, float interval = Constants.DEFAULT_LOG_THROTTLE_INTERVAL)
        {
            if (ShouldThrottle(throttleKey, interval)) return;
            _logSource?.LogWarning(FormatMessage(message, caller));
        }
        
        public static void LogError(string message, string caller = null)
        {
            _logSource?.LogError(FormatMessage(message, caller));
        }
        
        private static bool ShouldThrottle(string key, float interval)
        {
            if (key == null || _logSource == null) return false;
            
            if (_lastLogTime.TryGetValue(key, out float lastTime) && Time.unscaledTime < lastTime + interval)
            {
                return true;
            }
            
            _lastLogTime[key] = Time.unscaledTime;
            return false;
        }
        
        private static string FormatMessage(string message, string caller)
        {
            return $"[CheekiRebreeki] {(caller != null ? $"[{caller}] " : "")}{message}";
        }
    }
}