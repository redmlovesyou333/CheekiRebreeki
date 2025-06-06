using LiteNetLib.Utils;

namespace CheekiRebreeki.Networking.Packets
{
    public struct PlayerDiedPacket : INetSerializable
    {
        public string PlayerId;
        
        public void Serialize(NetDataWriter writer) => writer.Put(PlayerId);
        public void Deserialize(NetDataReader reader) => PlayerId = reader.GetString();
    }
}