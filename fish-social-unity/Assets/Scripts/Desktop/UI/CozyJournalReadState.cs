using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>Local read receipts, isolated by authenticated account; never alters catch data.</summary>
    public static class CozyJournalReadState
    {
        [Serializable] sealed class Saved { public List<string> ids = new List<string>(); }
        static readonly Dictionary<string, HashSet<string>> Accounts = new Dictionary<string, HashSet<string>>();

        static string Key(string playerId)
        {
            using (var hash = SHA256.Create())
                return "fishsocial.journal.read." + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(playerId))).Replace("-", "");
        }

        static HashSet<string> Reads(string playerId)
        {
            if (Accounts.TryGetValue(playerId, out var reads)) return reads;
            var json = PlayerPrefs.GetString(Key(playerId), string.Empty);
            Saved saved = null;
            try { if (!string.IsNullOrEmpty(json)) saved = JsonUtility.FromJson<Saved>(json); }
            catch (ArgumentException) { }
            reads = new HashSet<string>(saved?.ids ?? new List<string>(), StringComparer.Ordinal);
            Accounts[playerId] = reads;
            return reads;
        }

        public static bool IsUnread(string playerId, string catchId)
        {
            return !string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(catchId) && !Reads(playerId).Contains(catchId);
        }

        public static bool MarkRead(string playerId, string catchId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(catchId)) return false;
            var reads = Reads(playerId);
            if (!reads.Add(catchId)) return false;
            PlayerPrefs.SetString(Key(playerId), JsonUtility.ToJson(new Saved { ids = new List<string>(reads) }));
            PlayerPrefs.Save();
            return true;
        }

        public static void ClearAccount(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            Accounts.Remove(playerId);
            PlayerPrefs.DeleteKey(Key(playerId));
            PlayerPrefs.Save();
        }
    }
}
