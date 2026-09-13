using Lidgren.Network;
using System.Collections.Generic;

namespace SRMultiplayer.Packets
{
    /// <summary>
    /// Server -> everyone. Publishes each player's measured round trip time so
    /// the multiplayer menu can show the whole lobby, not just the local player.
    /// </summary>
    [Packet(PacketType.PlayerPings)]
    public class PacketPlayerPings : Packet
    {
        public struct PingData
        {
            public byte ID;
            public ushort Ping;
        }

        /// <summary>
        /// The host's current frame rate. A client's round trip cannot be faster
        /// than the host's frame time, because packets are only drained once per
        /// frame - so this is the number that explains a bad ping.
        /// </summary>
        public ushort HostFps;

        //a property, not a field, so the base field serializer skips it and
        //leaves the list to the custom pass below
        public List<PingData> Pings { get; set; }

        public PacketPlayerPings() { }
        public PacketPlayerPings(NetIncomingMessage im) { Deserialize(im); }

        public override void Serialize(NetOutgoingMessage om)
        {
            base.Serialize(om);

            om.Write(Pings.Count);
            foreach (var ping in Pings)
            {
                om.Write(ping.ID);
                om.Write(ping.Ping);
            }
        }

        public override void Deserialize(NetIncomingMessage im)
        {
            base.Deserialize(im);

            Pings = new List<PingData>();
            int count = im.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                Pings.Add(new PingData()
                {
                    ID = im.ReadByte(),
                    Ping = im.ReadUInt16()
                });
            }
        }
    }
}
