using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    public sealed class DesktopGalleryModalView : MonoBehaviour
    {
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
            if (transform.childCount == 0)
                Build();
        }

        public void OnOpened()
        {
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

        void Build()
        {
            _status = DesktopModalUi.Label(transform, "Status", string.Empty, 16, TextAnchor.MiddleLeft);
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 1f);
            statusRt.anchorMax = new Vector2(1f, 1f);
            statusRt.pivot = new Vector2(0f, 1f);
            statusRt.sizeDelta = new Vector2(-80f, 28f);

            var retry = DesktopModalUi.MakeButton(transform, "Retry", "重试", () => StartCoroutine(Load()));
            var retryRt = retry.GetComponent<RectTransform>();
            retryRt.anchorMin = new Vector2(1f, 1f);
            retryRt.anchorMax = new Vector2(1f, 1f);
            retryRt.pivot = new Vector2(1f, 1f);
            retryRt.sizeDelta = new Vector2(72f, 28f);

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            gridGo.transform.SetParent(transform, false);
            gridGo.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f, 1f);
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = Vector2.zero;
            gridRt.anchorMax = new Vector2(0.62f, 1f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = new Vector2(-8f, -36f);
            var content = new GameObject("Species", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(gridGo.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(110f, 56f);
            grid.spacing = new Vector2(6f, 6f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            gridGo.GetComponent<ScrollRect>().content = contentRt;
            gridGo.GetComponent<ScrollRect>().horizontal = false;
            _grid = content.transform;

            _detail = DesktopModalUi.Label(transform, "Detail", "选择一个鱼种查看图鉴。", 16, TextAnchor.UpperLeft);
            var detailRt = _detail.rectTransform;
            detailRt.anchorMin = new Vector2(0.62f, 0f);
            detailRt.anchorMax = Vector2.one;
            detailRt.offsetMin = new Vector2(8f, 8f);
            detailRt.offsetMax = new Vector2(0f, -36f);
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
            _status.text = "已解锁 " + unlocked + " / " + DesktopFishCatalog.Species.Length;
            RenderGrid();
        }

        void RenderGrid()
        {
            DesktopModalUi.Clear(_grid);
            for (var i = 0; i < DesktopFishCatalog.Species.Length; i++)
            {
                var species = DesktopFishCatalog.Species[i];
                var entry = FindEntry(species.Id);
                var unlocked = entry != null && entry.totalCaught > 0;
                var slot = new GameObject(species.Id, typeof(RectTransform), typeof(Image), typeof(Button));
                slot.transform.SetParent(_grid, false);
                slot.GetComponent<Image>().color = species.Id == _selectedId ? DesktopModalUi.SlotOn : DesktopModalUi.Slot;
                var label = unlocked ? species.Name : "？？？";
                var text = DesktopModalUi.Label(slot.transform, "T", label, 14, TextAnchor.MiddleCenter);
                DesktopModalUi.Stretch(text.gameObject);
                var captured = species.Id;
                slot.GetComponent<Button>().onClick.AddListener(() => Select(captured));
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
                _detail.text = "未解锁\n捕获后显示食性、咬钩、脱钩和推荐鱼饵。";
                RenderGrid();
                return;
            }
            var first = entry.firstCaughtAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(entry.firstCaughtAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "—";
            _detail.text = species.Name +
                           "\n食性：" + species.DietLabel +
                           "\n基础咬钩：" + DesktopFishCatalog.FormatBiteRate(species) +
                           "\n脱钩率：" + (species.BaseEscapeRate * 100f).ToString("0.0") + "%" +
                           "\n累计捕获：" + entry.totalCaught +
                           "\n最大体型：" + entry.maxSizeM.ToString("0.00") + "m" +
                           "\n首次捕获：" + first +
                           "\n推荐鱼饵：" + DesktopFishCatalog.TopBaits(species);
            RenderGrid();
        }
    }
}
