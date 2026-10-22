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
        bool _loading;
        public string SelectedSpeciesId => _selectedId;

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond)
        {
            _api = api;
            _pond = pond;
            DesktopModalUi.StyleTree(transform);
            _status = DesktopModalUi.FindComponent<Text>(transform, "Status");
            _detail = DesktopModalUi.FindComponent<Text>(transform, "Detail/Viewport/Body");
            if (_detail == null) _detail = DesktopModalUi.FindComponent<Text>(transform, "Detail");
            _grid = transform.Find("Grid/Viewport/Species") ?? DesktopModalUi.FindChild(transform, "Grid/Species");
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
            {
                _pond.CodexUnlocked -= OnUnlocked;
                _pond.CodexUnlocked += OnUnlocked;
            }
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
            if (_loading) yield break;
            if (_api == null || !_api.CanUse)
            {
                _status.text = "没有玩家身份，无法加载图鉴。";
                _detail.text = _status.text;
                yield break;
            }
            _loading = true;
            _status.text = "正在加载图鉴…";
            var ok = false;
            string error = null;
            yield return _api.GetCodex((success, entries, message) =>
            {
                ok = success;
                _entries = entries ?? new FishCodexEntryDto[0];
                error = message;
            });
            _loading = false;
            if (!ok)
            {
                _status.text = error ?? "图鉴加载失败。";
                _detail.text = _status.text + "\n点击重试。";
                yield break;
            }
            PresentEntries(_entries);
        }

        /// <summary>Render a received codex snapshot; also used by the Editor preview fixture without a network session.</summary>
        public void PresentEntries(FishCodexEntryDto[] entries)
        {
            if (!_uiReady) return;
            _entries = entries ?? new FishCodexEntryDto[0];
            var catalog = DesktopGameData.Species;
            if (catalog == null || catalog.Length == 0)
            {
                _status.text = "暂时没有鱼种资料。"; _detail.text = _status.text; return;
            }
            var unlocked = 0;
            foreach (var definition in catalog)
            {
                if (definition == null) continue;
                var entry = FindEntry(definition.speciesId);
                if (entry != null && entry.totalCaught > 0) unlocked++;
            }
            _status.text = "已解锁 " + unlocked + " / " + catalog.Length;
            if (string.IsNullOrEmpty(_selectedId) || DesktopFishCatalog.GetSpecies(_selectedId) == null)
            {
                _selectedId = null;
                foreach (var definition in catalog)
                    if (definition != null && FindEntry(definition.speciesId)?.totalCaught > 0) { _selectedId = definition.speciesId; break; }
                if (string.IsNullOrEmpty(_selectedId))
                    foreach (var definition in catalog)
                        if (definition != null) { _selectedId = definition.speciesId; break; }
            }
            RenderGrid();
            SelectSpecies(_selectedId);
        }

        void RenderGrid()
        {
            if (_grid == null)
                return;
            // Detach before deferred destruction so the next layout pass has exactly the new data set.
            for (var child = _grid.childCount - 1; child >= 0; child--)
            {
                var old = _grid.GetChild(child).gameObject;
                old.SetActive(false); old.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
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
                var art = DesktopModalUi.FindComponent<Image>(slot.transform, "FishArt");
                if (art != null)
                {
                    art.sprite = CozyItemArt.Fish(def.speciesId); art.preserveAspect = true;
                    art.enabled = art.sprite != null; art.raycastTarget = false;
                    art.color = unlocked ? Color.white : new Color(0, 0, 0, .26f);
                }
                var rarity = DesktopModalUi.FindComponent<Text>(slot.transform, "Rarity");
                if (rarity != null) rarity.text = unlocked ? DesktopGameData.RarityLabel(def.rarityTier) : "未解锁";
                var collection=slot.GetComponent<CozyProfileCollectionCard>();
                if(collection!=null) collection.Bind(unlocked?DesktopGameData.SpeciesName(def.speciesId):"尚未发现",unlocked?"累计捕获 "+entry.totalCaught+" 次":"捕获后解锁",CozyItemArt.Fish(def.speciesId),!unlocked,unlocked);
                var captured = def.speciesId;
                var button = slot.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => SelectSpecies(captured));
                    var visual = button.GetComponent<CozyButtonVisual>();
                    if (visual != null)
                        visual.SetSelected(def.speciesId == _selectedId);
                }
                CozyStickerMotion.Bind(slot, "codex:" + (_api?.PlayerId ?? "") + ":" + def.speciesId, i,
                    _grid.parent as RectTransform, sourceId: def.speciesId);
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

        public void SelectSpecies(string speciesId)
        {
            if (!_uiReady || string.IsNullOrEmpty(speciesId)) return;
            _selectedId = speciesId;
            var species = DesktopFishCatalog.GetSpecies(speciesId);
            var entry = FindEntry(speciesId);
            var unlocked = entry != null && entry.totalCaught > 0;
            foreach (Transform slot in _grid)
                slot.GetComponent<CozyButtonVisual>()?.SetSelected(slot.name == speciesId);
            var art = DesktopModalUi.FindComponent<Image>(transform, "GalleryAside/DesignArt");
            if (art != null)
            {
                art.sprite = CozyItemArt.Fish(speciesId); art.enabled = art.sprite != null;
                art.color = unlocked ? Color.white : new Color(0, 0, 0, .26f);
                art.preserveAspect = true; art.raycastTarget = false;
            }
            CozyItemArt.Text(transform, "GalleryAside/DesignName", unlocked && species != null ? species.Name : "？？？");
            var locked = transform.Find("GalleryAside/LockedHint"); if (locked != null) locked.gameObject.SetActive(!unlocked);
            var scroll = transform.Find("Detail")?.GetComponent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
            if (species == null) { _detail.text = "未知鱼种。"; return; }
            if (!unlocked)
            {
                _detail.text = "尚未解锁\n捕获后，记录它的名字、食性、体型与推荐鱼饵。";
                return;
            }
            var first = "—";
            if (entry.firstCaughtAt > 0)
            {
                try { first = DateTimeOffset.FromUnixTimeMilliseconds(entry.firstCaughtAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm"); }
                catch (ArgumentOutOfRangeException) { }
            }
            _detail.text = "稀有度：" + species.RarityLabel +
                "\n食性：" + species.DietLabel +
                "\n钓组：" + (string.IsNullOrEmpty(species.CatchGroupLabel) ? "—" : species.CatchGroupLabel) +
                "\n体型参考：" + species.TypicalMinM.ToString("0.00") + "–" + species.TypicalMaxM.ToString("0.00") + " m" +
                "\n推荐鱼饵：" + DesktopFishCatalog.TopBaits(species) +
                "\n累计捕获：" + entry.totalCaught +
                "\n最大体型：" + entry.maxSizeM.ToString("0.00") + " m" +
                "\n首次捕获：" + first;
        }
    }
}
