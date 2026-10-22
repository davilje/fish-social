using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop
{
    public sealed class PanelRouter : MonoBehaviour
    {
        readonly Dictionary<ShellPanelId, GameObject> _panels = new Dictionary<ShellPanelId, GameObject>();
        public ShellPanelId Current { get; private set; } = ShellPanelId.Home;
        public event Action<ShellPanelId> PanelChanged;
        int navigationVersion;
        bool transitioning;

        public void Register(ShellPanelId id, GameObject panel)
        {
            _panels[id] = panel;
            panel.SetActive(id == ShellPanelId.Home);
        }

        public void Show(ShellPanelId id)
        {
            // Even selecting the same tab or returning home must invalidate the old flip.
            CozyMotion.CancelPageTransition();
            transitioning = false;
            var version = ++navigationVersion;
            GameObject previous = null;
            _panels.TryGetValue(Current, out previous);
            var changed = Current != id && previous != null && previous.activeSelf;
            Current = id;
            GameObject next = null;
            _panels.TryGetValue(id, out next);
            // Listeners start load coroutines immediately. The slot must already be active.
            if (next != null && !next.activeSelf)
                next.SetActive(true);
            PanelChanged?.Invoke(id);
            // A route listener may synchronously redirect (e.g. login/permission guard).
            if (version != navigationVersion) return;
            if (!changed || next == null)
            {
                foreach (var kv in _panels)
                    if (kv.Value != null) kv.Value.SetActive(kv.Key == id);
                return;
            }
            transitioning = true;
            CozyMotion.Flip(previous, next, () =>
            {
                if (version != navigationVersion || Current != id) return;
                transitioning = false;
                foreach (var kv in _panels)
                    if (kv.Key != id && kv.Value != null)
                        kv.Value.SetActive(false);
            });
        }

        void OnDisable()
        {
            ++navigationVersion;
            if (transitioning) CozyMotion.CancelPageTransition();
            transitioning = false;
        }
    }
}
