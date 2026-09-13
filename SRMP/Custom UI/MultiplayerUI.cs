using UnityEngine;
using System.Collections;
using SRMultiplayer;
using UnityEngine.SceneManagement;
using System;
using System.Text.RegularExpressions;
using SRMultiplayer.EpicSDK;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;

public class MultiplayerUI : SRSingleton<MultiplayerUI>
{
    private Rect windowRect = new Rect(Screen.width - 300 - 20, 20, 300, 500);
    private Vector2 playersScroll = Vector2.zero;
    private string ipaddress = "localhost";
    private string port = "16500";
    private string servercode = "";

    //use internal name with getter and setter to allow the menu panel to remember the users last choice
    private int _menuOpen;
    private int menuOpen  //menu open hold the current menu state (0 colapsed, 1 minimized, 2 open)
    {
        get
        {
            return _menuOpen;
        }
        set
        {
            _menuOpen = value;

            //save users setting
            PlayerPrefs.SetInt("SRMP_Menu", menuOpen);
        }
    }


    private string username;
    private float lastCodeUse;
    private ConnectError error;
    private ConnectHelp help;
    private string errorMessage;

    public PacketKickClient kickData;

    public enum ConnectError
    {
        None,
        InvalidServerCode,
        ServerCodeTimeout,
        Kicked,
        Message
    }

    public enum ConnectHelp
    {
        None,
        ServerCode,
        Hosting,
    }

    /// <summary>
    /// Creation of gui with side panel is last display state
    /// </summary>
    public override void Awake()
    {
        base.Awake();

        //set default ui location width adapting numbers for smaller resolutions
        float width = 300;
        if (Screen.width / 4 < width) width = Screen.width / 4;
        windowRect = new Rect(Screen.width - width - 20, 20, width, 500);

        Globals.Username = PlayerPrefs.GetString("SRMP_Username", "");
        ipaddress = PlayerPrefs.GetString("SRMP_IP", "localhost");
        port = PlayerPrefs.GetString("SRMP_Port", "16500");

        menuOpen = PlayerPrefs.GetInt("SRMP_Menu", 2); ; //start panel open by default

        username = Globals.Username;
    }

    /// <summary>
    /// Update of panel display
    /// </summary>
    private void Update()
    {
        if (lastCodeUse > 0f)
        {
            var prevTime = lastCodeUse;
            lastCodeUse -= Time.deltaTime;
            if (prevTime > 0f && lastCodeUse <= 0f)
            {
                error = ConnectError.ServerCodeTimeout;
            }
        }


        //this sets presskey senarios
        if (Input.GetKeyDown(KeyCode.F4))
        {
            //use the f4 key to swap between collapsed and minimized
            menuOpen = menuOpen == 2 ? 0 : 2;
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            //use the f3 key to set minimized or maximized
            menuOpen = menuOpen < 2 ? 2 : 1;
        }
    }
    /// <summary>
    /// Handle draw event of the gui
    /// </summary>
    private void OnGUI()
    {
        //verify on a window that the menu can be drawn on
        if ((!Levels.isMainMenu() && !Globals.IsMultiplayer && Globals.PauseState != PauseState.Pause)) return;
        if (Globals.IsMultiplayer && Globals.PauseState != PauseState.Pause) return;

        //if yes draw the window for the given state
        if (SceneManager.GetActiveScene().buildIndex >= 2)
        {
            //check to make sure the panel is taking less than 25% of the screen if possible
            float width = 300;
            if(Screen.width/ 4 < width) width = Screen.width/4;
            windowRect.width = width;

            //check to see if the windows needs to move due to it being off the screen from size change
            //also prevent the user from dragging it off the screen
            if (windowRect.x + 20 + windowRect.width > Screen.width) windowRect.x = Screen.width - windowRect.width - 20;
            if (windowRect.y + 20 + windowRect.height > Screen.height) windowRect.y = (Screen.height - windowRect.height - 20) >= 20 ? (Screen.height - windowRect.height - 20) : 20;
            if (windowRect.x < 20) windowRect.x = 20;
            if (windowRect.y < 20) windowRect.y = 20;

            

            //drawn in the window
            switch (menuOpen)
            {
                case 0: //collapsed
                    windowRect.height = 50;
                    windowRect = GUILayout.Window(1, windowRect, ClosedWindow, "SRMP v" + Globals.Version);
                    break;
                case 1: //minimized
                    windowRect.height = 100;
                    windowRect = GUILayout.Window(1, windowRect, MiniWindow, "SRMP v" + Globals.Version);
                    break;
                case 2: //open
                    windowRect.height = 500;
                    windowRect = GUILayout.Window(1, windowRect, MultiplayerWindow, "SRMP v" + Globals.Version);
                    break;
            }


        }
    }
    /// <summary>
    /// display for the function keyps section of the gui
    /// </summary>
    private void FunctionKeys()
    {
        if (menuOpen != 0)
        {
            GUILayout.Label("Press Button or Key To Change Style");
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(menuOpen == 1 ? "F3 - Full" : "F3 - Mini"))
        {
            menuOpen = menuOpen == 1 ? 2 : 1;
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(menuOpen == 0 ? "F4 - Full" : "F4 - Colapsed"))
        {
            menuOpen = menuOpen == 0 ? 2 : 0;
        }
        GUILayout.EndHorizontal();
    }
    /// <summary>
    /// Sets the window display for collapsed
    /// Only activated if the id is the id for the window
    /// </summary>
    private void ClosedWindow(int id)
    {
        if (id != 1) return;
        //display f3 and f4 commands
        FunctionKeys();

        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }
    /// <summary>
    /// Sets the window display for summary mode
    /// Only activated if the id is the id for the window
    /// </summary>
    private void MiniWindow(int id)
    {
        if (id != 1) return;

        //display f3 and f4 commands
        FunctionKeys();

        //show username and current connection status
        GUIStyle standard = new GUIStyle(GUI.skin.label);
        GUIStyle red = new GUIStyle(GUI.skin.label);
        red.normal.textColor = Color.red;


        if (string.IsNullOrWhiteSpace(Globals.Username))
        {
            GUILayout.Label("Username: Not Set", red);
        }
        else
        {
            GUILayout.Label("Username: " + Globals.Username);

            if (Globals.IsServer)
            {
                GUILayout.Label("Status: Host");
            }
            else if (Globals.IsClient)
            {

                GUIStyle green = new GUIStyle(GUI.skin.label);
                green.normal.textColor = Color.green;
                GUILayout.Label("Status: Client", green);
            }
            else
            {
                bool canHost = true;

                GUILayout.BeginHorizontal();
                GUILayout.Label("Status: Disconnected", red);

                GUILayout.FlexibleSpace();
                if (canHost)
                {
                    //only show host if not main menu
                    if (!Levels.isMainMenu())
                    {
                        if (GUILayout.Button("Host"))
                        {
                            EpicApplication.Instance.Lobby.CreateLobby();
                            SaveSettings();
                        }
                    }
                }
                GUILayout.EndHorizontal();
                if (!canHost)
                {
                    GUILayout.Label("Invalid Port Settings", red);
                }
            }

        }
        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }
    /// <summary>
    /// Sets the window display for full display mode
    /// Only activated if the id is the id for the window
    /// </summary>
    private void MultiplayerWindow(int id)
    {
        if (id != 1) return;

        //display f3 and f4 commands
        FunctionKeys();

        //now display standard 
        if (string.IsNullOrWhiteSpace(Globals.Username))
        {
            UsernameGUI();
        }
        else
        {
            if (Globals.IsServer)
            {
                ServerGUI();
            }
            else if (Globals.IsClient)
            {
                ClientGUI();
            }
            else
            {
                if (kickData != null)
                {
                    ErrorGUI();
                }
                else if (help != ConnectHelp.None)
                {
                    HelpGUI();
                }
                else
                {
                    if (lastCodeUse > 0f)
                    {
                        GUILayout.Label("Trying to connect with server code...");
                    }
                    else if (Levels.isMainMenu())
                    {
                        ConnectGUI();
                    }
                    else
                    {
                        HostGUI();
                    }
                }
            }
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }

    /// <summary>
    /// Renders a player's round trip time, coloured by how playable it is.
    /// A host has no round trip to itself, so it is labelled rather than shown
    /// as a suspiciously perfect 0 ms.
    /// </summary>
    private static void PingLabel(NetworkPlayer player)
    {
        var previous = GUI.contentColor;

        if (player.IsLocal && Globals.IsServer)
        {
            GUI.contentColor = Color.grey;
            GUILayout.Label("host", GUILayout.Width(60));
        }
        else if (player.Ping <= 0)
        {
            GUI.contentColor = Color.grey;
            GUILayout.Label("-- ms", GUILayout.Width(60));
        }
        else
        {
            if (player.Ping < 80) GUI.contentColor = Color.green;
            else if (player.Ping < 200) GUI.contentColor = Color.yellow;
            else GUI.contentColor = new Color(1f, 0.4f, 0.4f);

            GUILayout.Label(player.Ping + " ms", GUILayout.Width(60));
        }

        GUI.contentColor = previous;
    }

    /// <summary>
    /// Display the active server info part of the gui
    /// </summary>
    private void ServerGUI()
    {
        GUILayout.Label("You are the server");
        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Server Code: " + Globals.ServerCode);
        if (GUILayout.Button("Copy"))
        {
            GUIUtility.systemCopyBuffer = Globals.ServerCode;
        }
        GUILayout.EndHorizontal();
        
        GUILayout.Label("Players");
        playersScroll = GUILayout.BeginScrollView(playersScroll, GUI.skin.box);
        foreach (var player in Globals.Players.Values)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(player.IsLocal ? player.Username + " (you)" : player.Username);
            if (player.IsVR)
            {
                GUI.contentColor = Color.cyan;
                GUILayout.Label("VR");
                GUI.contentColor = Color.white;
            }
            GUILayout.FlexibleSpace();
            PingLabel(player);
            if (!player.IsLocal && GUILayout.Button("Kick"))
            {
                NetworkServer.Instance.DisconnectKick(player);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }
    /// <summary>
    /// Display the client info part of the gui
    /// </summary>
    private void ClientGUI()
    {
        GUILayout.Label("You are a client");
        GUILayout.Space(20);

        GUILayout.Label("Players");
        playersScroll = GUILayout.BeginScrollView(playersScroll, GUI.skin.box);
        foreach (var player in Globals.Players.Values)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(player.IsLocal ? player.Username + " (you)" : player.Username);
            if (player.IsVR)
            {
                GUI.contentColor = Color.cyan;
                GUILayout.Label("VR");
                GUI.contentColor = Color.white;
            }
            GUILayout.FlexibleSpace();
            PingLabel(player);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    /// <summary>
    /// Display the connection information of the gui
    /// this section includes user information,
    /// how to and other imbedded sections for handling display
    /// </summary>
    private void ConnectGUI()
    {
        GUILayout.Label("Username: " + Globals.Username);
        if (GUILayout.Button("Change Username"))
        {
            Globals.Username = "";
            return;
        }

        GUILayout.Space(20);
        if (GUILayout.Button("How do I host a game?"))
        {
            help = ConnectHelp.Hosting;
        }

        GUILayout.Space(20);

        GUILayout.Label("Join with server code:");
        GUILayout.BeginHorizontal();
        servercode = GUILayout.TextField(servercode, 7, GUILayout.Width(120));
        servercode = servercode.Replace(" ", "").ToUpper();
        if (GUILayout.Button("Join"))
        {
            lastCodeUse = 5;
            EpicApplication.Instance.Lobby.JoinLobby(servercode);
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Display the hosting info part of the gui
    /// </summary>
    private void HostGUI()
    {
        GUILayout.Label("Username: " + Globals.Username);
        if (GUILayout.Button("Change Username"))
        {
            Globals.Username = "";
            return;
        }


        if (GUILayout.Button("Host"))
        {
            EpicApplication.Instance.Lobby.CreateLobby();
            SaveSettings();
        }

    }

    /// <summary>
    /// Display the Help info part of the gui with instructions for hosting
    /// </summary>
    private void HelpGUI()
    {
        switch (help)
        {
            case ConnectHelp.ServerCode:
                {

                }
                break;
            case ConnectHelp.Hosting:
                {
                    GUILayout.Label("You can host a game by loading any Singleplayer save.");
                    GUILayout.Label("When the game is loaded, pause it and the HostUI should appear.");
                    GUILayout.Label("(Please make a backup of your Singleplayer save to avoid data loss on crashes or error)");

                    if (GUILayout.Button("Okay"))
                    {
                        help = ConnectHelp.None;
                    }
                }
                break;
        }
    }
    /// <summary>
    /// Display the Error summaries in the gui
    /// </summary>
    private void ErrorGUI()
    {
        switch (kickData.reason)
        {
            case PacketKickClient.Reason.Kicked:
                GUILayout.Label("You got kicked from the game");

                if (GUILayout.Button("Okay"))
                {
                    kickData = null;
                }
                break;
            case PacketKickClient.Reason.VersionMismatch:
                GUILayout.Label("Your version is different from the server version!");
                GUILayout.Label($"Your Version: {Globals.Version}");
                GUILayout.Label($"Server Version: {kickData.data}");
                
                if (GUILayout.Button("Okay"))
                {
                    kickData = null;
                }
                break;
            case PacketKickClient.Reason.ModsMismatch:
                GUILayout.Label("The server has different mods installed!");
                GUILayout.Label((string)kickData.data);
                
                if (GUILayout.Button("Okay"))
                {
                    kickData = null;
                }
                break;
            case PacketKickClient.Reason.DLCMismatch:
                GUILayout.Label("The server has different DLCs installed!");
                GUILayout.Label((string)kickData.data);
                
                if (GUILayout.Button("Okay"))
                {
                    kickData = null;
                }
                break;
            case PacketKickClient.Reason.Custom:
                GUILayout.Label((string)kickData.data);
                
                if (GUILayout.Button("Okay"))
                {
                    kickData = null;
                }
                break;
        }
        
    }
    /// <summary>
    /// Display the Current user information 
    /// </summary>
    private void UsernameGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Username", GUILayout.Width(60));
        username = GUILayout.TextField(username, 30);
        GUILayout.EndHorizontal();

        if (string.IsNullOrWhiteSpace(username) || !new Regex(@"^[a-zA-Z_][\w]*$").IsMatch(username))
        {
            GUILayout.Label("Invalid username");
        }
        else
        {
            if (GUILayout.Button("Save Username"))
            {
                Globals.Username = username;
                SaveSettings();
                var auth = EpicApplication.Instance.Authentication;
                if (auth.IsLoggedIn)
                    auth.Logout();
                auth.Login(username);
            }
        }
    }
    /// <summary>
    /// Saves the gui settings for the user
    /// </summary>
    private void SaveSettings()
    {
        PlayerPrefs.SetString("SRMP_Username", Globals.Username);
        PlayerPrefs.SetString("SRMP_IP", ipaddress);
        PlayerPrefs.GetString("SRMP_Port", port);
    }
    /// <summary>
    /// Handles connection resonces display when connection is lost from the server
    /// </summary>
    public void ConnectResponse(ConnectError connectError, string message = "")
    {
        lastCodeUse = 0f;
        error = connectError;
        errorMessage = message;
    }
}
