using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EFT;
using EFT.HealthSystem;
using Fika.Core.Coop.Players;
using Fika.Core.Coop.Components;
using Fika.Core.Coop.Utils;
using CheekiRebreeki.Config;
using CheekiRebreeki.Networking;
using CheekiRebreeki.Networking.Packets;
using CheekiRebreeki.Utils;
using EFT.UI;
using EFT.Communications;
using Logger = CheekiRebreeki.Utils.Logger;

namespace CheekiRebreeki.Core
{
    internal class ReviveManager : IDisposable
    {
        private readonly Dictionary<string, DownedPlayerInfo> _downedPlayers = new Dictionary<string, DownedPlayerInfo>();
        private readonly HashSet<string> _playersBeingForceKilled = new HashSet<string>();
        private readonly Dictionary<string, float> _recentlyDownedProtection = new Dictionary<string, float>();
        
        private readonly List<string> _playersToAnimateDown = new List<string>();
        private readonly List<string> _playersToAnimateUp = new List<string>();
        private bool _isWiping;

        private readonly PluginConfig _config;
        private readonly NetworkHandler _networkHandler;
        
        public ReviveManager(PluginConfig config, NetworkHandler networkHandler)
        {
            _config = config;
            _networkHandler = networkHandler;
            
            _networkHandler.OnPlayerDownedReceived += HandlePlayerDownedPacket;
            _networkHandler.OnPlayerRevivedReceived += HandlePlayerRevivedPacket;
            _networkHandler.OnPlayerDiedReceived += HandlePlayerDiedPacket;
            _networkHandler.OnSquadWipeReceived += HandleSquadWipePacket;
        }
        
        public void Update()
        {
            CleanupRecentlyDownedProtection();
            AnimatePlayers();
            CheckForKeybindPresses();
        }

        private void CheckForKeybindPresses()
        {
            if (UnityEngine.Input.GetKeyDown(_config.ReviveKeybind.Value))
            {
                AttemptRevive();
            }
        }

        private void AttemptRevive()
        {
            var myPlayer = PlayerUtils.GetCoopHandler()?.MyPlayer;
            if (myPlayer?.ActiveHealthController?.IsAlive != true) return;

            var nearestDowned = FindNearestDownedPlayer(myPlayer);
            if (nearestDowned != null)
            {
                Logger.LogInfo($"Revive key pressed. Reviving {PlayerUtils.GetPlayerName(nearestDowned)}.");
                RevivePlayer(nearestDowned);
            }
        }

        private void AnimatePlayers()
        {
            if (_playersToAnimateDown.Any())
            {
                foreach (var playerId in _playersToAnimateDown.ToList())
                {
                    var player = PlayerUtils.GetCoopPlayerById(playerId);
                    if (player == null || !player.IsYourPlayer) continue;
                    if (player.MovementContext != null)
                    {
                        player.MovementContext.SetPoseLevel(0f, true);
                        player.MovementContext.IsInPronePose = true;
                    }
                    _playersToAnimateDown.Remove(playerId);
                }
            }

            if (_playersToAnimateUp.Any())
            {
                foreach (var playerId in _playersToAnimateUp.ToList())
                {
                    var player = PlayerUtils.GetCoopPlayerById(playerId);
                    if (player == null || !player.IsYourPlayer) continue;
                    if (player.MovementContext != null)
                    {
                        player.MovementContext.SetPoseLevel(0.5f, true);
                        player.MovementContext.IsInPronePose = false;
                    }
                    _playersToAnimateUp.Remove(playerId);
                }
            }
        }
        
        private void CleanupRecentlyDownedProtection()
        {
            if (!_recentlyDownedProtection.Any()) return;
            
            var keysToRemove = _recentlyDownedProtection
                .Where(kvp => Time.time >= kvp.Value + Constants.RECENTLY_DOWNED_PROTECTION_DURATION)
                .Select(kvp => kvp.Key).ToList();
                
            foreach (var key in keysToRemove) _recentlyDownedProtection.Remove(key);
        }
        
        public void SetPlayerDowned(Player player, string source)
        {
            if (!PlayerUtils.IsPlayerValid(player) || _downedPlayers.ContainsKey(player.ProfileId)) return;
            
            Logger.LogInfo($"Player {PlayerUtils.GetPlayerName(player)} is going down (Source: {source}).");
            
            var packet = new PlayerDownedPacket { PlayerId = player.ProfileId };
            _networkHandler.SendPacket(packet);
            HandlePlayerDownedPacket(packet);
        }
        
        public void RevivePlayer(CoopPlayer player)
        {
            if (!PlayerUtils.IsPlayerValid(player) || !_downedPlayers.ContainsKey(player.ProfileId)) return;
            
            Logger.LogInfo($"Initiating revive for player {PlayerUtils.GetPlayerName(player)}.");
            
            var packet = new PlayerRevivedPacket { PlayerId = player.ProfileId, HealthPercent = _config.ReviveHealthPercent.Value };
            _networkHandler.SendPacket(packet);
            HandlePlayerRevivedPacket(packet);
        }
        
        public void ForceTrulyKillPlayer(string profileId)
        {
            if (string.IsNullOrEmpty(profileId) || _playersBeingForceKilled.Contains(profileId))
            {
                return;
            }

            Logger.LogInfo($"Force killing player with ID {profileId}.");
            
            try
            {
                _playersBeingForceKilled.Add(profileId);
                
                var player = PlayerUtils.GetCoopPlayerById(profileId);
                if (player?.ActiveHealthController != null && player.ActiveHealthController.IsAlive)
                {
                    var damage = new DamageInfoStruct { Damage = Constants.LETHAL_DAMAGE_AMOUNT, DamageType = EDamageType.Blunt };
                    player.ActiveHealthController.ApplyDamage(EBodyPart.Head, damage.Damage, damage);
                }

                var packet = new PlayerDiedPacket { PlayerId = profileId };
                _networkHandler.SendPacket(packet);
                HandlePlayerDiedPacket(packet);
            }
            finally
            {
                _playersBeingForceKilled.Remove(profileId);
            }
        }
        
        public bool IsPlayerBeingForceKilled(string playerId) => _playersBeingForceKilled.Contains(playerId);
        public bool IsPlayerDowned(string playerId) => _downedPlayers.ContainsKey(playerId);
        
        #region Network Event Handlers

        private void HandlePlayerDownedPacket(PlayerDownedPacket packet)
        {
            if (_downedPlayers.ContainsKey(packet.PlayerId)) return;

            var targetPlayer = PlayerUtils.GetCoopPlayerById(packet.PlayerId);
            if (targetPlayer == null) return;

            _recentlyDownedProtection[packet.PlayerId] = Time.time;
            _downedPlayers[packet.PlayerId] = new DownedPlayerInfo();
            Logger.LogInfo($"Player {PlayerUtils.GetPlayerName(targetPlayer)} marked as downed.");

            if (targetPlayer.ActiveHealthController != null)
            {
                SetBodyPartToMinHealth(targetPlayer.ActiveHealthController, EBodyPart.Head);
                SetBodyPartToMinHealth(targetPlayer.ActiveHealthController, EBodyPart.Chest);
            }

            if (targetPlayer.IsYourPlayer)
            {
                if (!_playersToAnimateDown.Contains(packet.PlayerId)) _playersToAnimateDown.Add(packet.PlayerId);
                GamePlayerOwner.SetIgnoreInput(true);
                OnScreenTextManager.ShowDownedMessage("YOU ARE DOWNED!\nWaiting for revive...");
                ShowNotification(DOWNED_SELF, ENotificationDurationType.Long, ENotificationIconType.Alert);
            }
            else
            {
                ShowNotification(string.Format(TEAMMATE_DOWNED, PlayerUtils.GetPlayerName(targetPlayer)), ENotificationDurationType.Long, ENotificationIconType.Alert);
            }

            CheckForSquadWipe();
        }
        
        private void HandlePlayerRevivedPacket(PlayerRevivedPacket packet)
        {
            if (!_downedPlayers.ContainsKey(packet.PlayerId)) return;

            var targetPlayer = PlayerUtils.GetCoopPlayerById(packet.PlayerId);
            if (targetPlayer == null) return;

            _downedPlayers.Remove(packet.PlayerId);
            Logger.LogInfo($"Player {PlayerUtils.GetPlayerName(targetPlayer)} revived.");

            var healthController = targetPlayer.ActiveHealthController;
            if (healthController != null)
            {
                if (!healthController.IsAlive) healthController.IsAlive = true;
                foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
                {
                    if (bodyPart != EBodyPart.Common) RestoreBodyPartHealth(healthController, bodyPart, packet.HealthPercent / 100f);
                }
            }

            if (targetPlayer.IsYourPlayer)
            {
                GamePlayerOwner.SetIgnoreInput(false);
                OnScreenTextManager.HideDownedMessage();
                if (!_playersToAnimateUp.Contains(packet.PlayerId)) _playersToAnimateUp.Add(packet.PlayerId);
                ShowNotification(string.Format(REVIVED_SELF, "A teammate"), ENotificationDurationType.Long, ENotificationIconType.Friend);
            }
            else
            {
                ShowNotification(string.Format(TEAMMATE_REVIVED, PlayerUtils.GetPlayerName(targetPlayer), "a teammate"), ENotificationDurationType.Default, ENotificationIconType.Friend);
            }
        }
        
        private void HandlePlayerDiedPacket(PlayerDiedPacket packet)
        {
            if (!_downedPlayers.ContainsKey(packet.PlayerId))
            {
                // If the player isn't in the downed list, they are already considered dead.
                // This can happen if the packet arrives twice.
                return;
            }
            
            _downedPlayers.Remove(packet.PlayerId);
            
            var targetPlayer = PlayerUtils.GetCoopPlayerById(packet.PlayerId);
            if (targetPlayer != null)
            {
                Logger.LogInfo($"Player {PlayerUtils.GetPlayerName(targetPlayer)} is confirmed dead.");
                if (targetPlayer.IsYourPlayer)
                {
                    OnScreenTextManager.HideDownedMessage();
                    GamePlayerOwner.SetIgnoreInput(false);
                }
            }
        }

        private void HandleSquadWipePacket(SquadWipePacket packet)
        {
            Logger.LogInfo("Squad Wipe command received. Initiating local player death.");
            var myPlayer = PlayerUtils.GetCoopHandler()?.MyPlayer;
            if (myPlayer != null)
            {
                ForceTrulyKillPlayer(myPlayer.ProfileId);
            }
        }
        #endregion

        #region Helper Methods
        private void SetBodyPartToMinHealth(ActiveHealthController healthController, EBodyPart bodyPart)
        {
            var health = healthController.GetBodyPartHealth(bodyPart);
            float targetHealth = Constants.MIN_HEALTH_THRESHOLD;
            if (Math.Abs(health.Current - targetHealth) > Constants.HEALTH_DELTA_THRESHOLD)
            {
                healthController.ChangeHealth(bodyPart, targetHealth - health.Current, default);
            }
        }

        private static void RestoreBodyPartHealth(ActiveHealthController healthController, EBodyPart bodyPart, float healthPercent)
        {
            var bodyPartHealth = healthController.GetBodyPartHealth(bodyPart);
            float maxHealth = bodyPartHealth.Maximum;
            float targetHealth = Mathf.Clamp(maxHealth * healthPercent, Constants.MIN_HEALTH_THRESHOLD, maxHealth);
            if (bodyPartHealth.AtMinimum) healthController.FullRestoreBodyPart(bodyPart);
            float healthDelta = targetHealth - healthController.GetBodyPartHealth(bodyPart).Current;
            if (Math.Abs(healthDelta) > Constants.HEALTH_DELTA_THRESHOLD)
            {
                healthController.ChangeHealth(bodyPart, healthDelta, default);
            }
        }
        #endregion
        
        #region Debug Button Handlers
        public void OnForceDownToggled(object sender, EventArgs e)
        {
            var myPlayer = PlayerUtils.GetCoopHandler()?.MyPlayer;
            if (myPlayer != null) SetPlayerDowned(myPlayer, "DebugToggle");
        }
        
        public void OnForceTrulyKillToggled(object sender, EventArgs e)
        {
            var myPlayer = PlayerUtils.GetCoopHandler()?.MyPlayer;
            if (myPlayer != null) ForceTrulyKillPlayer(myPlayer.ProfileId);
        }
        #endregion
        
        private CoopPlayer FindNearestDownedPlayer(CoopPlayer fromPlayer)
        {
            var coopHandler = PlayerUtils.GetCoopHandler();
            if (coopHandler == null) return null;

            return coopHandler.Players.Values
                .Where(p => p != fromPlayer && !p.IsAI && _downedPlayers.ContainsKey(p.ProfileId))
                .OrderBy(p => Vector3.Distance(fromPlayer.Position, p.Position))
                .FirstOrDefault(p => Vector3.Distance(fromPlayer.Position, p.Position) <= _config.ReviveRadius.Value);
        }
        
        private void CheckForSquadWipe()
        {
            if (!FikaBackendUtils.IsServer || _isWiping) return;

            var coopHandler = PlayerUtils.GetCoopHandler();
            if (coopHandler == null || coopHandler.Players.Count <= 1) return;

            var allHumanPlayers = coopHandler.Players.Values.Where(p => p != null && !p.IsAI).ToList();
            if (!allHumanPlayers.Any()) return;

            bool isAnyoneAbleToRevive = allHumanPlayers.Any(p => !_downedPlayers.ContainsKey(p.ProfileId));

            if (!isAnyoneAbleToRevive)
            {
                _isWiping = true;
                Logger.LogInfo("SQUAD WIPE! All players are down. Sending wipe command.");
                
                var squadWipePacket = new SquadWipePacket();
                _networkHandler.SendPacket(squadWipePacket);

                // The server/host sends the packet but may not receive it back from the networking layer.
                // To ensure the host also processes the wipe, we call the handler directly.
                HandleSquadWipePacket(squadWipePacket);
            }
        }
        
        public void Dispose()
        {
            if (_networkHandler != null)
            {
                _networkHandler.OnPlayerDownedReceived -= HandlePlayerDownedPacket;
                _networkHandler.OnPlayerRevivedReceived -= HandlePlayerRevivedPacket;
                _networkHandler.OnPlayerDiedReceived -= HandlePlayerDiedPacket;
                _networkHandler.OnSquadWipeReceived -= HandleSquadWipePacket;
            }
        }
        
        private void ShowNotification(string message, ENotificationDurationType duration = ENotificationDurationType.Default, ENotificationIconType iconType = ENotificationIconType.Default)
        {
            NotificationManagerClass.DisplayMessageNotification(message, duration, iconType);
        }
        
        private const string DOWNED_SELF = "You are critically injured! A teammate can revive you!";
        private const string REVIVED_SELF = "{0} revived you! Find cover and heal!";
        private const string TEAMMATE_DOWNED = "SQUAD: {0} is down and needs help!";
        private const string TEAMMATE_REVIVED = "SQUAD: {0} is back in the fight, thanks to {1}!";
    }
}