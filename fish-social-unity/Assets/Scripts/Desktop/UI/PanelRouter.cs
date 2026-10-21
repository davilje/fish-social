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

        public void Register(ShellPanelId id, GameObject panel)
        {
            _panels[id] = panel;
            panel.SetActive(id == ShellPanelId.Home);
        }

        public void Show(ShellPanelId id)
        {
            GameObject previous = null;
            _panels.TryGetValue(Current, out previous);
            var changed = Current != id && previous != null && previous.activeSelf;
            Current = id;
            GameObject next = null;
            _panels.TryGetValue(id, out next);
            PanelChanged?.Invoke(id);
            if (!changed || next == null)
            {
                foreach (var kv in _panels)
                    kv.Value.SetActive(kv.Key == id);
                return;
            }
            CozyMotion.Flip(previous, next, () =>
            {
                foreach (var kv in _panels)
                    if (kv.Key != id && kv.Value != null)
                        kv.Value.SetActive(false);
            });
        }
    }
}
