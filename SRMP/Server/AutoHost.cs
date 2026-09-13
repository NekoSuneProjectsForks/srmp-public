using Lidgren.Network;
using SRMultiplayer.EpicSDK;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SRMultiplayer.Server
{
    /// <summary>
    /// Drives an unattended host: loads a save, opens the lobby and publishes the
    /// friend code, all without anyone touching the multiplayer window. Intended
    /// for running the game headless (Xvfb/Proton) as a de facto server.
    /// </summary>
    public class AutoHost : SRSingleton<AutoHost>
    {
        private const int MainMenuScene = 2;
        private const int GameScene = 3;

        public AutoHostConfig Config { get; private set; }
        public bool IsHosting { get; private set; }

        /// <summary>
        /// True once the host character has been tucked away. Gates the
        /// invulnerability patch so a normal client host is never affected.
        /// </summary>
        public bool IsParked { get; private set; }

        private Vector3 m_ParkPosition;

        private float m_LastStatus;
        private float m_LastSave;
        private float m_LastShutdownCheck;
        private bool m_ShuttingDown;

        public override void Awake()
        {
            base.Awake();

            Config = AutoHostConfig.Load();
            Config.ApplyCommandLine(Environment.GetCommandLineArgs());

            if (!Config.Enabled)
            {
                return;
            }

            //claim the username before EpicApplication logs in with it
            Globals.Username = Config.Username;

            //EOS fixes lobby size at creation, so this must be set before hosting
            Globals.MaxPlayers = Mathf.Clamp(Config.MaxPlayers, 2, 64);

            ServerLog("=====================================");
            ServerLog(" SRMP headless host starting");
            ServerLog(" username : " + Config.Username);
            ServerLog(" save     : " + (string.IsNullOrEmpty(Config.GameName)
                ? (Config.LoadLatestSave ? "<most recent>" : "<new game>")
                : Config.GameName));
            ServerLog(" slots    : " + Globals.MaxPlayers);
            ServerLog(" config   : " + AutoHostConfig.ConfigPath);
            ServerLog("=====================================");
        }

        private void Start()
        {
            if (!Config.Enabled) return;

            //MultiplayerUI.Start restores the interactive username from PlayerPrefs;
            //component order puts us after it, so re-assert ours here as well
            Globals.Username = Config.Username;

            StartCoroutine(RunHost());
        }

        private IEnumerator RunHost()
        {
            yield return WaitForMainMenu();
            yield return new WaitForSeconds(Config.StartupDelaySeconds);

            bool loggedIn = false;
            yield return WaitForLogin(ok => loggedIn = ok);
            if (!loggedIn)
            {
                ServerLog("[AutoHost] EOS login did not complete, aborting host startup");
                yield break;
            }

            if (!LoadWorld())
            {
                yield break;
            }

            bool loaded = false;
            yield return WaitForWorld(ok => loaded = ok);
            if (!loaded)
            {
                ServerLog("[AutoHost] World did not finish loading, aborting host startup");
                yield break;
            }

            //let the scene settle before the server walks every actor in it
            yield return new WaitForSeconds(2f);

            ServerLog("[AutoHost] Opening lobby...");
            EpicApplication.Instance.Lobby.CreateLobby();

            float deadline = Time.realtimeSinceStartup + 60f;
            while (!Globals.IsServer && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!Globals.IsServer)
            {
                ServerLog("[AutoHost] Lobby creation timed out, no server running");
                yield break;
            }

            IsHosting = true;
            ParkHostPlayer();
            PublishServerCode();
        }

        /// <summary>Waits until the main menu scene is the active one.</summary>
        private IEnumerator WaitForMainMenu()
        {
            while (SceneManager.GetActiveScene().buildIndex != MainMenuScene
                   || SRSingleton<GameContext>.Instance == null)
            {
                yield return null;
            }
        }

        /// <summary>Waits for the anonymous EOS device login to land.</summary>
        private IEnumerator WaitForLogin(Action<bool> result)
        {
            float deadline = Time.realtimeSinceStartup + Config.LoginTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var app = EpicApplication.Instance;
                if (app != null && app.Authentication != null && app.Authentication.IsLoggedIn)
                {
                    ServerLog("[AutoHost] EOS login complete as " + app.Authentication.Username);
                    result(true);
                    yield break;
                }
                yield return null;
            }
            result(false);
        }

        /// <summary>
        /// Starts loading the configured save, or a brand new game when no save is
        /// configured or the configured one is missing.
        /// </summary>
        private bool LoadWorld()
        {
            var autoSave = SRSingleton<GameContext>.Instance.AutoSaveDirector;

            if (!string.IsNullOrEmpty(Config.GameName))
            {
                var summary = FindSave(autoSave, Config.GameName);
                if (summary != null)
                {
                    ServerLog($"[AutoHost] Loading save '{summary.displayName}' (day {summary.day})");
                    autoSave.BeginLoad(summary.name, summary.saveName,
                        () => ServerLog("[AutoHost] Save failed to load"));
                    return true;
                }

                if (!Config.CreateGameIfMissing)
                {
                    ServerLog($"[AutoHost] Save '{Config.GameName}' not found and CreateGameIfMissing is false, aborting");
                    return false;
                }

                ServerLog($"[AutoHost] Save '{Config.GameName}' not found, creating a new game instead");
            }

            //no specific world asked for: continue where the server left off,
            //otherwise every restart silently abandons the previous world
            if (Config.LoadLatestSave)
            {
                var latest = FindLatestSave(autoSave);
                if (latest != null)
                {
                    ServerLog($"[AutoHost] Continuing most recent save '{latest.displayName}' "
                              + $"(day {latest.day}, saved {latest.saveTimestamp:yyyy-MM-dd HH:mm})");
                    autoSave.BeginLoad(latest.name, latest.saveName,
                        () => ServerLog("[AutoHost] Save failed to load"));
                    return true;
                }
                ServerLog("[AutoHost] No existing save found, creating the first world");
            }

            var mode = Config.ResolveGameMode();
            ServerLog($"[AutoHost] Creating new game '{Config.NewGameDisplayName}' ({mode})");
            autoSave.LoadNewGame(Config.NewGameDisplayName, Identifiable.Id.PINK_SLIME, mode,
                () => ServerLog("[AutoHost] New game failed to load"));
            return true;
        }

        /// <summary>
        /// Resolves a configured save name against the internal game name first and
        /// the display name second, picking the newest save of that game.
        /// </summary>
        private GameData.Summary FindSave(AutoSaveDirector autoSave, string wanted)
        {
            try
            {
                var games = autoSave.AvailableGamesByGameName();
                if (games == null) return null;

                foreach (var entry in games)
                {
                    var saves = entry.Value?.Where(s => s != null && !s.isInvalid).ToList();
                    if (saves == null || saves.Count == 0) continue;

                    bool match = string.Equals(entry.Key, wanted, StringComparison.OrdinalIgnoreCase);
                    if (!match)
                    {
                        match = saves.Any(s => string.Equals(s.displayName, wanted, StringComparison.OrdinalIgnoreCase));
                    }

                    if (match)
                    {
                        return saves.OrderByDescending(s => s.saveTimestamp).First();
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLog("[AutoHost] Could not enumerate saves\n" + ex);
            }
            return null;
        }

        /// <summary>
        /// The newest valid save across every world. The game's own
        /// GetSaveToContinue is preferred so the server picks the same world the
        /// Continue button would; scanning is only a fallback for when that is
        /// unavailable.
        /// </summary>
        private GameData.Summary FindLatestSave(AutoSaveDirector autoSave)
        {
            try
            {
                var continueSave = autoSave.GetSaveToContinue();
                if (continueSave != null && !continueSave.isInvalid)
                {
                    return continueSave;
                }
            }
            catch (Exception ex)
            {
                ServerLog("[AutoHost] GetSaveToContinue failed, scanning instead\n" + ex);
            }

            try
            {
                var games = autoSave.AvailableGamesByGameName();
                if (games == null) return null;

                GameData.Summary newest = null;
                foreach (var entry in games)
                {
                    if (entry.Value == null) continue;
                    foreach (var save in entry.Value)
                    {
                        if (save == null || save.isInvalid) continue;
                        if (newest == null || save.saveTimestamp > newest.saveTimestamp)
                        {
                            newest = save;
                        }
                    }
                }
                return newest;
            }
            catch (Exception ex)
            {
                ServerLog("[AutoHost] Could not scan saves\n" + ex);
                return null;
            }
        }

        /// <summary>Waits for the game scene to report itself fully loaded.</summary>
        private IEnumerator WaitForWorld(Action<bool> result)
        {
            float deadline = Time.realtimeSinceStartup + Config.LoadTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (Globals.GameLoaded && SceneManager.GetActiveScene().buildIndex == GameScene)
                {
                    result(true);
                    yield break;
                }
                yield return null;
            }
            result(false);
        }

        /// <summary>
        /// Drops the host character straight down, out of sight. Straight down
        /// rather than far away on purpose: the host keeps the same horizontal
        /// position, so it stays inside the same streaming region and carries on
        /// loading and arbitrating the ranch.
        /// </summary>
        private void ParkHostPlayer()
        {
            if (!Config.HidePlayer) return;

            try
            {
                var player = SRSingleton<SceneContext>.Instance.Player;
                if (player == null)
                {
                    ServerLog("[AutoHost] No player to park");
                    return;
                }

                m_ParkPosition = player.transform.position + Vector3.down * Mathf.Abs(Config.ParkDepth);
                player.transform.position = m_ParkPosition;
                IsParked = true;

                ServerLog($"[AutoHost] Host character parked {Config.ParkDepth}m below the surface "
                          + "and made invulnerable");
            }
            catch (Exception ex)
            {
                ServerLog("[AutoHost] Could not park the host character\n" + ex);
            }
        }

        /// <summary>
        /// Holds the host in place. Without this it falls, drifts or gets pushed,
        /// and the server player wanders off into the world it is supposed to be
        /// hidden from.
        /// </summary>
        private void HoldHostParked()
        {
            if (!IsParked) return;

            try
            {
                var player = SRSingleton<SceneContext>.Instance.Player;
                if (player == null) return;

                if (player.transform.position != m_ParkPosition)
                {
                    player.transform.position = m_ParkPosition;
                }

                //nothing should be able to kill it, but a world that finds a way
                //would take the whole server down with it
                var state = SRSingleton<SceneContext>.Instance.PlayerState;
                if (state != null)
                {
                    if (state.GetCurrHealth() < state.GetMaxHealth()) state.SetHealth(state.GetMaxHealth());
                    if (state.GetCurrEnergy() < state.GetMaxEnergy()) state.SetEnergy(state.GetMaxEnergy());
                }
            }
            catch
            {
                //a transient null during a scene change is not worth logging every frame
            }
        }

        /// <summary>Writes the friend code to stdout, the log and a file operators can read.</summary>
        private void PublishServerCode()
        {
            string code = Globals.ServerCode;

            ServerLog("=====================================");
            ServerLog(" SRMP server is up");
            ServerLog(" FRIEND CODE : " + code);
            ServerLog(" world       : " + Globals.CurrentGameName);
            ServerLog("=====================================");

            if (string.IsNullOrEmpty(Config.ServerCodeFile)) return;

            try
            {
                string path = Path.Combine(SRMP.ModDataPath, Config.ServerCodeFile);
                File.WriteAllText(path, code);
                ServerLog("[AutoHost] Friend code written to " + path);
            }
            catch (Exception ex)
            {
                ServerLog("[AutoHost] Could not write friend code file\n" + ex);
            }
        }

        /// <summary>Absolute path of the file that requests a clean shutdown.</summary>
        public string ShutdownRequestPath
        {
            get { return Path.Combine(SRMP.ModDataPath, Config.ShutdownRequestFile); }
        }

        /// <summary>
        /// Saves the world, tells everyone why, then quits. A container stop
        /// cannot do this from outside: Unity will not flush a save in response
        /// to a signal, so the request has to be handled in-process.
        /// </summary>
        private IEnumerator ShutdownSequence()
        {
            ServerLog("[AutoHost] Shutdown requested, saving world...");

            try
            {
                new PacketPlayerChat { message = "Server is shutting down, saving..." }
                    .SendToAll(NetDeliveryMethod.ReliableOrdered);
            }
            catch (Exception ex)
            {
                ServerLog("[AutoHost] Could not announce shutdown\n" + ex);
            }

            bool saved = false;
            try
            {
                SRSingleton<GameContext>.Instance.AutoSaveDirector.SaveAllNow();
                saved = true;
            }
            catch (Exception ex)
            {
                ServerLog("[AutoHost] Save failed during shutdown\n" + ex);
            }

            //give the save and the outgoing chat packet a moment to flush
            yield return new WaitForSeconds(saved ? 3f : 1f);

            if (saved) ServerLog("[AutoHost] World saved");

            try
            {
                if (File.Exists(ShutdownRequestPath)) File.Delete(ShutdownRequestPath);
            }
            catch { /* the container is going away anyway */ }

            //close the lobby before quitting: members get a membership-closed
            //event and return to the menu, instead of waiting out a timeout in a
            //world that is no longer being hosted
            try
            {
                EpicApplication.Instance.Lobby.DestroyLobby();
            }
            catch (Exception ex)
            {
                ServerLog("[AutoHost] Could not close the lobby cleanly\n" + ex);
            }

            //give the close a moment to reach everyone before the process dies
            yield return new WaitForSeconds(2f);

            ServerLog("[AutoHost] Closing lobby and quitting");

            //SRMP.OnDestroy and EpicApplication.OnApplicationQuit tear the lobby
            //and the EOS platform down as the application exits
            Application.Quit();
        }

        private void Update()
        {
            if (!IsHosting) return;

            HoldHostParked();

            //poll rather than use a FileSystemWatcher: this has to work across a
            //bind mount, where watcher events are not reliably delivered
            if (!m_ShuttingDown
                && Time.realtimeSinceStartup - m_LastShutdownCheck > 1f)
            {
                m_LastShutdownCheck = Time.realtimeSinceStartup;
                try
                {
                    if (!string.IsNullOrEmpty(Config.ShutdownRequestFile)
                        && File.Exists(ShutdownRequestPath))
                    {
                        m_ShuttingDown = true;
                        StartCoroutine(ShutdownSequence());
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ServerLog("[AutoHost] Could not check for shutdown request\n" + ex);
                }
            }

            if (m_ShuttingDown) return;

            if (Config.StatusIntervalSeconds > 0f
                && Time.realtimeSinceStartup - m_LastStatus > Config.StatusIntervalSeconds)
            {
                m_LastStatus = Time.realtimeSinceStartup;
                var names = Globals.Players.Values
                    .Where(p => p != null && p.ID != Globals.LocalID)
                    .Select(p => p.Username)
                    .ToList();
                ServerLog($"[AutoHost] code {Globals.ServerCode} | {names.Count} player(s) online"
                          + (names.Count > 0 ? ": " + string.Join(", ", names.ToArray()) : ""));
            }

            if (Config.AutoSaveIntervalSeconds > 0f
                && Time.realtimeSinceStartup - m_LastSave > Config.AutoSaveIntervalSeconds)
            {
                m_LastSave = Time.realtimeSinceStartup;
                try
                {
                    SRSingleton<GameContext>.Instance.AutoSaveDirector.SaveAllNow();
                    ServerLog("[AutoHost] World saved");
                }
                catch (Exception ex)
                {
                    ServerLog("[AutoHost] Save failed\n" + ex);
                }
            }
        }

        /// <summary>
        /// Logs to the SRMP log and to stdout, so a headless wrapper can follow the
        /// server without parsing the Unity player log.
        /// </summary>
        public static void ServerLog(string message)
        {
            SRMP.Log(message);
            try
            {
                Console.WriteLine("[SRMP] " + message);
            }
            catch { /* no console attached; the SRMP log still has it */ }
        }
    }
}
