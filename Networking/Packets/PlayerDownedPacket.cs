using LiteNetLib.Utils;

namespace CheekiRebreeki.Networking.Packets
{
    public struct PlayerDownedPacket : INetSerializable
    {
        public string PlayerId;
        
        public void Serialize(NetDataWriter writer) => writer.Put(PlayerId);
        public void Deserialize(NetDataReader reader) => PlayerId = reader.GetString();
    }
}