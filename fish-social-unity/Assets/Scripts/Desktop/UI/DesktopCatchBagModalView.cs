using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    public sealed class DesktopCatchBagModalView : MonoBehaviour
    {
        bool _uiReady;
        const int MinSlots = 80;
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        Text _status;
        Text _coins;
        Text _detail;
        Transform _grid;
        Button _returnButton;
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

            var retryBound = DesktopModalUi.BindButton(transform, "Retry", () => StartCoroutine(Load()));
            var sellBound = DesktopModalUi.BindButton(transform, "Sell", SellSelected);
            var shareBound = DesktopModalUi.BindButton(transform, "Share", ShareSelected);
            EnsureReturnButton();
            DesktopModalUi.StyleTree(transform);
            _uiReady = _coins != null && _status != null && _detail != null && _grid != null &&
                _returnButton != null && retryBound && sellBound && shareBound;
            if (!_uiReady)
                Debug.LogError("[DesktopUI] PanelCatch prefab is missing required controls.", this);
        }

        void EnsureReturnButton()
        {
            _returnButton = DesktopModalUi.FindComponent<Button>(transform, "Return");
            if (!DesktopModalUi.BindButton(transform, "Return", ReturnSelected))
                Debug.LogError("[DesktopUI] PanelCatch requires Return Button.", this);
            RefreshReturnButton();
        }

        public void OnOpened()
        {
            if (!_uiReady)
                return;
            if (_pond != null)
                _pond.InventoryUpdated += OnInventory;
            RefreshReturnButton();
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

        bool CanReturnHere()
        {
            var mode = _pond != null && _pond.CurrentUser != null
                ? _pond.CurrentUser.returnFeeMode
                : null;
            if (mode == "sell_only" || mode == "auto_return")
                return false;
            return _pond != null &&
                   _pond.HasSpot &&
                   !string.IsNullOrEmpty(_pond.CurrentPondId);
        }

        void RefreshReturnButton()
        {
            if (_returnButton == null)
                return;
            var mode = _pond != null && _pond.CurrentUser != null
                ? _pond.CurrentUser.returnFeeMode
                : null;
            if (mode == "sell_only")
            {
                _returnButton.gameObject.SetActive(true);
                _returnButton.interactable = false;
                var label = _returnButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = "本局出售档";
                return;
            }
            if (mode == "auto_return")
            {
                _returnButton.gameObject.SetActive(true);
                _returnButton.interactable = false;
                var label = _returnButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = "自动回鱼档";
                return;
            }
            _returnButton.gameObject.SetActive(true);
            var defaultLabel = _returnButton.GetComponentInChildren<Text>();
            if (defaultLabel != null && defaultLabel.text != "回鱼")
                defaultLabel.text = "回鱼";
            _returnButton.interactable = CanReturnHere() && !_busy;
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
            RefreshReturnButton();
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
                var slot = DesktopUiPrefabFactory.Instantiate("CatchSlot", _grid);
                if (slot == null)
                    continue;
                slot.name = "Slot" + i;
                var selected = item != null && item.id == _selectedId;
                var slotButton = slot.GetComponent<Button>();
                if (slotButton != null)
                {
                    DesktopModalUi.StyleButton(slotButton);
                    var visual = slotButton.GetComponent<CozyButtonVisual>();
                    if (visual != null)
                        visual.SetSelected(selected);
                    slotButton.interactable = item != null;
                }
                var label = item == null
                    ? (i + 1).ToString()
                    : DesktopFishCatalog.SpeciesName(item.speciesId) + "\n" + DesktopFishCatalog.QualityName(item.quality);
                var text = DesktopUiPrefabFactory.Child(slot, "Label");
                var labelText = text != null ? text.GetComponent<Text>() : null;
                if (labelText != null)
                {
                    labelText.text = label;
                }
                DesktopModalUi.StyleTree(slot.transform);
                if (slotButton != null)
                    slotButton.onClick.RemoveAllListeners();
                if (item != null)
                {
                    var captured = item;
                    var button = slot.GetComponent<Button>();
                    if (button != null)
                        button.onClick.AddListener(() => Select(captured));
                }
            }
        }

        void Select(FishInventoryItemDto item)
        {
            _selectedId = item.id;
            var species = DesktopFishCatalog.SpeciesName(item.speciesId);
            var quality = DesktopFishCatalog.QualityName(item.quality);
            var sell = DesktopFishCatalog.EstimateSellPrice(item.quality, item.sizeM, item.speciesId);
            var jin = DesktopGameData.CalcWeightJin(item.sizeM);
            var eligible = DesktopGameData.IsReturnEligible(item.quality, item.sizeM);
            var returnMul = eligible ? DesktopGameData.ResolveReturnGoldMul(item.sizeM) : 0f;
            var returnGold = Mathf.FloorToInt(sell * returnMul);
            var seatHint = CanReturnHere()
                ? "可回当前塘（约增重 0.02~0.05m；需回鱼档）"
                : "回鱼需在当前鱼塘钓位且选择回鱼档";
            string returnHint;
            if (!eligible)
            {
                if (DesktopGameData.ResolveReturnGoldMul(item.sizeM) <= 0f)
                    returnHint = "回鱼约得：不可回（需达到 " + DesktopGameData.MinReturnWeightJin().ToString("0") + " 斤，且紫品及以上）";
                else
                    returnHint = "回鱼约得：不可回（需紫品及以上）";
            }
            else
            {
                returnHint = "回鱼约得：" + returnGold + " 金币（卖价×" + (returnMul * 100f).ToString("0") + "%，" +
                             (jin > DesktopGameData.HeavyReturnWeightJin() ? ">100斤档" : "≥10斤档") + "）";
            }
            _detail.text = "鱼种：" + species +
                           "\n品质：" + quality +
                           "\n体长：" + item.sizeM.ToString("0.00") + "m" +
                           "\n重量：" + DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(item.sizeM)) +
                           "（约 " + jin.ToString("0.##") + " 斤）" +
                           "\n参考售价：" + sell + " 金币" +
                           "\n" + returnHint +
                           "\n" + seatHint;
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
            RefreshReturnButton();
            _status.text = "正在出售…";
            StartCoroutine(_api.SellFish(_selectedId, (ok, earned, total, items, message) =>
            {
                _busy = false;
                RefreshReturnButton();
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

        void ReturnSelected()
        {
            if (_busy || string.IsNullOrEmpty(_selectedId))
            {
                _status.text = "请先选中一条鱼获。";
                return;
            }
            if (!CanReturnHere())
            {
                _status.text = "需在当前鱼塘钓位才能回鱼。";
                return;
            }
            _busy = true;
            RefreshReturnButton();
            _status.text = "正在回鱼…";
            StartCoroutine(_api.ReturnFishToPond(_selectedId,
                (ok, gold, playerXp, pondXp, newSizeM, sizeGainM, totalCoins, items, message) =>
                {
                    _busy = false;
                    RefreshReturnButton();
                    if (!ok)
                    {
                        _status.text = message ?? "回鱼失败。";
                        return;
                    }
                    _items = items ?? new FishInventoryItemDto[0];
                    if (totalCoins > 0 || gold > 0)
                    {
                        _coinsValue = totalCoins;
                        _coins.text = "金币：" + _coinsValue;
                    }
                    _selectedId = null;
                    _status.text = "回鱼成功：+" + gold + " 金，玩家XP +" + playerXp +
                                   "，塘XP +" + pondXp +
                                   "，塘内增重约 " + sizeGainM.ToString("0.00") + "m" +
                                   "（现 " + newSizeM.ToString("0.00") + "m）。";
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
            RefreshReturnButton();
            _status.text = "正在分享…";
            var nickname = _pond != null ? _pond.Nickname : "Steam玩家";
            StartCoroutine(_api.ShareFish(_selectedId, nickname, (ok, message) =>
            {
                _busy = false;
                RefreshReturnButton();
                _status.text = message;
            }));
        }
    }
}
