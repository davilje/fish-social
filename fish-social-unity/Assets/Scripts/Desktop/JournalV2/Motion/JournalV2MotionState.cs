using System;
using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>V2 owns its motion state independently of the retired presentation layer.</summary>
    public static class JournalV2MotionState
    {
        public static bool Reduced { get; private set; }
        public static bool Paused { get; private set; }
        public static event Action Changed;
        public static void SetReduced(bool value) { if (Reduced == value) return; Reduced = value; Changed?.Invoke(); }
        public static void SetPaused(bool value) { if (Paused == value) return; Paused = value; Changed?.Invoke(); }
        public static bool Immediate => Reduced || Paused;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Reduced = false; Paused = false; Changed = null; }
    }
}
