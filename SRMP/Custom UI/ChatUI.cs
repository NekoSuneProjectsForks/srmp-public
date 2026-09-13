using System.Linq;
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using SRMultiplayer;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;

public class ChatUI : SRSingleton<ChatUI>
{
    private const int MaxMessages = 100;
    private bool openChat;
    private string message = "";
    private readonly List<ChatMessage> messages = new List<ChatMessage>();
    private Vector2 chatScroll;
    private readonly int maxWidth = 290;

    /// <summary>
    /// Create a chat message to be displayed.
    /// </summary>
    public class ChatMessage
    {
        public string Text;
        public float FadeTime;
        public DateTime Time;

        public ChatMessage(string msg)
        {
            Text = msg;
            FadeTime = 10f;
            Time = DateTime.Now;
        }
    }

    /// <summary>
    /// Handle chat input state.
    /// </summary>
    private void Update()
    {
        if (!Globals.IsMultiplayer)
        {
            openChat = false;
            message = "";
            return;
        }

        // Only Return-to-open is handled here. Return-to-send is handled in
        // OnGUI so one key-up cannot close the chat and then re-open it two
        // frames later via FocusChat().
        if (!openChat && Input.GetKeyUp(KeyCode.Return))
        {
            StartCoroutine(FocusChat());
        }

        if (openChat && Input.GetKeyUp(KeyCode.Escape))
        {
            openChat = false;
            message = "";
        }
    }

    /// <summary>
    /// Create the chat GUI.
    /// </summary>
    /// <summary>
    /// Commands offered while typing. Kept here rather than asked of the server
    /// so the list appears the instant '/' is typed, with no round trip.
    /// </summary>
    private static readonly string[] CommandHints =
    {
        "/help - list commands",
        "/tps - server tick rate",
        "/ping - your round trip time",
        "/list - who is online",
        "/home - teleport yourself to the ranch",
        "/tp <player> [dest|home] - operators only"
    };

    /// <summary>
    /// Shows matching commands as soon as the line starts with '/', so the
    /// commands are discoverable without knowing /help exists first.
    /// </summary>
    private static void DrawCommandHints(string current)
    {
        if (string.IsNullOrEmpty(current) || !current.StartsWith("/")) return;

        //match on the word typed so far, so the list narrows as you go
        string typed = current.Split(' ')[0];
        var matches = CommandHints
            .Where(h => h.StartsWith(typed, System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0) return;

        var previous = GUI.contentColor;
        GUI.contentColor = new Color(0.7f, 0.85f, 1f);
        foreach (var hint in matches)
        {
            GUILayout.Label(hint);
        }
        GUI.contentColor = previous;
    }

    /// <summary>
    /// Completes the last word from the names that make sense here: everyone
    /// online, plus any extra names the server offered (banned players, who are
    /// by definition not online and so cannot be derived locally).
    /// </summary>
    private static string CompleteName(string current)
    {
        if (string.IsNullOrEmpty(current)) return current;

        int split = current.LastIndexOf(' ');
        string prefix = split >= 0 ? current.Substring(0, split + 1) : "";
        string word = split >= 0 ? current.Substring(split + 1) : current;

        //a bare command is not a name; leave it to the hint list
        if (word.StartsWith("/") || word.Length == 0) return current;

        var candidates = Globals.Players.Values
            .Where(p => p != null && !string.IsNullOrEmpty(p.Username))
            .Select(p => p.Username)
            .Concat(Globals.SuggestedNames ?? new List<string>())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .Where(n => n.StartsWith(word, System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n)
            .ToList();

        if (candidates.Count == 0) return current;

        //cycle rather than stopping at the first, so duplicates are reachable
        int next = candidates.FindIndex(n =>
            string.Equals(n, word, System.StringComparison.OrdinalIgnoreCase)) + 1;
        if (next >= candidates.Count) next = 0;

        return prefix + candidates[next];
    }

    private void OnGUI()
    {
        if (!Globals.IsMultiplayer)
            return;

        Color originalColor = GUI.color;
        try
        {
            if (openChat)
            {
                GUILayout.BeginArea(new Rect(20, Screen.height / 2, maxWidth + 10, 300), GUI.skin.box);
                chatScroll = GUILayout.BeginScrollView(chatScroll);
                GUIStyle skin = new GUIStyle(GUI.skin.box)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };

                foreach (ChatMessage msg in messages)
                {
                    GUILayout.Label(WrapString(msg.Text, maxWidth), skin, GUILayout.MaxWidth(maxWidth));
                }
                GUILayout.EndScrollView();

                DrawCommandHints(message);

                GUI.SetNextControlName("ChatInput");
                message = GUILayout.TextField(message ?? "");
                GUILayout.EndArea();
                GUI.FocusControl("ChatInput");

                Event e = Event.current;

                //Tab completes the word under the cursor. Consumed on KeyDown so
                //the control does not also treat it as a focus change.
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
                {
                    message = CompleteName(message ?? "");
                    e.Use();
                }

                if (e.rawType == EventType.KeyUp && e.keyCode == KeyCode.Return)
                {
                    string outgoing = message ?? "";
                    openChat = false;

                    if (!string.IsNullOrWhiteSpace(outgoing))
                    {
                        if (Globals.IsServer)
                        {
                            string displayed = Globals.Username + ": " + outgoing;
                            AddChatMessage(displayed);
                            new PacketPlayerChat { message = displayed }.Send();
                        }
                        else if (Globals.IsClient)
                        {
                            new PacketPlayerChat { message = outgoing }.Send();
                        }
                    }

                    message = "";
                    e.Use();
                }
            }
            else
            {
                GUILayout.BeginArea(new Rect(20, Screen.height / 2, maxWidth + 10, 300));
                chatScroll = GUILayout.BeginScrollView(chatScroll);
                GUIStyle skin = new GUIStyle(GUI.skin.box)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };

                foreach (ChatMessage msg in messages)
                {
                    if (msg.FadeTime <= 0f)
                        continue;

                    msg.FadeTime -= Time.deltaTime;
                    Color c = originalColor;
                    c.a = Mathf.Clamp01(msg.FadeTime / 5f);
                    GUI.color = c;
                    GUILayout.Label(WrapString(msg.Text, maxWidth), skin, GUILayout.MaxWidth(maxWidth));
                    GUI.color = originalColor;
                }

                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
        }
        finally
        {
            // Do not leak chat fade alpha into other IMGUI windows.
            GUI.color = originalColor;
        }
    }

    /// <summary>
    /// Add a chat message to be displayed.
    /// </summary>
    public void AddChatMessage(string chatMessage)
    {
        // Historical builds could display empty remote chat entries. Never add
        // an empty/null packet to the visible history even if one reaches us.
        if (string.IsNullOrWhiteSpace(chatMessage))
            return;

        messages.Add(new ChatMessage(chatMessage));
        if (messages.Count > MaxMessages)
            messages.RemoveRange(0, messages.Count - MaxMessages);

        chatScroll = new Vector2(0, 100000);
    }

    public void Clear()
    {
        messages.Clear();
    }

    /// <summary>
    /// Open chat after the Return key event that requested it has completed.
    /// </summary>
    private IEnumerator FocusChat()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (!Globals.IsMultiplayer || openChat)
            yield break;

        openChat = true;
        GUI.FocusControl("ChatInput");
    }

    /// <summary>
    /// Wrap a message safely. The previous implementation used a fixed five
    /// element temporary array and could overflow on a long unbroken token.
    /// </summary>
    private string WrapString(string msg, int width)
    {
        if (string.IsNullOrEmpty(msg) || width <= 0)
            return msg ?? "";

        StringBuilder result = new StringBuilder(msg.Length + 16);
        string[] words = msg.Split(' ');
        int lineLength = 0;

        for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            string word = words[wordIndex] ?? "";

            while (word.Length > width)
            {
                if (lineLength > 0)
                {
                    result.Append('\n');
                    lineLength = 0;
                }

                result.Append(word.Substring(0, width));
                result.Append('\n');
                word = word.Substring(width);
            }

            int required = word.Length + (lineLength > 0 ? 1 : 0);
            if (lineLength > 0 && lineLength + required > width)
            {
                result.Append('\n');
                lineLength = 0;
            }
            else if (lineLength > 0)
            {
                result.Append(' ');
                lineLength++;
            }

            result.Append(word);
            lineLength += word.Length;
        }

        return result.ToString();
    }
}
