using System;
using System.Collections.Generic;
using FishSocial.Desktop.Auth;
using UnityEngine;

namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Own-cat visual state machine. Overlay consumes ToWire(Current); it must
    /// not infer a second state machine from fishingPhase.
    /// </summary>
    public sealed class PetStateController : MonoBehaviour
    {
        readonly List<IPetRenderer> _renderers = new List<IPetRenderer>();
        bool _dragging;

        public PetVisualState Current { get; private set; } = PetVisualState.Offline;
        public event Action<PetVisualState> StateChanged;

        public void AddRenderer(IPetRenderer renderer)
        {
            if (renderer == null || _renderers.Contains(renderer))
                return;
            _renderers.Add(renderer);
            renderer.Apply(Current);
        }

        public void SetDragging(bool dragging)
        {
            if (_dragging == dragging)
                return;
            _dragging = dragging;
            RefreshFromApp();
        }

        public void RefreshFromApp()
        {
            var bootstrap = DesktopAppBootstrap.Instance;
            Refresh(bootstrap != null ? bootstrap.SteamAuth : null,
                bootstrap != null ? bootstrap.PondSession : null);
        }

        public void Refresh(SteamAuthController steam, SocialPondSessionController pond)
        {
            var next = Resolve(steam, pond, _dragging);
            if (next == Current)
                return;

            Current = next;
            ApplyRenderers();
            StateChanged?.Invoke(Current);
        }

        void ApplyRenderers()
        {
            for (var i = 0; i < _renderers.Count; i++)
                _renderers[i].Apply(Current);
        }

        public static PetVisualState Resolve(
            SteamAuthController steam,
            SocialPondSessionController pond,
            bool dragging)
        {
            if (dragging)
                return PetVisualState.Dragging;

            var authenticated = steam != null && steam.IsAuthenticated;
            if (!authenticated)
                return PetVisualState.Offline;

            if (pond == null || pond.State == SocialSocketState.Failed)
                return PetVisualState.Offline;

            if (pond.State != SocialSocketState.Connected)
                return PetVisualState.Idle;

            var phase = pond.CurrentPhase ?? string.Empty;
            return FromFishingPhase(phase, pond.HasPendingCatch);
        }

        public static PetVisualState FromFishingPhase(string phase)
        {
            return FromFishingPhase(phase, false);
        }

        public static PetVisualState FromFishingPhase(string phase, bool hasPendingCatch)
        {
            switch (phase)
            {
                case "hooked":
                    return PetVisualState.Hooked;
                case "resolving":
                case "stopping":
                    return PetVisualState.Reel;
                case "seated":
                    if (hasPendingCatch)
                        return PetVisualState.Catching;
                    return PetVisualState.Sit;
                case "groundbaiting":
                    return PetVisualState.Sit;
                case "baiting":
                case "casting":
                    return PetVisualState.Cast;
                case "waiting":
                    return PetVisualState.Fishing;
                default:
                    return PetVisualState.Idle;
            }
        }

        public static string ToWire(PetVisualState state)
        {
            switch (state)
            {
                case PetVisualState.Sit: return "sit";
                case PetVisualState.Cast: return "cast";
                case PetVisualState.Fishing: return "fishing";
                case PetVisualState.Hooked: return "hooked";
                case PetVisualState.Reel: return "reel";
                case PetVisualState.Catching: return "catch";
                case PetVisualState.Dragging: return "dragging";
                case PetVisualState.Offline: return "offline";
                default: return "idle";
            }
        }

        public static string ToChinese(PetVisualState state)
        {
            switch (state)
            {
                case PetVisualState.Sit: return "坐下";
                case PetVisualState.Cast: return "抛竿";
                case PetVisualState.Fishing: return "钓鱼";
                case PetVisualState.Hooked: return "咬钩";
                case PetVisualState.Reel: return "收杆";
                case PetVisualState.Catching: return "钓到鱼";
                case PetVisualState.Dragging: return "拖动";
                case PetVisualState.Offline: return "离线";
                default: return "待机";
            }
        }
    }
}
