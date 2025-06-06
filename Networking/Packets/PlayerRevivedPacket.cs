using LiteNetLib.Utils;

namespace CheekiRebreeki.Networking.Packets
{
    public struct PlayerRevivedPacket : INetSerializable
    {
        public string PlayerId;
        public float HealthPercent;
        
        public void Serialize(NetDataWriter writer)
        {
            writer.Put(PlayerId);
            writer.Put(HealthPercent);
        }
        
        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetString();
            HealthPercent = reader.GetFloat();
        }
    }
}