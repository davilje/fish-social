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
            // The index button stays bound while that page is already showing.
            // A second click must not close and reopen it, or the page reloads.
            if (Current == id)
                return;
            CozyMotion.CancelPageTransition();
            transitioning = false;
            var version = ++navigationVersion;
            GameObject previous = null;
            _panels.TryGetValue(Current, out previous);
            var changed = Current != id && previous != null && previous.activeSelf;
            Current = id;
            GameObject next = null;
            _panels.TryGetValue(id, out next);
            // OnOpened rebuilds while this call is still on the stack. Conceal first, or a later
            // sibling paints the settled page and the transition then plays that same page again.
            var animate = changed && next != null && !CozyMotion.Reduced && !CozyMotion.Paused;
            if (animate)
                CozyMotion.ConcealIncoming(next);
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
