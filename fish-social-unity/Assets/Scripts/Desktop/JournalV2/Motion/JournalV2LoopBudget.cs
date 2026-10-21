using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>Only the highest-priority visible owner runs a persistent loop. Finite feedback does not consume this budget.</summary>
    public static class JournalV2LoopBudget
    {
        struct Entry { public MonoBehaviour Owner; public int Priority; }
        static readonly List<Entry> entries = new List<Entry>();
        public static MonoBehaviour Owner
        {
            get
            {
                if (JournalV2MotionState.Immediate) return null;
                MonoBehaviour result = null; var priority = int.MinValue;
                for (var i = entries.Count-1; i >= 0; i--)
                {
                    var entry = entries[i];
                    if (entry.Owner == null) { entries.RemoveAt(i); continue; }
                    if (!entry.Owner.isActiveAndEnabled || !entry.Owner.gameObject.activeInHierarchy || entry.Priority <= priority) continue;
                    result = entry.Owner; priority = entry.Priority;
                }
                return result;
            }
        }
        public static void Claim(MonoBehaviour owner, int priority)
        { Release(owner); if (owner != null) entries.Add(new Entry { Owner = owner, Priority = priority }); }
        public static void Release(MonoBehaviour owner)
        { for (var i = entries.Count-1; i >= 0; i--) if (entries[i].Owner == null || entries[i].Owner == owner) entries.RemoveAt(i); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { entries.Clear(); }
    }
}
