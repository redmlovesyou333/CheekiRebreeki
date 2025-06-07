using LiteNetLib.Utils;

namespace CheekiRebreeki.Networking.Packets
{
    public struct CancelRevivePacket : INetSerializable
    {
        public string TargetId;
        public string ReviverId;
        
        public void Serialize(NetDataWriter writer)
        {
            writer.Put(TargetId);
            writer.Put(ReviverId);
        }
        
        public void Deserialize(NetDataReader reader)
        {
            TargetId = reader.GetString();
            ReviverId = reader.GetString();
        }
    }
}