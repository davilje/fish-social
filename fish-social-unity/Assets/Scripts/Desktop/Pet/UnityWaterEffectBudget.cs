using System;
using System.Collections.Generic;

namespace FishSocial.Desktop.Pet
{
    /// <summary>Platform-independent admission for Unity's single 24-image / 8-group pool.</summary>
    public sealed class UnityWaterEffectBudget
    {
        public sealed class Group
        {
            public string ActorId;
            public bool Self;
            public int Instances, Priority;
            public Action Recycle;
        }
        readonly List<Group> groups = new List<Group>(8);
        public int Groups => groups.Count;
        public int Instances { get { var n = 0; foreach (var g in groups) n += g.Instances; return n; } }
        int Count(string actor) { var n = 0; foreach (var g in groups) if (g.ActorId == actor) n++; return n; }
        bool Full(string actor, bool self, int count)
        {
            var otherGroups = 0; var otherInstances = 0;
            foreach (var g in groups) if (!g.Self) { otherGroups++; otherInstances += g.Instances; }
            return Groups >= 8 || Instances + count > 24 || Count(actor) >= 2 ||
                !self && (otherGroups >= 6 || otherInstances + count > 12);
        }
        public Group Admit(string actor, bool self, int count, int priority, Action recycle)
        {
            if (string.IsNullOrEmpty(actor) || count < 1 || count > 12) return null;
            while (Full(actor, self, count))
            {
                Group victim = null;
                foreach (var candidate in groups)
                {
                    if (Count(actor) >= 2 && candidate.ActorId != actor || candidate.Self && !self || candidate.Priority > priority) continue;
                    if (victim == null || candidate.Priority < victim.Priority) victim = candidate;
                }
                if (victim == null) return null;
                groups.Remove(victim); victim.Recycle?.Invoke();
            }
            var admitted = new Group { ActorId = actor, Self = self, Instances = count, Priority = priority, Recycle = recycle };
            groups.Add(admitted); return admitted;
        }
        public void Release(Group group) { if (group != null) groups.Remove(group); }
        public static bool CanRender(FishingPresentationEnvelope envelope, bool hasContact, string quality, bool self, string cue)
        {
            if (envelope == null || envelope.owner != "unity" || !hasContact || quality == "off") return false;
            return quality != "reduced" || self && (cue == "float.contact" || cue == "retrieve.contact" || cue == "bite.onset" || cue == "fish.surface" || cue == "fish.slap");
        }
        public static int Variant(string key) { uint h = 2166136261; foreach (var c in key ?? "") h = (h ^ c) * 16777619; return (int)(h & 1); }
    }
}
