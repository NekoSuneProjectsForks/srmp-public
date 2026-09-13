using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using SRMultiplayer.Enums;
using SRMultiplayer.EpicSDK;
using SRMultiplayer.Packets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SRMultiplayer.Networking
{
    public class NetworkClient : EpicP2P
    {
        private ProductUserId serverUserId;
        
        public static NetworkClient Instance { get; private set; }
        
        public NetworkClientStatus Status { get; private set; }

        public NetworkClient(P2PInterface p2PInterface) : base(p2PInterface, false)
        {
            Status = NetworkClientStatus.None;
            Instance = this;
        }

        public void Connect(ProductUserId serverUserId)
        {
            if (serverUserId == null)
            {
                SRMP.Log("Cannot connect: server user id is null.");
                Status = NetworkClientStatus.Disconnected;
                return;
            }

            this.serverUserId = serverUserId;
            Status = NetworkClientStatus.Connecting;

            SetupP2P();

            AcceptConnection(serverUserId);
        }

        public void SendPacket(IPacket packet, PacketReliability packetReliability = PacketReliability.ReliableOrdered)//, byte channel = 0)
        {
            if (packet == null || serverUserId == null || Status == NetworkClientStatus.Disconnected || Status == NetworkClientStatus.None)
                return;

            NetOutgoingMessage om = new NetOutgoingMessage();
            om.Data = Array.Empty<byte>();
            
            packet.Serialize(om);
            
            int fragCount = (om.m_data.Length + 999) / 1000;
            var payload = om.m_data;
            int len = payload.Length;
            
            byte[] packetTypeAsBytes = BitConverter.GetBytes((ushort)packet.GetPacketType());
            
            for (byte i = 0; i < fragCount; i++)
            {               
                int offset = i * 1000;
                int chunkSize = Math.Min(1000, len - offset);
                
                byte[] buffer = new byte[4 + chunkSize];
                buffer[0] = packetTypeAsBytes[0];
                buffer[1] = packetTypeAsBytes[1];
                buffer[2] = i;
                buffer[3] = (byte)fragCount;

                Buffer.BlockCopy(payload, offset, buffer, 4, chunkSize);

                SendDataInternal(serverUserId, buffer, packetReliability);
            }
            
            //SRMP.Log($"SEND {packet.GetPacketType()} {{ {BitConverter.ToString(om.m_data)} }}");
        }

        public override void OnMessageReceived(ProductUserId senderUserId, byte channel, ref NetIncomingMessage im)
        {
            PacketType packetType = PacketType.Unknown;
            try
            {
                packetType = (PacketType)im.ReadUInt16();
                byte fragmentIndex = im.ReadByte();
                byte totalFragments = im.ReadByte();

                if (totalFragments == 0 || fragmentIndex >= totalFragments)
                {
                    SRMP.Log($"Discarded malformed packet fragment from server: type={packetType}, fragment={fragmentIndex}, total={totalFragments}");
                    return;
                }

                byte[] payload = im.Data.Skip(4).ToArray();

                if (!incompletePackets.TryGetValue(packetType, out var msg) || msg.fragTotal != totalFragments)
                {
                    msg = new IncompletePacket
                    {
                        fragments = new byte[totalFragments][],
                        fragTotal = totalFragments,
                        fragIndex = 0,
                    };
                    incompletePackets[packetType] = msg;
                }
                if (msg.fragments[fragmentIndex] == null)
                {
                    msg.fragments[fragmentIndex] = payload;
                    msg.fragIndex++;
                }

                if (msg.fragIndex >= msg.fragTotal)
                {
                    List<byte> completeData = new List<byte>();
                    foreach (var frag in msg.fragments)
                    {
                        if (frag == null)
                        {
                            SRMP.Log($"Discarded incomplete packet from server: {packetType}");
                            incompletePackets.Remove(packetType);
                            return;
                        }
                        completeData.AddRange(frag);
                    }
                        

                    incompletePackets.Remove(packetType);
                    im = new NetIncomingMessage
                    {
                        m_data = completeData.ToArray(),
                        LengthBytes = completeData.Count,
                        m_readPosition = 16
                    };
                
                    //SRMP.Log($"RECV {packetType} {{ {BitConverter.ToString(im.m_data)} }}");
                }
                else
                    return;

            }
            catch (Exception e)
            {
                SRMP.Log($"Exception in receiving packets from server:\n{e}");
                return;
            }
            if (packetType == PacketType.Authentication)
            {
                try
                {
                    im.m_readPosition = 0;
                    
                    Globals.LocalID = im.ReadByte();

                    int playerCount = im.ReadInt32();
                    for (int i = 0; i < playerCount; i++)
                    {
                        byte id = im.ReadByte();
                        string username = im.ReadString() ?? $"Player {id}";
                        bool hasloaded = im.ReadBoolean();
                        bool isVR = im.ReadBoolean();

                        if (Globals.Players.TryGetValue(id, out var existingPlayer) && existingPlayer != null)
                        {
                            UnityEngine.Object.Destroy(existingPlayer.gameObject);
                            Globals.Players.Remove(id);
                        }

                        var playerObject = new GameObject($"{username} ({id})");
                        var player = playerObject.AddComponent<NetworkPlayer>();
                        UnityEngine.Object.DontDestroyOnLoad(playerObject);

                        player.ID = id;
                        player.Username = username;
                        player.HasLoaded = hasloaded;
                        player.IsVR = isVR;
                        Globals.Players[id] = player;

                        if (id == Globals.LocalID)
                        {
                            Globals.LocalPlayer = player;
                        }
                        if (isVR)
                            SRMP.Log($"Player {id} is a VR player!");
                    }
                    Globals.PartyID = new Guid(im.ReadBytes(16));
                    var gameMode = (PlayerState.GameMode)im.ReadByte();
                    Globals.CurrentGameName = im.ReadString();
                    SRMP.Log("Auth Complete");
                    Status = NetworkClientStatus.Connected;

                    var gameContext = SRSingleton<GameContext>.Instance;
                    if (gameContext == null || gameContext.AutoSaveDirector == null)
                    {
                        SRMP.Log("Cannot finish multiplayer authentication: GameContext/AutoSaveDirector is not ready.");
                        Status = NetworkClientStatus.Disconnected;
                        return;
                    }

                    gameContext.AutoSaveDirector.LoadNewGame("SRMultiplayerGame", Identifiable.Id.GOLD_SLIME, gameMode, () =>
                    {
                        if (serverUserId != null)
                            CloseConnection(serverUserId);

                        SceneManager.LoadScene(2);
                    });
                }
                catch (Exception e)
                {
                    SRMP.Log($"Exception while applying authentication/join data:\n{e}");
                    Status = NetworkClientStatus.Disconnected;
                }
            }
            else
                NetworkHandlerClient.HandlePacket(packetType, im);
        }

        public override void OnShutdown()
        {
            EpicApplication.Instance?.Metrics?.EndSession();

            if (serverUserId != null)
                CloseConnection(serverUserId);

            Status = NetworkClientStatus.Disconnected;
            serverUserId = null;
            incompletePackets.Clear();

            //the lobby closing routes here rather than through OnDisconnected,
            //so without this the player is left standing in a world that is no
            //longer synchronised with anything
            if (SceneManager.GetActiveScene().buildIndex == 3)
            {
                Globals.GameLoaded = false;
                Globals.ClientLoaded = false;
                SceneManager.LoadScene(2);
            }
        }

        private void SendAuthentication()
        {
            if (serverUserId == null)
                return;

            NetOutgoingMessage om = new NetOutgoingMessage();
            om.Data = Array.Empty<byte>();

            om.Write((ushort)PacketType.Authentication);
            om.Write((byte)0);
            om.Write((byte)1);
            
            om.Write(Globals.Username ?? "Player");
            om.Write(Globals.UserData.UUID.ToByteArray());
            om.Write(Globals.Version);

            var mods = Globals.Mods;
            
            om.Write(mods.Count);
            foreach (var mod in mods)
                om.Write(mod);
            
            om.Write(Globals.VRInstalled);
            
            SendDataInternal(serverUserId, om.Data);
        }

        public override void OnConnected(ProductUserId remoteUserId, NetworkConnectionType networkType, ConnectionEstablishedType connectionType)
        {
            if (serverUserId == null || remoteUserId != serverUserId)
                return;

            Status = NetworkClientStatus.Authenticating;

            EpicApplication.Instance?.Metrics?.BeginSession();

            SendAuthentication();
        }

        public override void OnDisconnected(ProductUserId remoteUserId, ConnectionClosedReason reason)
        {
            if (serverUserId == null || remoteUserId != serverUserId)
                return;

            SRMP.Log($"Disconnected from server: {reason}");
            EpicApplication.Instance?.Metrics?.EndSession();

            Status = NetworkClientStatus.Disconnected;
            incompletePackets.Clear();
            serverUserId = null;

            if (SceneManager.GetActiveScene().buildIndex == 3)
            {
                Globals.GameLoaded = false;
                Globals.ClientLoaded = false;
                SceneManager.LoadScene(2);
            }
        }
        
        
    }
}
