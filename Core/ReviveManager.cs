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
using CheekiRebreeki.Core.UI;
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
        private CoopPlayer _myPlayer;

        // State for reviving others
        private bool _isAttemptingRevive;
        private float _reviveHoldTimer;
        private CoopPlayer _currentTarget;

        // State for being revived by others
        private bool _isBeingRevived;
        private float _beingRevivedTimer;
        
        public ReviveManager(PluginConfig config, NetworkHandler networkHandler)
        {
            _config = config;
            _networkHandler = networkHandler;
            
            _networkHandler.OnPlayerDownedReceived += HandlePlayerDownedPacket;
            _networkHandler.OnPlayerRevivedReceived += HandlePlayerRevivedPacket;
            _networkHandler.OnPlayerDiedReceived += HandlePlayerDiedPacket;
            _networkHandler.OnSquadWipeReceived += HandleSquadWipePacket;
            _networkHandler.OnStartReviveReceived += HandleStartRevivePacket;
            _networkHandler.OnCancelReviveReceived += HandleCancelRevivePacket;
        }
        
        public void Update()
        {
            var coopHandler = PlayerUtils.GetCoopHandler();
            _myPlayer = (coopHandler != null) ? coopHandler.MyPlayer : null;
            
            if (_myPlayer == null || _myPlayer.ActiveHealthController == null) return;

            CleanupRecentlyDownedProtection();
            AnimatePlayers();

            bool isDowned = IsPlayerDowned(_myPlayer.ProfileId);
            bool isTrulyDead = !_myPlayer.ActiveHealthController.IsAlive;

            if (isTrulyDead || isDowned)
            {
                if (_isAttemptingRevive)
                {
                    Logger.LogInfo("Revive attempt cancelled because reviver is now downed or dead.");
                    if (_currentTarget != null)
                    {
                        _networkHandler.SendPacket(new CancelRevivePacket { TargetId = _currentTarget.ProfileId, ReviverId = _myPlayer.ProfileId });
                    }
                    ResetReviveAttempt();
                }
                
                RevivePromptManager.Hide();

                if (isDowned) UpdateBeingRevivedLogic();
                else ProgressBarManager.Hide();
                return;
            }
            
            UpdateRevivingLogic();
        }

        private void UpdateRevivingLogic()
        {
            if (_isAttemptingRevive)
            {
                bool stillHolding = UnityEngine.Input.GetKey(_config.ReviveKeybind.Value);
                bool targetStillValid = IsTargetStillValid(_currentTarget, _myPlayer);

                if (stillHolding && targetStillValid)
                {
                    _reviveHoldTimer += Time.deltaTime;
                    float progress = Mathf.Clamp01(_reviveHoldTimer / _config.ReviveHoldDuration.Value);
                    ProgressBarManager.UpdateProgress(progress);

                    if (progress >= 1f)
                    {
                        Logger.LogInfo($"Revive complete for {PlayerUtils.GetPlayerName(_currentTarget)}.");
                        RevivePlayer(_currentTarget);
                        ResetReviveAttempt();
                    }
                }
                else
                {
                    Logger.LogInfo("Revive attempt cancelled.");
                    if (_currentTarget != null)
                    {
                        _networkHandler.SendPacket(new CancelRevivePacket { TargetId = _currentTarget.ProfileId, ReviverId = _myPlayer.ProfileId });
                    }
                    ResetReviveAttempt();
                }
                return;
            }

            var lookedAtDownedPlayer = FindLookedAtDownedPlayer(_myPlayer);
            if (lookedAtDownedPlayer != null)
            {
                RevivePromptManager.Show(_config.ReviveKeybind.Value.ToString(), PlayerUtils.GetPlayerName(lookedAtDownedPlayer));

                if (UnityEngine.Input.GetKeyDown(_config.ReviveKeybind.Value))
                {
                    Logger.LogInfo($"Starting revive attempt on {PlayerUtils.GetPlayerName(lookedAtDownedPlayer)}.");
                    _isAttemptingRevive = true;
                    _reviveHoldTimer = 0f;
                    _currentTarget = lookedAtDownedPlayer;
                    
                    ProgressBarManager.Show("REVIVING...");
                    _networkHandler.SendPacket(new StartRevivePacket { TargetId = _currentTarget.ProfileId, ReviverId = _myPlayer.ProfileId });
                }
            }
            else
            {
                RevivePromptManager.Hide();
            }
        }

        private void UpdateBeingRevivedLogic()
        {
            if (_isBeingRevived)
            {
                _beingRevivedTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_beingRevivedTimer / _config.ReviveHoldDuration.Value);
                ProgressBarManager.UpdateProgress(progress);
            }
        }

        private void ResetReviveAttempt()
        {
            _isAttemptingRevive = false;
            _reviveHoldTimer = 0f;
            _currentTarget = null;
            ProgressBarManager.Hide();
            RevivePromptManager.Hide();
        }

        public void RevivePlayer(CoopPlayer player)
        {
            if (!PlayerUtils.IsPlayerValid(player) || !_downedPlayers.ContainsKey(player.ProfileId)) return;
            
            Logger.LogInfo($"Initiating revive for player {PlayerUtils.GetPlayerName(player)}.");
            
            var packet = new PlayerRevivedPacket { PlayerId = player.ProfileId, HealthPercent = _config.ReviveHealthPercent.Value };
            _networkHandler.SendPacket(packet);
            HandlePlayerRevivedPacket(packet);
        }

        #region Look-At and Validation Logic

        private CoopPlayer FindLookedAtDownedPlayer(CoopPlayer fromPlayer)
        {
            if (fromPlayer == null || fromPlayer.Fireport == null) return null;

            var fireportTransform = fromPlayer.Fireport;
            
            // Use a SphereCast for a more forgiving aim check.
            if (Physics.SphereCast(fireportTransform.position, Constants.REVIVE_AIM_SPHERECAST_RADIUS, fireportTransform.forward, out RaycastHit hit, _config.ReviveRadius.Value))
            {
                var hitPlayer = hit.collider.GetComponentInParent<Player>();
                if (hitPlayer == null || hitPlayer == fromPlayer || hitPlayer.IsAI) return null;

                // Check if the player we hit is actually downed and in range.
                if (_downedPlayers.ContainsKey(hitPlayer.ProfileId) && Vector3.Distance(fromPlayer.Position, hitPlayer.Position) <= _config.ReviveRadius.Value)
                {
                    // Fika's CoopPlayer objects are distinct from the base Player, so we need to get the CoopPlayer instance.
                    return PlayerUtils.GetCoopPlayerById(hitPlayer.ProfileId);
                }
            }

            return null;
        }

        private bool IsTargetStillValid(CoopPlayer target, CoopPlayer reviver)
        {
            if (target == null || reviver == null || reviver.Fireport == null) return false;

            // Basic checks: is the target still downed and within the maximum radius?
            if (!_downedPlayers.ContainsKey(target.ProfileId) || Vector3.Distance(reviver.Position, target.Position) > _config.ReviveRadius.Value)
            {
                return false;
            }

            var fireportTransform = reviver.Fireport;
            
            // Perform a SphereCast to see if we are still looking at the target.
            if (Physics.SphereCast(fireportTransform.position, Constants.REVIVE_AIM_SPHERECAST_RADIUS, fireportTransform.forward, out RaycastHit hit, _config.ReviveRadius.Value))
            {
                var hitPlayer = hit.collider.GetComponentInParent<Player>();
                // Check if the object we hit is the same player as our target.
                if (hitPlayer != null && hitPlayer.ProfileId == target.ProfileId)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Network Event Handlers

        private void HandleStartRevivePacket(StartRevivePacket packet)
        {
            if (_myPlayer == null || packet.TargetId != _myPlayer.ProfileId) return;

            _isBeingRevived = true;
            _beingRevivedTimer = 0f;
            var reviver = PlayerUtils.GetCoopPlayerById(packet.ReviverId);
            var reviverName = PlayerUtils.GetPlayerName(reviver).ToUpper();
            ProgressBarManager.Show($"BEING REVIVED BY {reviverName}");
        }

        private void HandleCancelRevivePacket(CancelRevivePacket packet)
        {
            if (_myPlayer == null || packet.TargetId != _myPlayer.ProfileId) return;

            _isBeingRevived = false;
            _beingRevivedTimer = 0f;
            ProgressBarManager.Hide();
        }

        private void HandlePlayerRevivedPacket(PlayerRevivedPacket packet)
        {
            if (!_downedPlayers.ContainsKey(packet.PlayerId)) return;

            var targetPlayer = PlayerUtils.GetCoopPlayerById(packet.PlayerId);
            if (targetPlayer == null) return;

            _downedPlayers.Remove(packet.PlayerId);
            Logger.LogInfo($"Player {PlayerUtils.GetPlayerName(targetPlayer)} revived.");

            if (targetPlayer.IsYourPlayer)
            {
                _isBeingRevived = false;
                ProgressBarManager.Hide();
            }

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
        
        private void HandlePlayerDiedPacket(PlayerDiedPacket packet)
        {
            if (!_downedPlayers.ContainsKey(packet.PlayerId))
            {
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
                    ProgressBarManager.Hide();
                    GamePlayerOwner.SetIgnoreInput(false);
                }
            }
        }

        private void HandleSquadWipePacket(SquadWipePacket packet)
        {
            Logger.LogInfo("Squad Wipe command received. Initiating local player death.");
            var coopHandler = PlayerUtils.GetCoopHandler();
            if (coopHandler == null) return;
            var myPlayer = coopHandler.MyPlayer;
            if (myPlayer != null)
            {
                ForceTrulyKillPlayer(myPlayer.ProfileId);
            }
        }

        #endregion

        #region Helper Methods
        
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
                if (player != null && player.ActiveHealthController != null && player.ActiveHealthController.IsAlive)
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
                HandleSquadWipePacket(squadWipePacket);
            }
        }
        
        private void ShowNotification(string message, ENotificationDurationType duration = ENotificationDurationType.Default, ENotificationIconType iconType = ENotificationIconType.Default)
        {
            NotificationManagerClass.DisplayMessageNotification(message, duration, iconType);
        }

        #endregion

        #region Debug Button Handlers
        
        public void OnForceDownToggled(object sender, EventArgs e)
        {
            var coopHandler = PlayerUtils.GetCoopHandler();
            if (coopHandler == null) return;
            var myPlayer = coopHandler.MyPlayer;
            if (myPlayer != null) SetPlayerDowned(myPlayer, "DebugToggle");
        }
        
        public void OnForceTrulyKillToggled(object sender, EventArgs e)
        {
            var coopHandler = PlayerUtils.GetCoopHandler();
            if (coopHandler == null) return;
            var myPlayer = coopHandler.MyPlayer;
            if (myPlayer != null) ForceTrulyKillPlayer(myPlayer.ProfileId);
        }
        
        #endregion
        
        public void Dispose()
        {
            if (_networkHandler != null)
            {
                _networkHandler.OnPlayerDownedReceived -= HandlePlayerDownedPacket;
                _networkHandler.OnPlayerRevivedReceived -= HandlePlayerRevivedPacket;
                _networkHandler.OnPlayerDiedReceived -= HandlePlayerDiedPacket;
                _networkHandler.OnSquadWipeReceived -= HandleSquadWipePacket;
                _networkHandler.OnStartReviveReceived -= HandleStartRevivePacket;
                _networkHandler.OnCancelReviveReceived -= HandleCancelRevivePacket;
            }
            
            ProgressBarManager.Hide();
            RevivePromptManager.Hide();
        }
        
        private const string DOWNED_SELF = "You are critically injured! A teammate can revive you!";
        private const string REVIVED_SELF = "{0} revived you! Find cover and heal!";
        private const string TEAMMATE_DOWNED = "SQUAD: {0} is down and needs help!";
        private const string TEAMMATE_REVIVED = "SQUAD: {0} is back in the fight, thanks to {1}!";
    }
}