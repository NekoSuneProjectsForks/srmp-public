using Lidgren.Network;
using System.Collections.Generic;

namespace SRMultiplayer.Packets
{
    /// <summary>
    /// Server -> client. Names the client can tab-complete that it could not know
    /// on its own, currently the banned names needed by /unban. Online players
    /// are already known locally and are not repeated here.
    /// </summary>
    [Packet(PacketType.NameSuggestions)]
    public class PacketNameSuggestions : Packet
    {
        public List<string> Names { get; set; }

        public PacketNameSuggestions() { }
        public PacketNameSuggestions(NetIncomingMessage im) { Deserialize(im); }

        public override void Serialize(NetOutgoingMessage om)
        {
            base.Serialize(om);

            om.Write(Names.Count);
            foreach (var name in Names)
            {
                om.Write(name ?? "");
            }
        }

        public override void Deserialize(NetIncomingMessage im)
        {
            base.Deserialize(im);

            Names = new List<string>();
            int count = im.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                Names.Add(im.ReadString());
            }
        }
    }
}
