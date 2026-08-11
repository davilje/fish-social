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
            Current = id;
            foreach (var kv in _panels)
                kv.Value.SetActive(kv.Key == id);
            PanelChanged?.Invoke(id);
        }
    }
}
