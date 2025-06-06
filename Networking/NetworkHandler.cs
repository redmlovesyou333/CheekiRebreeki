using System;
using Comfort.Common;
using Fika.Core.Networking;
using Fika.Core.Coop.ClientClasses;
using Fika.Core.Coop.Utils;
using LiteNetLib;
using LiteNetLib.Utils;
using CheekiRebreeki.Networking.Packets;
using CheekiRebreeki.Utils;

namespace CheekiRebreeki.Networking
{
    internal class NetworkHandler : IDisposable
    {
        private IFikaNetworkManager _networkManager;
        private bool _isInitialized;
        
        public event Action<PlayerDownedPacket> OnPlayerDownedReceived;
        public event Action<PlayerRevivedPacket> OnPlayerRevivedReceived;
        public event Action<PlayerDiedPacket> OnPlayerDiedReceived;
        public event Action<SquadWipePacket> OnSquadWipeReceived;
        
        public void Update()
        {
            if (!_isInitialized)
            {
                TryInitialize();
            }
        }
        
        private void TryInitialize()
        {
            if (!Singleton<IFikaNetworkManager>.Instantiated)
            {
                Logger.LogInfo("Waiting for IFikaNetworkManager...", nameof(TryInitialize), throttleKey: "NetInit_WaitFika");
                return;
            }
            
            _networkManager = Singleton<IFikaNetworkManager>.Instance;
            if (_networkManager?.CoopHandler?.MyPlayer == null)
            {
                Logger.LogInfo("Waiting for local player to be ready...", nameof(TryInitialize), throttleKey: "NetInit_WaitPlayer");
                return;
            }

            RegisterPacketHandlers();
            _isInitialized = true;
            Logger.LogInfo($"NetworkHandler initialized successfully for player {PlayerUtils.GetPlayerName(_networkManager.CoopHandler.MyPlayer)}.");
        }
        
        private void RegisterPacketHandlers()
        {
            _networkManager.RegisterPacket<PlayerDownedPacket>(packet => OnPlayerDownedReceived?.Invoke(packet));
            _networkManager.RegisterPacket<PlayerRevivedPacket>(packet => OnPlayerRevivedReceived?.Invoke(packet));
            _networkManager.RegisterPacket<PlayerDiedPacket>(packet => OnPlayerDiedReceived?.Invoke(packet));
            _networkManager.RegisterPacket<SquadWipePacket>(packet => OnSquadWipeReceived?.Invoke(packet));
            Logger.LogInfo("Registered all network packet handlers.");
        }
        
        public void SendPacket<T>(T packet) where T : INetSerializable, new()
        {
            if (!_isInitialized || _networkManager == null)
            {
                Logger.LogWarning($"Cannot send {typeof(T).Name}: NetworkHandler not ready.");
                return;
            }
            
            if (FikaBackendUtils.IsServer)
            {
                Singleton<FikaServer>.Instance?.SendDataToAll(ref packet, DeliveryMethod.ReliableOrdered);
            }
            else if (Singleton<FikaClient>.Instantiated)
            {
                Singleton<FikaClient>.Instance?.SendData(ref packet, DeliveryMethod.ReliableOrdered);
            }
        }
        
        public void Dispose()
        {
            _isInitialized = false;
            _networkManager = null;
            
            OnPlayerDownedReceived = null;
            OnPlayerRevivedReceived = null;
            OnPlayerDiedReceived = null;
            OnSquadWipeReceived = null;
        }
    }
}