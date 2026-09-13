using Lidgren.Network;

namespace SRMultiplayer.Packets
{
    /// <summary>
    /// Server -> client reply to <see cref="PacketPing"/>. Carries the world
    /// clock as well, which makes this the fastest-moving time sync available:
    /// the client knows the round trip that produced it and can correct for it.
    /// </summary>
    [Packet(PacketType.Pong)]
    public class PacketPong : Packet
    {
        /// <summary>The client's stamp from the matching ping, echoed back.</summary>
        public double ClientTime;

        /// <summary>Server world time at the moment the reply was built.</summary>
        public double WorldTime;

        public PacketPong() { }
        public PacketPong(NetIncomingMessage im) { Deserialize(im); }
    }
}
