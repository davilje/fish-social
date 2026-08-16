using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    public sealed class DesktopCatchBagModalView : MonoBehaviour
    {
        const int MinSlots = 80;
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        Text _status;
        Text _coins;
        Text _detail;
        Transform _grid;
        FishInventoryItemDto[] _items = new FishInventoryItemDto[0];
        int _coinsValue;
        string _selectedId;
        bool _busy;

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond)
        {
            _api = api;
            _pond = pond;
            _coins = DesktopModalUi.FindComponent<Text>(transform, "Coins");
            _status = DesktopModalUi.FindComponent<Text>(transform, "Status");
            _detail = DesktopModalUi.FindComponent<Text>(transform, "Detail");
            _grid = DesktopModalUi.FindChild(transform, "Grid/Slots");

            DesktopModalUi.BindButton(transform, "Retry", () => StartCoroutine(Load()));
            DesktopModalUi.BindButton(transform, "Sell", SellSelected);
            DesktopModalUi.BindButton(transform, "Share", ShareSelected);
            if (_coins == null || _status == null || _detail == null || _grid == null)
                Debug.LogError("[DesktopUI] PanelCatch prefab is missing required controls.");
        }

        public void OnOpened()
        {
            if (_pond != null)
                _pond.InventoryUpdated += OnInventory;
            StartCoroutine(Load());
        }

        public void OnClosed()
        {
            if (_pond != null)
                _pond.InventoryUpdated -= OnInventory;
        }

        void OnDestroy()
        {
            if (_pond != null)
                _pond.InventoryUpdated -= OnInventory;
        }

        void OnInventory(FishInventoryItemDto[] items)
        {
            _items = items ?? new FishInventoryItemDto[0];
            RenderGrid();
        }

        void Build()
        {
            _coins = DesktopModalUi.Label(transform, "Coins", "金币：—", 18, TextAnchor.MiddleLeft);
            var coinsRt = _coins.rectTransform;
            coinsRt.anchorMin = new Vector2(0f, 1f);
            coinsRt.anchorMax = new Vector2(0.4f, 1f);
            coinsRt.pivot = new Vector2(0f, 1f);
            coinsRt.sizeDelta = new Vector2(0f, 28f);

            _status = DesktopModalUi.Label(transform, "Status", string.Empty, 14, TextAnchor.MiddleLeft);
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = new Vector2(0.4f, 1f);
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
            gridRt.anchorMax = new Vector2(0.68f, 1f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = new Vector2(-8f, -36f);
            var content = new GameObject("Slots", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(gridGo.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(86f, 64f);
            grid.spacing = new Vector2(6f, 6f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 7;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = gridGo.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            _grid = content.transform;

            _detail = DesktopModalUi.Label(transform, "Detail", "选中一条鱼获查看详情。", 16, TextAnchor.UpperLeft);
            var detailRt = _detail.rectTransform;
            detailRt.anchorMin = new Vector2(0.68f, 0f);
            detailRt.anchorMax = Vector2.one;
            detailRt.offsetMin = new Vector2(8f, 96f);
            detailRt.offsetMax = new Vector2(0f, -36f);

            var sell = DesktopModalUi.MakeButton(transform, "Sell", "出售", SellSelected);
            var sellRt = sell.GetComponent<RectTransform>();
            sellRt.anchorMin = new Vector2(0.68f, 0f);
            sellRt.anchorMax = new Vector2(0.84f, 0f);
            sellRt.offsetMin = new Vector2(8f, 8f);
            sellRt.offsetMax = new Vector2(-4f, 48f);
            var share = DesktopModalUi.MakeButton(transform, "Share", "分享动态", ShareSelected);
            var shareRt = share.GetComponent<RectTransform>();
            shareRt.anchorMin = new Vector2(0.84f, 0f);
            shareRt.anchorMax = Vector2.one;
            shareRt.offsetMin = new Vector2(4f, 8f);
            shareRt.offsetMax = new Vector2(0f, 48f);
        }

        IEnumerator Load()
        {
            if (_status == null || _detail == null)
                yield break;
            if (_api == null || !_api.CanUse)
            {
                _status.text = "请先完成 Steam 登录。";
                yield break;
            }
            _status.text = "正在加载背包…";
            var itemsOk = false;
            var coinsOk = false;
            string error = null;
            yield return _api.GetInventoryItems((ok, items, message) =>
            {
                itemsOk = ok;
                if (ok) _items = items ?? new FishInventoryItemDto[0];
                else error = message;
            });
            yield return _api.GetCoins((ok, coins, message) =>
            {
                coinsOk = ok;
                if (ok) _coinsValue = coins;
                else if (error == null) error = message;
            });
            if (!itemsOk)
            {
                _status.text = error ?? "背包加载失败。";
                _detail.text = _status.text + "\n点击重试。";
                yield break;
            }
            if (coinsOk)
                _coins.text = "金币：" + _coinsValue;
            else
                _coins.text = "金币：暂不可用";
            _status.text = itemsOk ? "已占用 " + _items.Length + " / " + Mathf.Max(MinSlots, _items.Length) : error;
            RenderGrid();
        }

        void RenderGrid()
        {
            if (_grid == null)
                return;
            DesktopModalUi.Clear(_grid);
            var count = Mathf.Max(MinSlots, _items != null ? _items.Length : 0);
            if (_items == null || _items.Length == 0)
                _detail.text = "背包是空的。钓到鱼并领取后会显示在这里。";
            for (var i = 0; i < count; i++)
            {
                var item = _items != null && i < _items.Length ? _items[i] : null;
                var slot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                slot.transform.SetParent(_grid, false);
                var selected = item != null && item.id == _selectedId;
                slot.GetComponent<Image>().color = selected ? DesktopModalUi.SlotOn : DesktopModalUi.Slot;
                var label = item == null
                    ? (i + 1).ToString()
                    : DesktopFishCatalog.SpeciesName(item.speciesId) + "\n" + DesktopFishCatalog.QualityName(item.quality);
                var text = DesktopModalUi.Label(slot.transform, "T", label, 12, TextAnchor.MiddleCenter);
                DesktopModalUi.Stretch(text.gameObject);
                if (item != null)
                {
                    var captured = item;
                    slot.GetComponent<Button>().onClick.AddListener(() => Select(captured));
                }
            }
        }

        void Select(FishInventoryItemDto item)
        {
            _selectedId = item.id;
            var species = DesktopFishCatalog.SpeciesName(item.speciesId);
            var quality = DesktopFishCatalog.QualityName(item.quality);
            var estimate = DesktopFishCatalog.EstimateSellPrice(item.quality, item.sizeM);
            _detail.text = "鱼种：" + species +
                           "\n品质：" + quality +
                           "\n体长：" + item.sizeM.ToString("0.00") + "m" +
                           "\n参考售价：" + estimate + " 金币（以服务端出售结果为准）";
            RenderGrid();
        }

        void SellSelected()
        {
            if (_busy || string.IsNullOrEmpty(_selectedId))
            {
                _status.text = "请先选中一条鱼获。";
                return;
            }
            _busy = true;
            _status.text = "正在出售…";
            StartCoroutine(_api.SellFish(_selectedId, (ok, earned, total, items, message) =>
            {
                _busy = false;
                if (!ok)
                {
                    _status.text = message;
                    return;
                }
                _items = items ?? new FishInventoryItemDto[0];
                _coinsValue = total;
                _coins.text = "金币：" + _coinsValue;
                _selectedId = null;
                _status.text = "出售成功，获得 " + earned + " 金币。";
                _detail.text = _status.text;
                RenderGrid();
            }));
        }

        void ShareSelected()
        {
            if (_busy || string.IsNullOrEmpty(_selectedId))
            {
                _status.text = "请先选中一条鱼获。";
                return;
            }
            _busy = true;
            _status.text = "正在分享…";
            var nickname = _pond != null ? _pond.Nickname : "Steam玩家";
            StartCoroutine(_api.ShareFish(_selectedId, nickname, (ok, message) =>
            {
                _busy = false;
                _status.text = message;
            }));
        }
    }
}
