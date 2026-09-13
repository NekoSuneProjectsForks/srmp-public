using SRMultiplayer.EpicSDK;
using SRMultiplayer.Networking;
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

        private float m_LastStatus;
        private float m_LastSave;

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

            ServerLog("=====================================");
            ServerLog(" SRMP headless host starting");
            ServerLog(" username : " + Config.Username);
            ServerLog(" save     : " + (string.IsNullOrEmpty(Config.GameName) ? "<new game>" : Config.GameName));
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

        private void Update()
        {
            if (!IsHosting) return;

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
