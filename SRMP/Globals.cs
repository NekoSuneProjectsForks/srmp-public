using SRMultiplayer.Networking;
using SRMultiplayer.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Epic.OnlineServices;
using SRMultiplayer.Enums;
using SRMultiplayer.EpicSDK;
using UnityEngine;

namespace SRMultiplayer
{
    public static class Globals
    {

        //setup global objects for refrence usage
        public static int Version;
        public static UserData UserData;
        public static GameObject BeatrixModel;
        public static RuntimeAnimatorController BeatrixController;
        public static Dictionary<byte, NetworkPlayer> Players = new Dictionary<byte, NetworkPlayer>();
        public static string Username;

        /// <summary>
        /// Lobby capacity, including the host. EOS caps a lobby at 64; the mod
        /// has historically shipped 16. Set before the lobby is created - EOS
        /// fixes the size at creation time, so changing it later does nothing.
        /// </summary>
        public static int MaxPlayers = 16;

        /// <summary>
        /// Frame rate reported by the host. Shown next to the host in the player
        /// list because it, not the network, sets the floor on everyone's ping.
        /// </summary>
        public static int HostFps;

        /// <summary>
        /// Names the server offered for tab completion that the client could not
        /// work out locally - currently banned names, for /unban.
        /// </summary>
        public static List<string> SuggestedNames = new List<string>();

        public static string ServerCode
        {
            get
            {
                var lobby = EpicApplication.Instance.Lobby;
                if (lobby == null)
                    return "";
                return lobby.LobbyId;
            }
        }
        
        public static byte LocalID;
        public static NetworkPlayer LocalPlayer;
        public static bool HandlePacket;
        public static Guid PartyID;
        public static bool IsClient { get { return NetworkClient.Instance?.Status == NetworkClientStatus.Connected; } }
        public static bool IsServer { get { return NetworkServer.Instance?.status == NetworkServer.ServerStatus.Running; } }
        public static bool IsMultiplayer { get { return IsClient || IsServer; } }
        public static bool GameLoaded;
        public static bool ClientLoaded;
        public static bool DisableAchievements;
        public static string CurrentGameName;
        public static PauseState PauseState;
        public static Dictionary<string, SECTR_AudioCue> Audios = new Dictionary<string, SECTR_AudioCue>();
        public static Dictionary<int, NetworkActor> Actors = new Dictionary<int, NetworkActor>();
        public static Dictionary<int, NetworkRegion> Regions = new Dictionary<int, NetworkRegion>();
        public static Dictionary<string, NetworkLandplot> LandPlots = new Dictionary<string, NetworkLandplot>();
        public static Dictionary<string, GameObject> FXPrefabs = new Dictionary<string, GameObject>();
        public static Dictionary<string, NetworkAccessDoor> AccessDoors = new Dictionary<string, NetworkAccessDoor>();
        public static Dictionary<string, NetworkGordo> Gordos = new Dictionary<string, NetworkGordo>();
        public static Dictionary<int, NetworkSpawnResource> SpawnResources = new Dictionary<int, NetworkSpawnResource>();
        public static Dictionary<string, NetworkPuzzleSlot> PuzzleSlots = new Dictionary<string, NetworkPuzzleSlot>();
        public static Dictionary<string, NetworkWorldStateMasterSwitch> Switches = new Dictionary<string, NetworkWorldStateMasterSwitch>();
        public static Dictionary<string, NetworkGadgetSite> GadgetSites = new Dictionary<string, NetworkGadgetSite>();
        public static Dictionary<int, NetworkDirectedActorSpawner> Spawners = new Dictionary<int, NetworkDirectedActorSpawner>();
        public static Dictionary<string, NetworkTreasurePod> TreasurePods = new Dictionary<string, NetworkTreasurePod>();
        public static Dictionary<int, NetworkExchangeAcceptor> ExchangeAcceptors = new Dictionary<int, NetworkExchangeAcceptor>();
        public static Dictionary<int, NetworkFireColumn> FireColumns = new Dictionary<int, NetworkFireColumn>();
        public static Dictionary<int, NetworkKookadobaPatchNode> Kookadobas = new Dictionary<int, NetworkKookadobaPatchNode>();
        public static Dictionary<int, NetworkNutcracker> Nutcrackers = new Dictionary<int, NetworkNutcracker>();
        public static Dictionary<int, NetworkRaceTrigger> RaceTriggers = new Dictionary<int, NetworkRaceTrigger>();
        public static List<string> LemonTrees = new List<string>();
        public static Dictionary<PacketType, long> PacketSize = new Dictionary<PacketType, long>();
        public static Dictionary<ProductUserId, byte> EpicToPlayer = new Dictionary<ProductUserId, byte>();
        public static Dictionary<byte, ProductUserId> PlayerToEpic = new Dictionary<byte, ProductUserId>();

        internal static Dictionary<PacketType, Type> CustomPackets = new Dictionary<PacketType, Type>();
        
        /// <summary>
        /// get list of current installed mods
        /// Excluding supporting files
        /// </summary>
        public static List<string> Mods
        {
            get
            {
                List<string> mods = new List<string>();
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name.Equals("SRVR"))
                        VRInstalled = true;
                    
                    if (
                        // Main
                        !assembly.GetName().Name.Contains("Unity") && !assembly.GetName().Name.Contains("InControl") && !assembly.GetName().Name.Contains("DOTween") &&
                        !assembly.GetName().Name.Contains("mscorlib") && !assembly.GetName().Name.Contains("System") && !assembly.GetName().Name.Contains("Assembly-CSharp") &&
                        !assembly.GetName().Name.Contains("Logger") && !assembly.GetName().Name.Contains("Mono.") && !assembly.GetName().Name.Contains("Harmony") &&
                        !assembly.GetName().Name.Equals("SRML") && !assembly.GetName().Name.Equals("SRML.Editor") && !assembly.GetName().Name.Equals("Newtonsoft.Json") &&
                        !assembly.GetName().Name.Equals("INIFileParser") && !assembly.GetName().Name.Equals("SRMultiplayer") && !assembly.GetName().Name.Contains("Microsoft.") &&
                        !assembly.GetName().Name.Equals("SRMP") && !assembly.GetName().Name.Equals("XGamingRuntime") && !assembly.GetName().Name.Contains("MonoMod")  &&
                        
                        // Unity Explorer (Debugging)
                        !assembly.GetName().Name.Equals("UniverseLib.Mono") && !assembly.GetName().Name.Contains("eval-")  && !assembly.GetName().Name.Equals("Tomlet") &&
                        !assembly.GetName().Name.Equals("mcs")
                        
                        // VR
                        && !assembly.GetName().Name.Equals("Valve.Newtonsoft.Json") && !assembly.GetName().Name.Contains("VR")
                        
                        // Ignored Mods
                        && !UserData.IgnoredMods.Contains(assembly.GetName().Name))
                    {
                        mods.Add(assembly.GetName().Name);
                    }
                }
                return mods;
            }
        }

        public static void TryAdd<TK, TV>(this Dictionary<TK, TV> dict, TK key, TV value)
        {
            if (dict.ContainsKey(key))
                return;
            dict.Add(key, value);
        }

        public static bool NicknamesModInstalled => Mods.Contains("Nicknames");
        public static bool VRInstalled;
        
        
    }
}
