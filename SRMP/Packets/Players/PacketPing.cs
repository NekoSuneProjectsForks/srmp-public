using Lidgren.Network;

namespace SRMultiplayer.Packets
{
    /// <summary>
    /// Client -> server heartbeat used to measure round trip time. The client
    /// stamps its own clock and the server echoes it back untouched, so no clock
    /// synchronisation between the two machines is needed to derive the RTT.
    /// </summary>
    [Packet(PacketType.Ping)]
    public class PacketPing : Packet
    {
        /// <summary>Sender's local clock when this was sent. Echoed verbatim.</summary>
        public double ClientTime;

        /// <summary>
        /// The RTT this client last measured, in milliseconds, so the server can
        /// publish it to everyone else. The client is the only side that can
        /// measure it without a shared clock.
        /// </summary>
        public int ReportedPing;

        public PacketPing() { }
        public PacketPing(NetIncomingMessage im) { Deserialize(im); }
    }
}
