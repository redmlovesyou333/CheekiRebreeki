using LiteNetLib.Utils;

namespace CheekiRebreeki.Networking.Packets
{
    public struct SquadWipePacket : INetSerializable
    {
        // This packet needs no data, its existence is the command.
        public void Serialize(NetDataWriter writer) { }
        public void Deserialize(NetDataReader reader) { }
    }
}