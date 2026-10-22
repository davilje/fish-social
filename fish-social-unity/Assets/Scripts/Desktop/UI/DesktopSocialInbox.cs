using System;
using System.Collections.Generic;

namespace FishSocial.Desktop
{
    public enum SocialSection { Friends, Requests, DirectMessages, PondChat, Notifications }
    public enum SocialNoticeKind { FriendRequest, DirectMessage, PondInvite, System }

    /// <summary>Account-scoped in-session inbox. Reading a notice never accepts a request or sends a message.</summary>
    public sealed class DesktopSocialInbox
    {
        public sealed class Entry
        {
            public string Id, Title, Body, PlayerId, Nickname;
            public SocialNoticeKind Kind;
            public long CreatedAt;
            public bool Read;
        }
        readonly List<Entry> entries = new List<Entry>();
        readonly HashSet<string> ids = new HashSet<string>();
        public IReadOnlyList<Entry> Entries => entries;
        public int UnreadCount { get { var n=0; foreach(var entry in entries) if(!entry.Read)n++; return n; } }
        public event Action Changed;
        public void Add(Entry entry)
        {
            if(entry==null || string.IsNullOrEmpty(entry.Id) || !ids.Add(entry.Id)) return;
            entries.Insert(0,entry);
            if(entries.Count>100) { ids.Remove(entries[entries.Count-1].Id); entries.RemoveAt(entries.Count-1); }
            Changed?.Invoke();
        }
        public void MarkRead(string id)
        { foreach(var entry in entries) if(entry.Id==id && !entry.Read){entry.Read=true; Changed?.Invoke();break;} }
        public void ReadConversation(string playerId)
        { var changed=false; foreach(var entry in entries)if(entry.Kind==SocialNoticeKind.DirectMessage && entry.PlayerId==playerId && !entry.Read){entry.Read=true;changed=true;} if(changed)Changed?.Invoke(); }
        public void MarkAllRead()
        { foreach(var entry in entries)entry.Read=true;Changed?.Invoke(); }
        public void Clear() {entries.Clear();ids.Clear();Changed?.Invoke();}
        public static SocialSection Destination(SocialNoticeKind kind)
        { return kind==SocialNoticeKind.FriendRequest?SocialSection.Requests:kind==SocialNoticeKind.DirectMessage?SocialSection.DirectMessages:kind==SocialNoticeKind.PondInvite?SocialSection.PondChat:SocialSection.Notifications; }
    }
}
