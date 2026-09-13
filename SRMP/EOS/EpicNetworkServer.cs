using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using SRMultiplayer.Enums;
using SRMultiplayer.EpicSDK;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;
using UnityEngine;
using Object = System.Object;

namespace SRMultiplayer.Networking
{
    public class NetworkServer : EpicP2P
    {
        public static NetworkServer Instance { get; private set; }
        private byte nextPlayerId = 2;
        private Queue<byte> freeIds = new Queue<byte>();

        public ServerStatus status;
        
        public Dictionary<ProductUserId, byte> players = new Dictionary<ProductUserId, byte>();
        
        public enum ServerStatus
        {
            Stopped,
            Running
        }
        public NetworkServer(P2PInterface p2PInterface) : base(p2PInterface, true)
        {
            Instance = this;
        }
        
        public void StartListen()
        {
            // EOS SRMP Stuff
            SetupP2P();
            SRSingleton<EpicApplication>.Instance.Metrics.BeginSession();

            // Original SRMP Stuff
            status = ServerStatus.Running;

            Globals.DisableAchievements = true;
            Globals.PartyID = Guid.NewGuid();

            byte id = 1;
            while (id < 255 && Globals.Players.Values.Any(p => p.ID == id))
                id++;

            Globals.LocalID = id;
            Globals.LocalPlayer = SRSingleton<SceneContext>.Instance.Player.AddComponent<NetworkPlayer>();
            Globals.LocalPlayer.ID = id;
            Globals.LocalPlayer.Username = Globals.Username;
            Globals.LocalPlayer.HasLoaded = true;
            var _ = Globals.Mods; // Make sure mods list is set up to check for vr
            Globals.LocalPlayer.IsVR = Globals.VRInstalled;
            Globals.LocalPlayer.Spawn();
            Globals.Players.Add(id, Globals.LocalPlayer);
            Globals.PlayerToEpic.Add(id, EpicApplication.Instance.Authentication.ProductUserId);
            Globals.EpicToPlayer.Add(EpicApplication.Instance.Authentication.ProductUserId, id);
            Globals.ClientLoaded = true;

            var worldName = SRSingleton<GameContext>.Instance.AutoSaveDirector.SavedGame.GetName();
            Directory.CreateDirectory(Path.Combine(SRMP.ModDataPath, worldName));

            //bans belong to the world, so they follow whichever save is hosted
            Server.BanList.LoadForWorld(worldName);

            foreach(var netRegion in Globals.Regions.Values)
            {
                if(netRegion.Region.root.activeInHierarchy)
                {
                    netRegion.AddPlayer(Globals.LocalPlayer);
                    netRegion.TakeOwnership();
                }
            }
            foreach(var actor in SRSingleton<SceneContext>.Instance.GameModel.AllActors().Values)
            {
                if (actor.ident != Identifiable.Id.NONE && actor.ident != Identifiable.Id.PLAYER && !Identifiable.SCENE_OBJECTS.Contains(actor.ident))
                {
                    var netActor = actor.transform.gameObject.AddComponent<NetworkActor>();
                    netActor.ID = Utils.GetRandomActorID();
                    netActor.Ident = (ushort)actor.ident;
                    netActor.RegionSet = (byte)actor.currRegionSetId;
                    if (actor.transform.gameObject.activeInHierarchy)
                    {
                        netActor.Owner = Globals.LocalID;
                    }

                    Globals.Actors.Add(netActor.ID, netActor);
                }
            }
            foreach(var landPlot in SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().Values)
            {
                var netLandPlot = landPlot.gameObj.GetComponent<NetworkLandplot>();
                netLandPlot.Plot = landPlot.gameObj.GetComponentInChildren<LandPlot>(true);
            }
            foreach (TutorialDirector.Id tut in (TutorialDirector.Id[])Enum.GetValues(typeof(TutorialDirector.Id)))
            {
                SRSingleton<SceneContext>.Instance.TutorialDirector.tutModel.completedIds.Add(tut);
            }
        }

        public void SendPacket(ProductUserId targetUserId, IPacket packet,
            PacketReliability packetReliability = PacketReliability.ReliableOrdered)//, byte channel = 0)
        {
            NetOutgoingMessage om = new NetOutgoingMessage
            {
                Data = Array.Empty<byte>()
            };

            packet.Serialize(om);
            
            var fragCount = (om.m_data.Length + 999) / 1000;
            var payload = om.m_data;
            var len = payload.Length;
            
            byte[] packetTypeAsBytes = BitConverter.GetBytes((ushort)packet.GetPacketType());
            
            for (byte i = 0; i < fragCount; i++)
            {
                var offset = i * 1000;
                var chunkSize = Math.Min(1000, len - offset);
                
                var buffer = new byte[4 + chunkSize];
                buffer[0] = packetTypeAsBytes[0];
                buffer[1] = packetTypeAsBytes[1];
                buffer[2] = i;
                buffer[3] = (byte)fragCount;

                Buffer.BlockCopy(payload, offset, buffer, 4, chunkSize);

                SendDataInternal(targetUserId, buffer, packetReliability);
            }

            //SRMP.Log($"SEND {packet.GetPacketType()} {{ {BitConverter.ToString(om.m_data)} }}");
        }

        public void SendPacketToAll(IPacket packet, ProductUserId except = null, PacketReliability packetReliability = PacketReliability.ReliableOrdered)//, byte channel = 0)
        {
            var self = EpicApplication.Instance.Authentication.ProductUserId;
            foreach (var player in Globals.EpicToPlayer.Keys)
            {
                if (except == player || player == self) continue;

                SendPacket(player, packet, packetReliability);
            }
        }

        public override void OnMessageReceived(ProductUserId senderUserId, byte channel, ref NetIncomingMessage im)
        {
            
            if (Globals.EpicToPlayer.TryGetValue(senderUserId, out var playerID))
            {
                var player = Globals.Players[playerID];
                //SRMP.Log("Received Packet Fragment");
                try
                {
                    PacketType packetType = (PacketType)im.ReadUInt16();
                    //SRMP.Log($"Packet Frag Type: {packetType}");
                    byte fragmentIndex = im.ReadByte();
                    byte totalFragments = im.ReadByte();

                    byte[] payload = im.Data.Skip(4).ToArray();

                    if (!incompletePackets.TryGetValue(packetType, out var msg))
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
                        int debugLogIndex = 0;
                        foreach (var frag in msg.fragments)
                        {            
                            debugLogIndex++;
                            completeData.AddRange(frag);
                        }
                        //SRMP.Log("Packet Fragments combined");

                        incompletePackets.Remove(packetType);
                        im = new NetIncomingMessage
                        {
                            m_data = completeData.ToArray(),
                            LengthBytes = completeData.Count,
                            m_readPosition = 16,
                        };
                        
                        //SRMP.Log($"RECV {packetType} {{ {BitConverter.ToString(im.m_data)} }}");
                    }
                    else
                        return;
                    
                    if(player.State == NetworkPlayerState.Authenticating)
                    {
                        if(packetType != PacketType.Authentication)
                        {
                            SRMP.Log($"{player} sent {packetType} while authenticating!");
                            //CloseConnection(senderUserId); some bugs???
                            return;
                        }

                        im.m_readPosition = 0;

                        HandleAuthentication(player, im);
                    }
                    else if(player.State != NetworkPlayerState.Connected)
                    {
                        SRMP.Log($"{player} sent {packetType} while in state {player.State}");
                    }
                    else
                    {
                        NetworkHandlerServer.HandlePacket(packetType, im, player);
                    }
                }
                catch (Exception ex)
                {
                    SRMP.Log($"Error in network server! {ex}");
                    //CloseConnection(senderUserId);
                }
            }
            else SRMP.Log($"Unknown player {senderUserId} tried to send a packet!");
        }
        
        
        public override void OnConnected(ProductUserId remoteUserId, NetworkConnectionType networkType, ConnectionEstablishedType connectionType)
        {
            byte nextId;
            if (freeIds.Count > 0)
            {
                nextId = freeIds.Dequeue();
            }
            else
            {
                nextId = nextPlayerId++;
            }
            
            
            Globals.PlayerToEpic.Add(nextId, remoteUserId);
            Globals.EpicToPlayer.Add(remoteUserId, nextId);
            var playerObj = new GameObject("Player"+nextId);
            var player = playerObj.AddComponent<NetworkPlayer>();
            player.ID = nextId;
            Globals.Players.Add(nextId, player);

            player.State = NetworkPlayerState.Authenticating;
            
            
        }
        
        private void HandleAuthentication(NetworkPlayer player, NetIncomingMessage im)
        {
            var pid = player.ID;
            
            var username = im.ReadString();
            var guid = im.ReadBytes(16);
            var build = im.ReadInt32();
            
            List<string> mods = new List<string>();
            var modsLen = im.ReadInt32();
            for (var i = 0; i < modsLen; i++)
                mods.Add(im.ReadString());

            var vr = im.ReadBoolean();
            
            //checked before anything else accepts the player: the EOS id is
            //observed by us during the handshake, so it cannot be forged by
            //renaming or by editing client-side files
            if (Globals.PlayerToEpic.TryGetValue(pid, out var epicId)
                && Server.BanList.IsBanned(epicId.ToString()))
            {
                var ban = Server.BanList.Find(epicId.ToString());
                SRMP.Log($"[Bans] Refused banned player {username} ({epicId})");
                DisconnectCustom(player, $"You are banned from this world: {ban.Reason}");
                return;
            }

            if (build != Globals.Version)
            {
                SRMP.Log($"Version Mismatch! YOU({Globals.Version}) vs PLAYER({build})");
                DisconnectVersionMismatch(player);
                return;
            }

            var modsDiff1 = Globals.Mods.Where(x => !mods.Contains(x));
            var modsDiff2 = mods.Where(x => !Globals.Mods.Contains(x));
            if (modsDiff1.Count() != 0 || modsDiff2.Count() != 0)
            {
                var modsDiff3 = modsDiff1.Union(modsDiff2);
                DisconnectModMismatch(player, string.Join(", ", modsDiff3.ToArray()));
                return;
            }
            
            new PacketPlayerJoined()
            {
                ID = pid,
                Username = username,
                VR = vr
            }.SendToAllExcept(player, NetDeliveryMethod.ReliableOrdered);
            
            player.UUID = new Guid(guid);
            player.Username = username;
            player.name = $"{username} ({pid})";
            player.IsVR = vr;

            NetOutgoingMessage hail = new NetOutgoingMessage();
            
            hail.Write((ushort)PacketType.Authentication);
            hail.Write((byte)0);
            hail.Write((byte)1);
            hail.Write(pid);
            hail.Write(Globals.Players.Count);
            foreach (var p in Globals.Players.Values.ToList())
            {
                hail.Write(p.ID);
                hail.Write(p.Username);
                hail.Write(p.HasLoaded);
                hail.Write(p.IsVR);
            }
            hail.Write(Globals.PartyID.ToByteArray());
            hail.Write((byte)SRSingleton<SceneContext>.Instance.GameModel.currGameMode);
            hail.Write(SRSingleton<GameContext>.Instance.AutoSaveDirector.SavedGame.GetName());
            
            SRMP.Log("Sent Auth Packet");
            
            SendDataInternal(Globals.PlayerToEpic[pid], hail.Data);

            player.State = NetworkPlayerState.Connected;
        }

        public override void OnDisconnected(ProductUserId remoteUserId, ConnectionClosedReason reason)
        {
            if (Globals.EpicToPlayer.TryGetValue(remoteUserId, out var player))
            {
                ReclaimPlayerOwnership(player);

                if (Globals.Players.TryGetValue(player, out var leaving) && leaving != null)
                {
                    //the object outlives the dictionary entry otherwise, and stale
                    //entries linger in every region's player list
                    foreach (var region in leaving.Regions.ToList())
                    {
                        region.RemovePlayer(leaving);
                    }
                    UnityEngine.Object.Destroy(leaving.gameObject);
                }

                Globals.Players.Remove(player);
                Globals.EpicToPlayer.Remove(remoteUserId);
                Globals.PlayerToEpic.Remove(player);
                
                new PacketPlayerLeft()
                {
                    ID = player
                }.SendToAll();
            }
        }

        /// <summary>
        /// Hands everything a departing player was simulating back to the host.
        /// Left alone these actors and regions stay owned by a player id that no
        /// longer exists, so nobody simulates them and nobody can claim them.
        /// </summary>
        private void ReclaimPlayerOwnership(byte leavingId)
        {
            int actors = 0;
            foreach (var actor in Globals.Actors.Values.ToList())
            {
                if (actor == null || actor.Owner != leavingId) continue;

                actor.Owner = Globals.LocalID;
                actors++;

                new PacketActorOwner()
                {
                    ID = actor.ID,
                    Owner = Globals.LocalID
                }.SendToAll(NetDeliveryMethod.ReliableOrdered);
            }

            int regions = 0;
            foreach (var region in Globals.Regions.Values.ToList())
            {
                if (region == null || region.Owner != leavingId) continue;

                region.SetOwnership(Globals.LocalID);
                regions++;

                new PacketRegionOwner()
                {
                    ID = region.ID,
                    Owner = Globals.LocalID
                }.SendToAll(NetDeliveryMethod.ReliableOrdered);
            }

            if (actors > 0 || regions > 0)
            {
                SRMP.Log($"[Server] Reclaimed {actors} actor(s) and {regions} region(s) "
                         + $"from departing player {leavingId}");
            }
        }

        public override void OnConnectionRequest(ProductUserId remoteUserId)
        {
            if (EpicApplication.Instance.Lobby.ContainsUserId(remoteUserId))
            {
                AcceptConnection(remoteUserId);
            }
            else
            {
                CloseConnection(remoteUserId);
            }
        }

        public override void OnShutdown()
        {
            status = ServerStatus.Stopped;
        }

        public void DisconnectKick(NetworkPlayer player)
        {
            new PacketKickClient()
            {
                reason = PacketKickClient.Reason.Kicked
            }.Send(player, NetDeliveryMethod.ReliableOrdered);
            
            CloseConnection(Globals.PlayerToEpic[player.ID]);
        }
        /// <summary>
        /// Disconnect player due to a mismatch between versions.
        /// </summary>
        public void DisconnectVersionMismatch(NetworkPlayer player)
        {
            new PacketKickClient()
            {
                reason = PacketKickClient.Reason.VersionMismatch,
                data = Globals.Version
            }.Send(player, NetDeliveryMethod.ReliableOrdered);
            
            CloseConnection(Globals.PlayerToEpic[player.ID]);
        }
        /// <summary>
        /// Disconnect player with a custom message.
        /// </summary>
        public void DisconnectCustom(NetworkPlayer player, string msg)
        {
            new PacketKickClient()
            {
                reason = PacketKickClient.Reason.Custom,
                data = msg
            }.Send(player, NetDeliveryMethod.ReliableOrdered);
            
            CloseConnection(Globals.PlayerToEpic[player.ID]);
        }
        /// <summary>
        /// Disconnect player due to different dlcs being installed
        /// </summary>
        public void DisconnectDLCMismatch(NetworkPlayer player, string list)
        {
            new PacketKickClient()
            {
                reason = PacketKickClient.Reason.DLCMismatch,
                data = list
            }.Send(player, NetDeliveryMethod.ReliableOrdered);
            
            CloseConnection(Globals.PlayerToEpic[player.ID]);
        }
        /// <summary>
        /// Disconnect player due to different mods being installed
        /// </summary>
        public void DisconnectModMismatch(NetworkPlayer player, string list)
        {
            new PacketKickClient()
            {
                reason = PacketKickClient.Reason.ModsMismatch,
                data = list
            }.Send(player, NetDeliveryMethod.ReliableOrdered);
            
            CloseConnection(Globals.PlayerToEpic[player.ID]);
        }
    }
}
