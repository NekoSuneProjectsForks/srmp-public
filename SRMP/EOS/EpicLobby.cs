using Epic.OnlineServices;
using UnityEngine;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.RTC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SRMultiplayer.EpicSDK;
using SRMultiplayer.Networking;

namespace SRMultiplayer.EpicSDK
{
    public class EpicLobby
    {
        private LobbyInterface lobbyInterface;

        private ulong? addNotifyLobbyMemberStatusReceivedHandle;
        private ulong? addNotifyLobbyMemberUpdateReceivedHandle;
        private ulong? addNotifyLobbyUpdateReceivedHandle;
        private ulong? addNotifyRTCRoomConnectionChangedHandle;

        public Utf8String LobbyId { get; private set; }
        public bool IsInLobby => LobbyId != null;
        public bool IsLobbyOwner { get; private set; }

        public NetworkClient NetworkClient { get; private set; }
        public NetworkServer NetworkServer { get; private set; }

        public EpicLobby(LobbyInterface lobbyInterface)
        {
            this.lobbyInterface = lobbyInterface;
        }

        public void Tick()
        {
            NetworkClient?.Tick();
            NetworkServer?.Tick();
        }

        private void RegisterEvents()
        {
            if (!addNotifyLobbyMemberStatusReceivedHandle.HasValue)
            {
                var addNotifyLobbyMemberStatusReceivedOptions = new AddNotifyLobbyMemberStatusReceivedOptions()
                {

                };
                addNotifyLobbyMemberStatusReceivedHandle = lobbyInterface.AddNotifyLobbyMemberStatusReceived(ref addNotifyLobbyMemberStatusReceivedOptions, null, OnLobbyMemberStatusReceived);
            }

            if (!addNotifyLobbyMemberUpdateReceivedHandle.HasValue)
            {
                var notifyLobbyMemberUpdateReceivedOptions = new AddNotifyLobbyMemberUpdateReceivedOptions()
                {

                };
                addNotifyLobbyMemberUpdateReceivedHandle = lobbyInterface.AddNotifyLobbyMemberUpdateReceived(ref notifyLobbyMemberUpdateReceivedOptions, null, OnLobbyMemberUpdateReceived);
            }

            if (!addNotifyLobbyUpdateReceivedHandle.HasValue)
            {
                var addNotifyLobbyUpdateReceivedOptions = new AddNotifyLobbyUpdateReceivedOptions()
                {

                };
                addNotifyLobbyUpdateReceivedHandle = lobbyInterface.AddNotifyLobbyUpdateReceived(ref addNotifyLobbyUpdateReceivedOptions, null, OnLobbyUpdateReceived);
            }

            if (!addNotifyRTCRoomConnectionChangedHandle.HasValue)
            {
                var addNotifyRTCRoomConnectionChangedOptions = new AddNotifyRTCRoomConnectionChangedOptions()
                {

                };
                addNotifyRTCRoomConnectionChangedHandle = lobbyInterface.AddNotifyRTCRoomConnectionChanged(ref addNotifyRTCRoomConnectionChangedOptions, null, OnRTCRoomConnectionChanged);
            }

            var getRTCRoomNameOptions = new GetRTCRoomNameOptions()
            {
                LobbyId = LobbyId,
                LocalUserId = SRSingleton<EpicApplication>.Instance.Authentication.ProductUserId
            };
            var result = lobbyInterface.GetRTCRoomName(ref getRTCRoomNameOptions, out var roomName);
            if (result == Result.Success)
            {
            }
            else
            {
                SRMultiplayer.SRMP.Log($"GetRTCRoomName -> {result}");
            }
        }

        private void UnregisterEvents()
        {
            if (addNotifyLobbyMemberStatusReceivedHandle.HasValue)
            {
                lobbyInterface.RemoveNotifyLobbyMemberStatusReceived(addNotifyLobbyMemberStatusReceivedHandle.Value);
                addNotifyLobbyMemberStatusReceivedHandle = null;
            }
            if (addNotifyLobbyMemberUpdateReceivedHandle.HasValue)
            {
                lobbyInterface.RemoveNotifyLobbyMemberUpdateReceived(addNotifyLobbyMemberUpdateReceivedHandle.Value);
                addNotifyLobbyMemberUpdateReceivedHandle = null;
            }
            if (addNotifyLobbyUpdateReceivedHandle.HasValue)
            {
                lobbyInterface.RemoveNotifyLobbyUpdateReceived(addNotifyLobbyUpdateReceivedHandle.Value);
                addNotifyLobbyUpdateReceivedHandle = null;
            }
            if (addNotifyRTCRoomConnectionChangedHandle.HasValue)
            {
                lobbyInterface.RemoveNotifyRTCRoomConnectionChanged(addNotifyRTCRoomConnectionChangedHandle.Value);
                addNotifyRTCRoomConnectionChangedHandle = null;
            }

        }

        private void OnRTCRoomConnectionChanged(ref RTCRoomConnectionChangedCallbackInfo data)
        {
            SRMultiplayer.SRMP.Log($"{data.LocalUserId} {data.LobbyId} {data.IsConnected} {data.DisconnectReason}");
        }

        private void OnLobbyUpdateReceived(ref LobbyUpdateReceivedCallbackInfo data)
        {
            SRMultiplayer.SRMP.LogStack();
        }

        private void OnLobbyMemberUpdateReceived(ref LobbyMemberUpdateReceivedCallbackInfo data)
        {
            SRMultiplayer.SRMP.LogStack();
        }

        private void OnLobbyMemberStatusReceived(ref LobbyMemberStatusReceivedCallbackInfo data)
        {
            SRMultiplayer.SRMP.Log($"{data.LobbyId} {data.TargetUserId} {data.CurrentStatus}");
            if(data.TargetUserId == SRSingleton<EpicApplication>.Instance.Authentication.ProductUserId)
            {
                if(data.CurrentStatus == LobbyMemberStatus.Closed)
                {
                    NetworkClient.Shutdown();
                    NetworkClient = null;
                    UnregisterEvents();
                    LobbyId = null;
                }
            }
        }

        public void CreateLobby()
        {
            if(LobbyId != null)
            {
                SRMultiplayer.SRMP.Log($"Already in a lobby");
                return;
            }

            var createLobbyOptions = new CreateLobbyOptions()
            {
                AllowedPlatformIds = null,
                AllowInvites = true,
                RejoinAfterKickRequiresInvite = true,
                BucketId = "SRMP",
                CrossplayOptOut = false,
                DisableHostMigration = true,
                EnableJoinById = true,
                EnableRTCRoom = true,
                LobbyId = GenerateServerCode(),
                LocalRTCOptions = new LocalRTCOptions()
                {
                    LocalAudioDeviceInputStartsMuted = false,
                    UseManualAudioInput = false,
                    UseManualAudioOutput = true,
                    Flags = (uint)JoinRoomFlags.EnableEcho
                },
                LocalUserId = SRSingleton<EpicApplication>.Instance.Authentication.ProductUserId,
                MaxLobbyMembers = (uint)Mathf.Clamp(Globals.MaxPlayers, 2, 64),
                PermissionLevel = LobbyPermissionLevel.Joinviapresence,
                PresenceEnabled = false,
            };
            lobbyInterface.CreateLobby(ref createLobbyOptions, null, OnCreateLobby);
        }

        private void OnCreateLobby(ref CreateLobbyCallbackInfo data)
        {
            SRMultiplayer.SRMP.Log($"Result -> {data.ResultCode}");
            if(data.ResultCode != Result.Success)
            {
                return;
            }

            IsLobbyOwner = true;
            LobbyId = data.LobbyId;
            RegisterEvents();

            NetworkServer = SRSingleton<EpicApplication>.Instance.CreateServer();
            NetworkServer.StartListen();
        }

        public void JoinLobby(string lobbyId)
        {
            if (LobbyId != null)
            {
                SRMultiplayer.SRMP.Log($"Already in a lobby");
                return;
            }

            var joinLobbyOptions = new JoinLobbyByIdOptions()
            {
                CrossplayOptOut = false,
                LocalRTCOptions = new LocalRTCOptions()
                {
                    LocalAudioDeviceInputStartsMuted = false,
                    UseManualAudioInput = false,
                    UseManualAudioOutput = true,
                    Flags = 0
                },
                LocalUserId = SRSingleton<EpicApplication>.Instance.Authentication.ProductUserId,
                PresenceEnabled = false,
                LobbyId = lobbyId
            };
            lobbyInterface.JoinLobbyById(ref joinLobbyOptions, null, OnJoinLobby);
        }

        private void OnJoinLobby(ref JoinLobbyByIdCallbackInfo data)
        {
            SRMultiplayer.SRMP.Log($"Result -> {data.ResultCode}");
            if(data.ResultCode != Result.Success)
            {
                return;
            }

            IsLobbyOwner = false;
            LobbyId = data.LobbyId;
            RegisterEvents();

            var lobbyDetails = GetLobbyDetails();
            if(lobbyDetails == null)
            {
                SRMultiplayer.SRMP.Log($"Couldn't get LobbyDetails");
                return;
            }
            var lobbyDetailsGetLobbyOwnerOptions = new LobbyDetailsGetLobbyOwnerOptions() { };
            var ownerUserId = lobbyDetails.GetLobbyOwner(ref lobbyDetailsGetLobbyOwnerOptions);
            if(ownerUserId == null)
            {
                SRMultiplayer.SRMP.Log($"Couldn't get LobbyOwner");
                return;
            }

            NetworkClient = SRSingleton<EpicApplication>.Instance.CreateClient();
            NetworkClient.Connect(ownerUserId);

            lobbyDetails.Release();
        }

        public void LeaveLobby()
        {
            if (!IsInLobby || IsLobbyOwner)
                return;
            
            var leaveLobbyOptions = new LeaveLobbyOptions()
            {
                LocalUserId = SRSingleton<EpicApplication>.Instance.Authentication.ProductUserId,
                LobbyId = LobbyId
            };
            lobbyInterface.LeaveLobby(ref leaveLobbyOptions, null, OnLeaveLobby);
        }

        private void OnLeaveLobby(ref LeaveLobbyCallbackInfo data)
        {
            SRMP.Log($"Result -> {data.ResultCode}");
            if(data.ResultCode != Result.Success)
            {
                //the remote side is already gone or unreachable; keeping local
                //state would only block the next join
                ForceReset($"leave failed ({data.ResultCode})");
                return;
            }

            NetworkClient.Shutdown();
            NetworkClient = null;
            UnregisterEvents();
            LobbyId = null;
        }

        /// <summary>
        /// Drops all local lobby state unconditionally.
        ///
        /// Every other path clears LobbyId only inside a success callback, so a
        /// server that crashed or timed out leaves it set forever - and both
        /// CreateLobby and JoinLobby then refuse with "already in a lobby",
        /// which strands the player: they cannot rejoin and cannot host their
        /// own world until they restart the game.
        /// </summary>
        public void ForceReset(string reason)
        {
            if (LobbyId == null && NetworkClient == null && NetworkServer == null) return;

            SRMP.Log($"[Lobby] Forcing local lobby state reset: {reason}");

            try { NetworkClient?.Shutdown(); } catch { }
            try { NetworkServer?.Shutdown(); } catch { }

            NetworkClient = null;
            NetworkServer = null;

            try { UnregisterEvents(); } catch { }

            LobbyId = null;
            IsLobbyOwner = false;
        }

        public void KickMember(ProductUserId targetUserId)
        {
            var kickMemberOptions = new KickMemberOptions()
            {
                LobbyId = LobbyId,
                LocalUserId = SRSingleton<EpicApplication>.Instance.Authentication.ProductUserId,
                TargetUserId = targetUserId
            };
            lobbyInterface.KickMember(ref kickMemberOptions, null, OnKickMember);
        }

        private void OnKickMember(ref KickMemberCallbackInfo data)
        {
            SRMultiplayer.SRMP.Log($"Result -> {data.ResultCode}");
        }

        public void DestroyLobby()
        {
            if (!IsLobbyOwner || !IsInLobby)
                return;
            
            var destroyLobbyOptions = new DestroyLobbyOptions()
            {
                LobbyId = LobbyId,
                LocalUserId = SRSingleton<EpicApplication>.Instance.Authentication.ProductUserId
            };
            lobbyInterface.DestroyLobby(ref destroyLobbyOptions, null, OnDestroyLobby);
        }

        private void OnDestroyLobby(ref DestroyLobbyCallbackInfo data)
        {
            SRMP.Log($"Result -> {data.ResultCode}");
            if (data.ResultCode != Result.Success)
            {
                ForceReset($"destroy failed ({data.ResultCode})");
                return;
            }

            NetworkServer.Shutdown();
            NetworkServer = null;
            UnregisterEvents();
            LobbyId = null;
        }

        private LobbyDetails GetLobbyDetails()
        {
            var copyLobbyDetailsHandleOptions = new CopyLobbyDetailsHandleOptions()
            {
                LobbyId = LobbyId,
                LocalUserId = SRSingleton<EpicApplication>.Instance.Authentication.ProductUserId
            };
            var result = lobbyInterface.CopyLobbyDetailsHandle(ref copyLobbyDetailsHandleOptions, out var lobbyDetails);
            if (result != Result.Success)
            {
                SRMultiplayer.SRMP.Log($"CopyLobbyDetailsHandle -> {result}");
            }
            return lobbyDetails;
        }

        public bool ContainsUserId(ProductUserId targetUserId)
        {
            var lobbyDetails = GetLobbyDetails();
            if (lobbyDetails == null) return false;

            var lobbyDetailsGetMemberCountOptions = new LobbyDetailsGetMemberCountOptions() { };
            for (uint i = 0; i < lobbyDetails.GetMemberCount(ref lobbyDetailsGetMemberCountOptions); i++)
            {
                var lobbyDetailsGetMemberByIndexOptions = new LobbyDetailsGetMemberByIndexOptions() { MemberIndex = i };
                var lobbyMember = lobbyDetails.GetMemberByIndex(ref lobbyDetailsGetMemberByIndexOptions);
                if(lobbyMember != null && lobbyMember == targetUserId)
                {
                    lobbyDetails.Release();
                    return true;
                }
            }
            lobbyDetails.Release();
            return false;
        }
        
        public static string GenerateServerCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            System.Random random = new System.Random();
            char[] result = new char[7];

            for (int i = 0; i < 7; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }

    }
}
