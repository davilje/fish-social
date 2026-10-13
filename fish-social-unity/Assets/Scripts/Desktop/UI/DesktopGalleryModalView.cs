using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    public sealed class DesktopGalleryModalView : MonoBehaviour
    {
        bool _uiReady;
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        Text _status;
        Text _detail;
        Transform _grid;
        FishCodexEntryDto[] _entries = new FishCodexEntryDto[0];
        string _selectedId;

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond)
        {
            _api = api;
            _pond = pond;
            DesktopModalUi.StyleTree(transform);
            _status = DesktopModalUi.FindComponent<Text>(transform, "Status");
            _detail = DesktopModalUi.FindComponent<Text>(transform, "Detail");
            _grid = DesktopModalUi.FindChild(transform, "Grid/Species");
            DesktopModalUi.BindButton(transform, "Retry", () => StartCoroutine(Load()));
            _uiReady = _status != null && _detail != null && _grid != null &&
                DesktopModalUi.FindComponent<Button>(transform, "Retry") != null;
            if (!_uiReady)
                Debug.LogError("[DesktopUI] PanelGallery prefab is missing required controls.", this);
        }

        public void OnOpened()
        {
            if (!_uiReady)
                return;
            if (_pond != null)
                _pond.CodexUnlocked += OnUnlocked;
            StartCoroutine(Load());
        }

        public void OnClosed()
        {
            if (_pond != null)
                _pond.CodexUnlocked -= OnUnlocked;
        }

        void OnDestroy()
        {
            if (_pond != null)
                _pond.CodexUnlocked -= OnUnlocked;
        }

        void OnUnlocked(CodexUnlockDto unlock)
        {
            if (unlock != null)
                _status.text = "新解锁：" + unlock.speciesName;
            StartCoroutine(Load());
        }

        IEnumerator Load()
        {
            if (_api == null || !_api.CanUse)
            {
                _status.text = "没有玩家身份，无法加载图鉴。";
                _detail.text = _status.text;
                yield break;
            }
            _status.text = "正在加载图鉴…";
            var ok = false;
            string error = null;
            yield return _api.GetCodex((success, entries, message) =>
            {
                ok = success;
                _entries = entries ?? new FishCodexEntryDto[0];
                error = message;
            });
            if (!ok)
            {
                _status.text = error ?? "图鉴加载失败。";
                _detail.text = _status.text + "\n点击重试。";
                yield break;
            }
            var unlocked = 0;
            for (var i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].totalCaught > 0)
                    unlocked++;
            }
            var catalog = DesktopGameData.Species;
            if (catalog == null || catalog.Length == 0)
            {
                _status.text = "未加载 fish_species 数值表。";
                _detail.text = _status.text;
                yield break;
            }
            _status.text = "已解锁 " + unlocked + " / " + catalog.Length;
            RenderGrid();
        }

        void RenderGrid()
        {
            if (_grid == null)
                return;
            DesktopModalUi.Clear(_grid);
            var catalog = DesktopGameData.Species;
            if (catalog == null)
                return;
            for (var i = 0; i < catalog.Length; i++)
            {
                var def = catalog[i];
                if (def == null || string.IsNullOrEmpty(def.speciesId))
                    continue;
                var entry = FindEntry(def.speciesId);
                var unlocked = entry != null && entry.totalCaught > 0;
                var slot = DesktopUiPrefabFactory.Instantiate("GallerySpeciesSlot", _grid);
                if (slot == null)
                    continue;
                slot.name = def.speciesId;
                var label = unlocked ? DesktopGameData.SpeciesName(def.speciesId) : "？？？";
                var text = DesktopUiPrefabFactory.Child(slot, "Label");
                var labelText = text != null ? text.GetComponent<Text>() : null;
                if (labelText != null)
                    labelText.text = label;
                DesktopModalUi.StyleTree(slot.transform);
                var captured = def.speciesId;
                var button = slot.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => Select(captured));
                    var visual = button.GetComponent<CozyButtonVisual>();
                    if (visual != null)
                        visual.SetSelected(def.speciesId == _selectedId);
                }
            }
        }

        FishCodexEntryDto FindEntry(string speciesId)
        {
            if (_entries == null)
                return null;
            for (var i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].speciesId == speciesId)
                    return _entries[i];
            }
            return null;
        }

        void Select(string speciesId)
        {
            _selectedId = speciesId;
            var species = DesktopFishCatalog.GetSpecies(speciesId);
            var entry = FindEntry(speciesId);
            var unlocked = entry != null && entry.totalCaught > 0;
            if (species == null)
            {
                _detail.text = "未知鱼种。";
                return;
            }
            if (!unlocked)
            {
                _detail.text = "未解锁\n捕获后显示食性、钓组、体型与推荐鱼饵。";
                RenderGrid();
                return;
            }
            var first = entry.firstCaughtAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(entry.firstCaughtAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "—";
            _detail.text = DesktopFishCatalog.FormatCodexProfile(species) +
                           "\n累计捕获：" + entry.totalCaught +
                           "\n最大体型：" + entry.maxSizeM.ToString("0.00") + "m" +
                           "\n首次捕获：" + first;
            RenderGrid();
        }
    }
}
