using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace SRMultiplayer.Server
{
    /// <summary>
    /// Configuration for unattended (headless) hosting.
    /// Read from SRMP/autohost.json next to the game install; written with
    /// defaults on first run so an operator has something to edit.
    /// </summary>
    public class AutoHostConfig
    {
        /// <summary>Master switch. Nothing happens unless this is true.</summary>
        public bool Enabled = false;

        /// <summary>Name the host player shows up as.</summary>
        public string Username = "Server";

        /// <summary>
        /// Existing save to host. Matched against the internal game name first,
        /// then the display name. Leave blank to always start a fresh game.
        /// </summary>
        public string GameName = "";

        /// <summary>
        /// When <see cref="GameName"/> is blank, continue the most recently saved
        /// world instead of creating a fresh one. Without this a headless server
        /// starts a brand new world on every boot and the previous one is only
        /// reachable by naming it explicitly.
        /// </summary>
        public bool LoadLatestSave = true;

        /// <summary>Display name used when a new game has to be created.</summary>
        public string NewGameDisplayName = "SRMP Server";

        /// <summary>CLASSIC, CASUAL, TIME_LIMIT or TIME_LIMIT_V2.</summary>
        public string GameMode = "CLASSIC";

        /// <summary>Create a new game when <see cref="GameName"/> cannot be found.</summary>
        public bool CreateGameIfMissing = true;

        /// <summary>
        /// Lobby capacity including the host. EOS allows up to 64, but this is a
        /// full game simulation per player on one machine, so raising it costs
        /// host CPU and bandwidth, not just a number.
        /// </summary>
        public int MaxPlayers = 16;

        /// <summary>
        /// Make the host character unkillable. An unattended server has nobody to
        /// respond to a death, and every death fades the screen, clears carried
        /// ammo and teleports the host back to the ranch. Only applies to
        /// auto-hosting; someone hosting from their own client is unaffected.
        /// </summary>
        public bool GodMode = true;

        /// <summary>
        /// Frames per second the server aims for. This is the tick rate of the
        /// whole session, not a graphics setting: packets are only drained once
        /// per frame, so a slow loop shows up as everyone's ping and as world
        /// desync. 0 means uncapped. Vertical sync is always disabled, since
        /// waiting on a display the server does not have is pure latency.
        /// </summary>
        public int TargetFrameRate = 60;

        /// <summary>
        /// Usernames allowed to run privileged chat commands such as /tp. A
        /// headless server has nobody sitting at it, so without this nobody can
        /// use them at all.
        /// </summary>
        public List<string> Operators = new List<string>();

        /// <summary>Seconds to wait at the main menu before touching anything.</summary>
        public float StartupDelaySeconds = 3f;

        /// <summary>Seconds to wait for the EOS login before giving up.</summary>
        public float LoginTimeoutSeconds = 60f;

        /// <summary>Seconds to wait for the world to finish loading before giving up.</summary>
        public float LoadTimeoutSeconds = 300f;

        /// <summary>File the friend code is written to, relative to the SRMP data folder.</summary>
        public string ServerCodeFile = "servercode.txt";

        /// <summary>
        /// File watched for a shutdown request, relative to the SRMP data folder.
        /// Creating it asks the host to save the world and quit cleanly, which is
        /// the only way to stop a Unity game without losing progress.
        /// </summary>
        public string ShutdownRequestFile = "shutdown.request";

        /// <summary>Seconds between "players online" heartbeat lines. 0 disables.</summary>
        public float StatusIntervalSeconds = 60f;

        /// <summary>Trigger a game save on this interval. 0 disables.</summary>
        public float AutoSaveIntervalSeconds = 300f;

        public static string ConfigPath
        {
            get { return Path.Combine(SRMP.ModDataPath, "autohost.json"); }
        }

        /// <summary>
        /// Loads the config, creating it with defaults if absent. Never throws:
        /// a broken config falls back to defaults so the game still boots.
        /// </summary>
        public static AutoHostConfig Load()
        {
            try
            {
                if (!Directory.Exists(SRMP.ModDataPath))
                {
                    Directory.CreateDirectory(SRMP.ModDataPath);
                }

                if (!File.Exists(ConfigPath))
                {
                    var created = new AutoHostConfig();
                    File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(created, Formatting.Indented));
                    SRMP.Log("[AutoHost] Created default config at " + ConfigPath);
                    return created;
                }

                var config = JsonConvert.DeserializeObject<AutoHostConfig>(File.ReadAllText(ConfigPath));
                return config ?? new AutoHostConfig();
            }
            catch (Exception ex)
            {
                SRMP.Log("[AutoHost] Could not read config, using defaults\n" + ex);
                return new AutoHostConfig();
            }
        }

        /// <summary>
        /// Applies command line overrides so a server can be pointed at a different
        /// save without editing the config file.
        /// </summary>
        public void ApplyCommandLine(string[] args)
        {
            if (args == null) return;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-srmp-autohost":
                        Enabled = true;
                        break;
                    case "-srmp-username":
                        if (i + 1 < args.Length) Username = args[++i];
                        break;
                    case "-srmp-game":
                        if (i + 1 < args.Length) GameName = args[++i];
                        break;
                    case "-srmp-gamemode":
                        if (i + 1 < args.Length) GameMode = args[++i];
                        break;
                    case "-srmp-slots":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int slots))
                        {
                            MaxPlayers = slots;
                            i++;
                        }
                        break;
                }
            }
        }

        public PlayerState.GameMode ResolveGameMode()
        {
            try
            {
                return (PlayerState.GameMode)Enum.Parse(typeof(PlayerState.GameMode), GameMode, true);
            }
            catch
            {
                SRMP.Log("[AutoHost] Unknown game mode '" + GameMode + "', falling back to CLASSIC");
                return PlayerState.GameMode.CLASSIC;
            }
        }
    }
}
