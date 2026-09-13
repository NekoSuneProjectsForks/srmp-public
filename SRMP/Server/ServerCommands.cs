using Lidgren.Network;
using MonomiPark.SlimeRancher.Regions;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SRMultiplayer.Server
{
    /// <summary>
    /// Chat commands, handled on the server so they work identically whether the
    /// host is someone's client or a headless container. Anything starting with
    /// '/' is consumed here and never relayed to the rest of the lobby.
    /// </summary>
    internal static class ServerCommands
    {
        /// <summary>
        /// Usernames allowed to run privileged commands, alongside the host.
        /// Populated from the auto-host config; empty for a normal client host,
        /// where the host is simply whoever is playing.
        /// </summary>
        public static readonly HashSet<string> Operators =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Handles a chat line if it is a command.
        /// </summary>
        /// <returns>True if the line was a command and must not be broadcast.</returns>
        public static bool TryHandle(string message, NetworkPlayer sender)
        {
            if (string.IsNullOrEmpty(message) || !message.StartsWith("/")) return false;
            if (sender == null) return true;

            var parts = message.Substring(1)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return true;

            string command = parts[0].ToLowerInvariant();
            string[] args = parts.Skip(1).ToArray();

            try
            {
                switch (command)
                {
                    case "help": Help(sender); break;
                    case "tps": Tps(sender); break;
                    case "ping": Ping(sender); break;
                    case "list": case "players": ListPlayers(sender); break;
                    case "home": Home(sender); break;
                    case "tp": case "teleport": Teleport(sender, args); break;
                    case "ban": Ban(sender, args); break;
                    case "unban": case "pardon": Unban(sender, args); break;
                    case "banlist": case "bans": BanListing(sender); break;
                    default:
                        Reply(sender, $"Unknown command '/{command}'. Try /help.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Reply(sender, $"/{command} failed: {ex.Message}");
                SRMP.Log($"[Commands] /{command} threw\n{ex}");
            }

            return true;
        }

        private static bool IsOperator(NetworkPlayer player)
        {
            //the host is always an operator; on a headless server nobody is
            //sitting at it, so the configured list is how anyone gets authority
            return player.IsLocal || Operators.Contains(player.Username ?? "");
        }

        /// <summary>Sends a line back to one player only.</summary>
        private static void Reply(NetworkPlayer player, string text)
        {
            //the host has no network path to itself, so its replies go straight
            //to its own chat window
            if (player.IsLocal)
            {
                ChatUI.Instance?.AddChatMessage(text);
                SRMP.Log("[Commands] " + text);
                return;
            }

            new PacketPlayerChat { message = text }
                .Send(player, NetDeliveryMethod.ReliableOrdered);
        }

        private static void Help(NetworkPlayer sender)
        {
            Reply(sender, "Commands: /help /tps /ping /list /home");
            if (IsOperator(sender))
            {
                Reply(sender, "Operator: /tp <player> [destination|home]");
                Reply(sender, "Operator: /ban <player> [reason] | /unban <name> | /banlist");
            }
        }

        private static void Tps(NetworkPlayer sender)
        {
            int fps = SRMP.MeasuredFps;
            Reply(sender, $"Server tick rate: {fps} fps");

            //the number only means something with the reason attached
            if (fps > 0 && fps < 20)
            {
                Reply(sender, "That is low. Packets are only handled once per tick, "
                              + "so this is what your ping and any desync are waiting on.");
            }
            Reply(sender, $"Players online: {Globals.Players.Count}/{Globals.MaxPlayers}");
        }

        private static void Ping(NetworkPlayer sender)
        {
            Reply(sender, sender.IsLocal
                ? "You are the host; there is no round trip to measure."
                : $"Your ping: {sender.Ping} ms");
        }

        private static void ListPlayers(NetworkPlayer sender)
        {
            var lines = Globals.Players.Values
                .Where(p => p != null)
                .Select(p => p.IsLocal ? $"{p.Username} (host)" : $"{p.Username} {p.Ping}ms");

            Reply(sender, "Online: " + string.Join(", ", lines.ToArray()));
        }

        private static void Home(NetworkPlayer sender)
        {
            var home = SRSingleton<SceneContext>.Instance.GetWakeUpDestination();
            TeleportTo(sender, home.transform.position, home.transform.eulerAngles.y,
                (byte)home.GetRegionSetId());
            Reply(sender, "Teleported home.");
        }

        private static void Teleport(NetworkPlayer sender, string[] args)
        {
            if (!IsOperator(sender))
            {
                Reply(sender, "/tp is operator only.");
                return;
            }

            if (args.Length < 1 || args.Length > 2)
            {
                Reply(sender, "Usage: /tp <player> [destination|home]  "
                              + "- with one argument you are moved to that player.");
                return;
            }

            //one argument moves the caller; two moves someone else
            string targetName = args.Length == 2 ? args[0] : sender.Username;
            string destination = args.Length == 2 ? args[1] : args[0];

            var target = FindPlayer(targetName);
            if (target == null)
            {
                Reply(sender, $"No player called '{targetName}'.");
                return;
            }

            if (destination.Equals("home", StringComparison.OrdinalIgnoreCase))
            {
                var home = SRSingleton<SceneContext>.Instance.GetWakeUpDestination();
                TeleportTo(target, home.transform.position, home.transform.eulerAngles.y,
                    (byte)home.GetRegionSetId());
                Reply(sender, $"Teleported {target.Username} home.");
                return;
            }

            var destPlayer = FindPlayer(destination);
            if (destPlayer == null)
            {
                Reply(sender, $"No destination called '{destination}'.");
                return;
            }

            TeleportTo(target, destPlayer.transform.position, destPlayer.transform.eulerAngles.y,
                (byte)destPlayer.CurrentRegionSet);
            Reply(sender, $"Teleported {target.Username} to {destPlayer.Username}.");

            if (target != sender)
            {
                Reply(target, $"{sender.Username} teleported you to {destPlayer.Username}.");
            }
        }

        private static void Ban(NetworkPlayer sender, string[] args)
        {
            if (!IsOperator(sender)) { Reply(sender, "/ban is operator only."); return; }
            if (args.Length < 1) { Reply(sender, "Usage: /ban <player> [reason]"); return; }

            var target = FindPlayer(args[0]);
            if (target == null) { Reply(sender, $"No player called '{args[0]}' is online."); return; }
            if (target.IsLocal) { Reply(sender, "Refusing to ban the host."); return; }
            if (IsOperator(target)) { Reply(sender, $"{target.Username} is an operator."); return; }

            if (!Globals.PlayerToEpic.TryGetValue(target.ID, out var epicId))
            {
                Reply(sender, $"No stable id for {target.Username}; cannot ban safely.");
                return;
            }

            string reason = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "";
            BanList.Add(epicId.ToString(), target.Username, reason, sender.Username);

            //a ban is a kick that also sticks, and it is deliberately a normal
            //disconnect so the player is free to join anywhere else
            NetworkServer.Instance.DisconnectCustom(target,
                $"You are banned from this world: {(string.IsNullOrEmpty(reason) ? "No reason given" : reason)}");

            Reply(sender, $"Banned {target.Username}.");
            BroadcastSuggestions();
        }

        private static void Unban(NetworkPlayer sender, string[] args)
        {
            if (!IsOperator(sender)) { Reply(sender, "/unban is operator only."); return; }

            if (args.Length < 1)
            {
                //also refreshes the caller's tab completion for the next attempt
                BanListing(sender);
                Reply(sender, "Usage: /unban <name>  (press Tab to complete)");
                return;
            }

            var removed = BanList.Remove(string.Join(" ", args));
            Reply(sender, removed == null
                ? $"'{string.Join(" ", args)}' is not banned."
                : $"Unbanned {removed.Username}.");

            BroadcastSuggestions();
        }

        private static void BanListing(NetworkPlayer sender)
        {
            if (!IsOperator(sender)) { Reply(sender, "/banlist is operator only."); return; }

            SendSuggestions(sender);

            if (BanList.Count == 0) { Reply(sender, "Nobody is banned from this world."); return; }

            Reply(sender, $"Banned ({BanList.Count}):");
            foreach (var entry in BanList.Entries)
            {
                Reply(sender, $"  {entry.Username} - {entry.Reason} (by {entry.BannedBy}, {entry.BannedAt})");
            }
        }

        /// <summary>
        /// Gives a client the names it cannot derive locally, so Tab can complete
        /// a banned player who is by definition not online.
        /// </summary>
        private static void SendSuggestions(NetworkPlayer player)
        {
            if (player.IsLocal)
            {
                Globals.SuggestedNames = BanList.Names;
                return;
            }

            new PacketNameSuggestions { Names = BanList.Names }
                .Send(player, NetDeliveryMethod.ReliableOrdered);
        }

        private static void BroadcastSuggestions()
        {
            foreach (var player in Globals.Players.Values.ToList())
            {
                if (player != null && IsOperator(player)) SendSuggestions(player);
            }
        }

        private static NetworkPlayer FindPlayer(string name)
        {
            return Globals.Players.Values.FirstOrDefault(p =>
                p != null && string.Equals(p.Username, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Moves a player. A remote player is moved by a position packet flagged
        /// as a teleport; the host has to move its own transform directly,
        /// because it never receives its own packets.
        /// </summary>
        private static void TeleportTo(NetworkPlayer player, Vector3 position, float rotation, byte regionSet)
        {
            if (player.IsLocal)
            {
                var scene = SRSingleton<SceneContext>.Instance;
                scene.player.transform.position = position;
                scene.player.transform.eulerAngles = new Vector3(0, rotation, 0);
                scene.PlayerState.model.SetCurrRegionSet((RegionRegistry.RegionSetId)regionSet);
                SRSingleton<Overlay>.Instance?.PlayTeleport();
                return;
            }

            new PacketPlayerPosition()
            {
                ID = player.ID,
                Position = position,
                Rotation = rotation,
                RegionSet = regionSet,
                WeaponY = player.GetWeaponLocation(),
                OnLoad = false
            }.Send(player, NetDeliveryMethod.ReliableOrdered);
        }
    }
}
