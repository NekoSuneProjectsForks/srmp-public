using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SRMultiplayer.Server
{
    /// <summary>
    /// Per-world ban list, stored next to the save it belongs to.
    ///
    /// Bans are keyed on the Epic Online Services ProductUserId, not the display
    /// name: the server observes that id itself during the connection handshake,
    /// so a player cannot change it by renaming themselves or by editing their
    /// own files. The username is kept purely so a human can read the list and
    /// type /unban with a name.
    ///
    /// Being on this list only bars the player from this world. It is a list held
    /// by one server about its own world, so it cannot and does not stop anyone
    /// joining other servers or hosting their own game.
    /// </summary>
    internal static class BanList
    {
        public class BanEntry
        {
            /// <summary>EOS ProductUserId. The identity that actually matters.</summary>
            public string Id;

            /// <summary>Name at the time of the ban, for display and /unban.</summary>
            public string Username;

            public string Reason;
            public string BannedBy;
            public string BannedAt;
        }

        private static readonly List<BanEntry> m_Entries = new List<BanEntry>();
        private static string m_Path;

        public static IEnumerable<BanEntry> Entries { get { return m_Entries; } }
        public static int Count { get { return m_Entries.Count; } }

        /// <summary>Names on the list, for display and tab completion.</summary>
        public static List<string> Names
        {
            get { return m_Entries.Select(e => e.Username ?? e.Id).ToList(); }
        }

        /// <summary>
        /// Points the list at a world and loads it. Called when hosting starts,
        /// so switching worlds switches ban lists rather than carrying them over.
        /// </summary>
        public static void LoadForWorld(string gameName)
        {
            m_Entries.Clear();

            try
            {
                var dir = Path.Combine(SRMP.ModDataPath, gameName ?? "unknown");
                Directory.CreateDirectory(dir);
                m_Path = Path.Combine(dir, "bans.json");

                if (!File.Exists(m_Path))
                {
                    SRMP.Log($"[Bans] No ban list yet for '{gameName}'");
                    return;
                }

                var loaded = JsonConvert.DeserializeObject<List<BanEntry>>(File.ReadAllText(m_Path));
                if (loaded != null) m_Entries.AddRange(loaded.Where(e => e != null && !string.IsNullOrEmpty(e.Id)));

                SRMP.Log($"[Bans] Loaded {m_Entries.Count} ban(s) from {m_Path}");
            }
            catch (Exception ex)
            {
                //a corrupt ban list must not stop the server from starting
                SRMP.Log($"[Bans] Could not load ban list: {ex}");
            }
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(m_Path)) return;

            try
            {
                File.WriteAllText(m_Path, JsonConvert.SerializeObject(m_Entries, Formatting.Indented));
            }
            catch (Exception ex)
            {
                SRMP.Log($"[Bans] Could not save ban list: {ex}");
            }
        }

        public static BanEntry Find(string productUserId)
        {
            if (string.IsNullOrEmpty(productUserId)) return null;
            return m_Entries.FirstOrDefault(e =>
                string.Equals(e.Id, productUserId, StringComparison.Ordinal));
        }

        public static bool IsBanned(string productUserId)
        {
            return Find(productUserId) != null;
        }

        /// <summary>Adds a ban, replacing any existing entry for the same identity.</summary>
        public static void Add(string productUserId, string username, string reason, string bannedBy)
        {
            if (string.IsNullOrEmpty(productUserId)) return;

            m_Entries.RemoveAll(e => string.Equals(e.Id, productUserId, StringComparison.Ordinal));
            m_Entries.Add(new BanEntry()
            {
                Id = productUserId,
                Username = username,
                Reason = string.IsNullOrEmpty(reason) ? "No reason given" : reason,
                BannedBy = bannedBy,
                BannedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
            });

            Save();
            SRMP.Log($"[Bans] Banned {username} ({productUserId}): {reason}");
        }

        /// <summary>
        /// Removes a ban by name or by raw id. Name matching is how an operator
        /// will actually use this; the id is there for when two players shared a
        /// name or the name was never recorded.
        /// </summary>
        public static BanEntry Remove(string nameOrId)
        {
            if (string.IsNullOrEmpty(nameOrId)) return null;

            var entry = m_Entries.FirstOrDefault(e =>
                             string.Equals(e.Username, nameOrId, StringComparison.OrdinalIgnoreCase))
                        ?? m_Entries.FirstOrDefault(e =>
                             string.Equals(e.Id, nameOrId, StringComparison.Ordinal));

            if (entry == null) return null;

            m_Entries.Remove(entry);
            Save();
            SRMP.Log($"[Bans] Unbanned {entry.Username} ({entry.Id})");
            return entry;
        }
    }
}
