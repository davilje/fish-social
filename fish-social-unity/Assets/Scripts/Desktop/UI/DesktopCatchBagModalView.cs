using System;
using System.Collections;
using System.Collections.Generic;
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
        bool _fishReady;
        Button _returnButton;
        Button _sellButton;
        Button _shareButton;
        Button _retry;
        Coroutine _loadRoutine;
        bool _itemsTab;
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
            _grid = (transform.Find("Grid/Viewport/Slots") ?? DesktopModalUi.FindChild(transform, "Grid/Slots"));

            _retry = DesktopModalUi.FindComponent<Button>(transform, "Retry");
            _sellButton = DesktopModalUi.FindComponent<Button>(transform, "Sell");
            _shareButton = DesktopModalUi.FindComponent<Button>(transform, "Share");
            if (_retry != null)
            {
                _retry.gameObject.SetActive(false);
                _retry.onClick.RemoveAllListeners();
                _retry.onClick.AddListener(Refresh);
            }
            var sellBound = DesktopModalUi.BindButton(transform, "Sell", SellSelected);
            var shareBound = DesktopModalUi.BindButton(transform, "Share", ShareSelected);
            EnsureReturnButton();
            BindCategory("DesignCategoryFish", false);
            BindCategory("DesignCategoryItems", true);
            ApplyCategoryState();
            DesktopModalUi.StyleTree(transform);
            _uiReady = _coins != null && _status != null && _detail != null && _grid != null &&
                _returnButton != null && sellBound && shareBound;
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
            {
                _pond.InventoryUpdated -= OnInventory;
                _pond.InventoryUpdated += OnInventory;
            }
            RefreshReturnButton();
            ShowBagRoot();
            if (_itemsTab || _fishReady)
            {
                RestoreCategoryDetail();
                return;
            }
            Refresh();
        }

        public void OnClosed()
        {
            StopAllCoroutines();
            _loadRoutine = null;
            _busy = false;
        }

        void OnDestroy()
        {
            if (_pond != null)
                _pond.InventoryUpdated -= OnInventory;
        }

        void OnInventory(FishInventoryItemDto[] items)
        {
            _items = items ?? new FishInventoryItemDto[0];
            if (_itemsTab)
            {
                _fishReady = false;
                return;
            }
            _fishReady = true;
            ShowBagRoot();
            RenderGrid();
        }

        void BindCategory(string path, bool items)
        {
            var button = DesktopModalUi.FindComponent<Button>(transform, path);
            if (button == null) return;
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (_busy) return;
                if (_itemsTab == items)
                {
                    RevealBagPhoto();
                    return;
                }
                if (!Application.isPlaying)
                {
                    _itemsTab = items;
                    ApplyCategoryState();
                    ShowBagRoot();
                    RestoreCategoryDetail();
                    return;
                }
                if (_categoryMotion != null) StopCoroutine(_categoryMotion);
                _categoryMotion = StartCoroutine(SwitchBag(items));
            });
        }

        Coroutine _categoryMotion;
        IEnumerator SwitchBag(bool items)
        {
            var motion = GetComponent<CozyJournalPageMotion>();
            if (Application.isPlaying && motion != null) yield return motion.Exit();
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);
            _loadRoutine = null;
            _itemsTab = items;
            ApplyCategoryState();
            ShowBagRoot();
            var ready = _itemsTab || _fishReady;
            if (!ready)
            {
                ClearGrid();
                ShowMessage(DesktopLocalization.Text("ui.catch.bag.status.loading_catches", "正在加载鱼获…"));
                Refresh();
                motion?.PlayEntrance(0);
            }
            else
            {
                RestoreCategoryDetail();
                if (!_itemsTab && _grid != null)
                {
                    var stickers = _grid.GetComponentsInChildren<CozyStickerMotion>();
                    for (var i = 0; i < stickers.Length; i++)
                        stickers[i].RestartEntrance();
                }
                motion?.PlayEntrance(!_itemsTab && _grid != null ? _grid.childCount : 0);
            }
            _categoryMotion = null;
        }

        void ApplyCategoryState()
        {
            if (_sellButton != null) _sellButton.gameObject.SetActive(!_itemsTab);
            if (_shareButton != null) _shareButton.gameObject.SetActive(!_itemsTab);
            if (_returnButton != null) _returnButton.gameObject.SetActive(!_itemsTab);
            // Equipment belongs to the equipment page, not the inventory's item category.
            foreach (var path in new[] { "Reflow_CatchDetails", "DesignDetailHeading", "JournalBagPhoto",
                "DesignArtBacking", "DesignArt", "DesignName", "JournalPhotoTape", "DetailRows", "Detail" })
                transform.Find(path)?.gameObject.SetActive(!_itemsTab);
            transform.Find("BagItemsEmpty")?.gameObject.SetActive(_itemsTab);
            DesktopModalUi.FindComponent<CozyButtonVisual>(transform, "DesignCategoryFish")?.SetSelected(!_itemsTab);
            DesktopModalUi.FindComponent<CozyButtonVisual>(transform, "DesignCategoryItems")?.SetSelected(_itemsTab);
            CozyItemArt.Text(transform, "Reflow_BagHeading", _itemsTab ? DesktopLocalization.Text("ui.catch.bag.title.items", "我的道具") : DesktopLocalization.Text("ui.catch.bag.title.catches", "我的鱼获"));
            CozyItemArt.Text(transform, "DesignDetailHeading", _itemsTab ? DesktopLocalization.Text("ui.catch.bag.title.item_details", "道具详情") : DesktopLocalization.Text("ui.catch.bag.title.catch_details", "鱼获详情"));
            CozyItemArt.Text(transform, "DesignBagNote", _itemsTab
                ? DesktopLocalization.Text("ui.catch.bag.hint.equipment_location", "钓竿、鱼饵和船具\n请在装备页查看和使用") : DesktopLocalization.Text("ui.catch.bag.hint.catch_actions", "背包内展示真实鱼获\n选中后可出售、回鱼或分享"));
        }

        void RestoreCategoryDetail()
        {
            if (_retry != null) _retry.gameObject.SetActive(false);
            if (_itemsTab)
            {
                ShowMessage(string.Empty);
                DesktopLocalization.SetText(_status, DesktopLocalization.Text("ui.catch.bag.empty.items", "暂无道具。钓竿、鱼饵和船具请在装备页查看。"));
                RefreshReturnButton();
                return;
            }
            DesktopLocalization.SetText(_status, DesktopLocalization.Format("ui.catch.bag.label.capacity", (object)(_items.Length), (object)(Mathf.Max(MinSlots, _items.Length))));
            if (SelectedItem() != null) Select(SelectedItem(), false);
            else ShowMessage(DesktopLocalization.Text("ui.catch.bag.empty.catches", "背包是空的。钓到鱼并领取后会显示在这里。"));
            RefreshReturnButton();
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
            var selected = SelectedItem();
            var available = !_itemsTab && selected != null && !_busy && _api != null && _api.CanUse;
            if (_sellButton != null) _sellButton.interactable = available;
            if (_shareButton != null) _shareButton.interactable = available;
            if (_returnButton == null)
                return;
            if (_itemsTab) { _returnButton.gameObject.SetActive(false); return; }
            var mode = _pond != null && _pond.CurrentUser != null
                ? _pond.CurrentUser.returnFeeMode
                : null;
            if (mode == "sell_only")
            {
                _returnButton.gameObject.SetActive(true);
                _returnButton.interactable = false;
                var label = _returnButton.GetComponentInChildren<Text>();
                if (label != null)
                    DesktopLocalization.SetText(label, DesktopLocalization.Text("ui.catch.bag.button.release", "回鱼"));
                return;
            }
            if (mode == "auto_return")
            {
                _returnButton.gameObject.SetActive(true);
                _returnButton.interactable = false;
                var label = _returnButton.GetComponentInChildren<Text>();
                if (label != null)
                    DesktopLocalization.SetText(label, DesktopLocalization.Text("ui.catch.bag.button.release", "回鱼"));
                return;
            }
            _returnButton.gameObject.SetActive(true);
            var defaultLabel = _returnButton.GetComponentInChildren<Text>();
            if (defaultLabel != null && defaultLabel.text != "回鱼")
                DesktopLocalization.SetText(defaultLabel, DesktopLocalization.Text("ui.catch.bag.button.release", "回鱼"));
            _returnButton.interactable = available && CanReturnHere() &&
                DesktopGameData.IsReturnEligible(selected.quality, selected.sizeM);
        }

        FishInventoryItemDto SelectedItem()
        {
            foreach (var item in _items)
                if (item != null && item.id == _selectedId) return item;
            return null;
        }

        void Refresh()
        {
            if (!_uiReady || _busy) return;
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);
            _loadRoutine = StartCoroutine(Load());
        }

        IEnumerator Load()
        {
            if (_retry != null) _retry.gameObject.SetActive(false);
            if (_itemsTab)
            {
                RestoreCategoryDetail();
                _loadRoutine = null;
                yield break;
            }
            if (_status == null || _detail == null)
                yield break;
            if (_api == null || !_api.CanUse)
            {
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(DesktopLocalization.Text("ui.catch.bag.error.login_required", "请先完成 Steam 登录。")));
                yield break;
            }
            DesktopLocalization.SetText(_status, DesktopLocalization.Text("ui.catch.bag.status.loading_inventory", "正在加载背包…"));
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
            _loadRoutine = null;
            if (!itemsOk)
            {
                if (_retry != null) _retry.gameObject.SetActive(true);
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(error ?? DesktopLocalization.Text("ui.catch.bag.error.inventory_load_failed", "背包加载失败。")));
                DesktopLocalization.SetText(_detail, DesktopLocalization.Format("ui.catch.bag.hint.retry_loading", (object)(_status.text)));
                yield break;
            }
            if (coinsOk)
                DesktopLocalization.SetText(_coins, DesktopLocalization.Format("ui.catch.bag.label.coins", (object)(_coinsValue)));
            else
                DesktopLocalization.SetText(_coins, DesktopLocalization.Text("ui.catch.bag.label.coins_unavailable", "金币：暂不可用"));
            DesktopLocalization.SetText(_status, DesktopLocalization.Message(itemsOk ? DesktopLocalization.Format("ui.catch.bag.label.capacity", (object)(_items.Length), (object)(Mathf.Max(MinSlots, _items.Length))) : error));
            RefreshReturnButton();
            if (itemsOk)
                _fishReady = true;
            RenderGrid();
        }

        void ShowBagRoot()
        {
            if (_grid != null) _grid.gameObject.SetActive(!_itemsTab);
        }

        void ClearGrid()
        {
            // Destroy is deferred in play mode. Detach retired rows now so layout and id lookup see only the replacement set.
            for (var i = _grid.childCount - 1; i >= 0; i--)
            {
                var old = _grid.GetChild(i).gameObject;
                old.SetActive(false); old.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
        }

        void RenderGrid()
        {
            if (_itemsTab) { RestoreCategoryDetail(); return; }
            if (_grid == null)
                return;
            if (SelectedItem() == null)
                _selectedId = _items != null && _items.Length > 0 && _items[0] != null ? _items[0].id : null;
            RefreshReturnButton();
            ClearGrid();
            var count = _items != null ? _items.Length : 0;
            if (_items == null || _items.Length == 0)
                ShowMessage(DesktopLocalization.Text("ui.catch.bag.empty.catches", "背包是空的。钓到鱼并领取后会显示在这里。"));
            else if (SelectedItem() != null)
                Select(SelectedItem(), false);
            var rareOrder = 0;
            for (var i = 0; i < count; i++)
            {
                var item = _items != null && i < _items.Length ? _items[i] : null;
                var slot = DesktopUiPrefabFactory.Instantiate("CatchSlot", _grid);
                if (slot == null)
                    continue;
                slot.name = DesktopLocalization.Concat("Slot", i);
                CozyItemArt.Set(slot.transform, "DesignItemArt", item != null ? CozyItemArt.Fish(item.speciesId) : null);
                var backing = slot.transform.Find("DesignArtBacking");
                if (backing != null)
                    backing.gameObject.SetActive(item != null);
                var selected = item != null && item.id == _selectedId;
                var slotButton = slot.GetComponent<Button>();
                if (slotButton != null)
                {
                    DesktopModalUi.StyleButton(slotButton);
                    var visual = slotButton.GetComponent<CozyButtonVisual>();
                    if (visual != null)
                        visual.SetSelected(selected);
                    slotButton.interactable = item != null && !_busy;
                }
                var label = item == null
                    ? (i + 1).ToString()
                    : DesktopFishCatalog.SpeciesName(item.speciesId);
                var text = DesktopUiPrefabFactory.Child(slot, "Label");
                var labelText = text != null ? text.GetComponent<Text>() : null;
                if (labelText != null)
                {
                    DesktopLocalization.SetText(labelText, label);
                    CozyItemArt.Text(slot.transform, "Meta", item == null ? "" : DesktopLocalization.Concat(item.sizeM.ToString("0.00"), " m"));
                }
                DesktopModalUi.StyleTree(slot.transform);
                if (slotButton != null)
                    slotButton.onClick.RemoveAllListeners();
                if (item != null)
                {
                    AddUnreadBadge(slot, CozyJournalReadState.IsUnread(_api?.PlayerId, item.id));
                    CozyStickerMotion.Bind(slot, DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat("catch:", (_api?.PlayerId ?? "")), ":"), item.id), i,
                        _grid.parent as RectTransform, sourceId: item.speciesId);
                    var tier = DesktopFishCatalog.GetSpecies(item.speciesId)?.RarityTier;
                    if (tier == "rare" || tier == "legendary") CozyRareGlint.Bind(slot, rareOrder++);
                    var captured = item;
                    var button = slot.GetComponent<Button>();
                    if (button != null)
                        button.onClick.AddListener(() => Select(captured));
                }
            }
        }

        static void AddUnreadBadge(GameObject slot, bool unread)
        {
            if (!unread) return;
            var go = new GameObject("JournalNew", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(slot.transform, false);
            var image = go.GetComponent<Image>();
            image.sprite = CozyJournalTheme.Sprite("decor/new-corner");
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(60, 28);
            rect.anchoredPosition = new Vector2(3, 3);
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = CatTheme.Font;
            label.fontSize = 12;
            DesktopLocalization.SetText(label, DesktopLocalization.Text("ui.catch.bag.badge.new", "NEW"));
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        void Select(FishInventoryItemDto item, bool rebuild = true)
        {
            if (_busy || item == null) return;
            _selectedId = item.id;
            if (rebuild) CozyJournalReadState.MarkRead(_api?.PlayerId, item.id);
            CozyItemArt.Set(transform, "DesignArt", CozyItemArt.Fish(item.speciesId));
            foreach (var layer in new[] { "JournalBagPhoto", "DesignArtBacking", "DesignArt", "DesignName", "JournalPhotoTape" })
            {
                var node = transform.Find(layer);
                if (node != null) node.SetAsLastSibling();
            }
            var species = DesktopFishCatalog.SpeciesName(item.speciesId);
            var quality = DesktopFishCatalog.QualityName(item.quality);
            var sell = DesktopFishCatalog.EstimateSellPrice(item.quality, item.sizeM, item.speciesId);
            var jin = DesktopGameData.CalcWeightJin(item.sizeM);
            var eligible = DesktopGameData.IsReturnEligible(item.quality, item.sizeM);
            var returnMul = eligible ? DesktopGameData.ResolveReturnGoldMul(item.sizeM) : 0f;
            var returnGold = Mathf.FloorToInt(sell * returnMul);
            var seatHint = CanReturnHere()
                ? DesktopLocalization.Text("ui.catch.bag.hint.release_growth", "可回当前塘（约增重 0.02~0.05m；需回鱼档）")
                : DesktopLocalization.Text("ui.catch.bag.hint.release_requirements", "回鱼需在当前鱼塘钓位且选择回鱼档");
            string returnHint;
            string returnValue;
            if (!eligible)
            {
                var needsWeight = DesktopGameData.ResolveReturnGoldMul(item.sizeM) <= 0f;
                returnHint = needsWeight
                    ? DesktopLocalization.Format("ui.catch.bag.hint.release_weight_and_quality", (object)(DesktopGameData.MinReturnWeightJin().ToString("0")))
                    : DesktopLocalization.Text("ui.catch.bag.hint.release_quality", "回鱼约得：不可回（需紫品及以上）");
                returnValue = needsWeight
                    ? DesktopLocalization.Format("ui.catch.bag.label.release_weight_and_quality", (object)(DesktopGameData.MinReturnWeightJin().ToString("0")))
                    : DesktopLocalization.Text("ui.catch.bag.label.release_quality", "不可回（需紫品+）");
            }
            else
            {
                returnHint = DesktopLocalization.Format("ui.catch.bag.label.estimated_release_reward", (object)(returnGold), (object)((returnMul * 100f).ToString("0")), (object)((jin > DesktopGameData.HeavyReturnWeightJin() ? DesktopLocalization.Text("ui.catch.bag.label.large_fish_tier", ">100斤档") : DesktopLocalization.Text("ui.catch.bag.label.standard_fish_tier", "≥10斤档"))));
                returnValue = DesktopLocalization.Format("ui.catch.bag.format.estimated_reward", (object)(returnGold), (object)((CanReturnHere() ? "" : DesktopLocalization.Text("ui.catch.bag.fragment.seat_required", " · 需钓位"))));
            }
            var mode = _pond?.CurrentUser?.returnFeeMode;
            if (mode == "sell_only") returnValue = DesktopLocalization.Text("ui.catch.bag.status.sell_mode", "本局出售档 · 不可回鱼");
            else if (mode == "auto_return") returnValue = DesktopLocalization.Text("ui.catch.bag.status.auto_release_mode", "本局自动回鱼");
            var size = DesktopLocalization.Concat(item.sizeM.ToString("0.00"), " m");
            var weight = DesktopLocalization.Format("ui.catch.bag.format.approximate_weight", (object)(DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(item.sizeM))), (object)(jin.ToString("0.##")));
            CozyItemArt.Text(transform, "DesignName", species);
            if (CozyItemArt.Rows(transform, "DetailRows",
                    "star", DesktopLocalization.Text("ui.catch.bag.label.quality", "品质"), quality,
                    "hook", DesktopLocalization.Text("ui.catch.bag.label.length", "体长"), size,
                    "fish", DesktopLocalization.Text("ui.catch.bag.label.weight", "重量"), weight,
                    "coin", DesktopLocalization.Text("ui.catch.bag.label.reference_price", "参考售价"), DesktopLocalization.Format("ui.catch.bag.format.coins", (object)(sell)),
                    "pond", DesktopLocalization.Text("ui.catch.bag.button.release", "回鱼"), returnValue))
                DesktopLocalization.SetText(_detail, string.Empty);
            else
                DesktopLocalization.SetText(_detail, DesktopLocalization.Format("ui.catch.bag.label.catch_summary", (object)(species), (object)(quality), (object)(size), (object)(weight), (object)(sell), (object)(returnHint), (object)(seatHint)));
            if (rebuild && _grid != null)
            {
                for (var i=0;i<_grid.childCount;i++)
                {
                    var slot=_grid.GetChild(i);
                    if (!slot.gameObject.activeSelf) continue;
                    var visual=slot.GetComponent<CozyButtonVisual>();
                    var selected=i<_items.Length && _items[i]!=null && _items[i].id==_selectedId;
                    if (visual!=null) visual.SetSelected(selected);
                    if (selected && slot.Find("JournalNew")!=null) slot.Find("JournalNew").gameObject.SetActive(false);
                }
            }
            RevealBagPhoto();
        }

        void RevealBagPhoto()
        {
            var photo = transform.Find("JournalBagPhoto");
            if (photo == null) return;
            var pose = photo.GetComponent<CozyJournalStaticPose>() ?? photo.gameObject.AddComponent<CozyJournalStaticPose>();
            pose.PlayReveal(new Vector2(24f, 0f), .26f, .3f);
        }

        void ShowMessage(string message)
        { message = DesktopLocalization.Message(message);
            CozyItemArt.Set(transform, "DesignArt", null);
            CozyItemArt.Text(transform, "DesignName", string.Empty);
            CozyItemArt.Rows(transform, "DetailRows");
            DesktopLocalization.SetText(_detail, message);
        }

        void SellSelected()
        {
            if (_api == null || !_api.CanUse)
            {
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(DesktopLocalization.Text("ui.catch.bag.error.session_expired", "当前会话已失效，请重新登录。")));
                return;
            }
            if (_busy || string.IsNullOrEmpty(_selectedId))
            {
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(DesktopLocalization.Text("ui.catch.bag.error.catch_required", "请先选中一条鱼获。")));
                return;
            }
            _busy = true;
            RefreshReturnButton();
            DesktopLocalization.SetText(_status, DesktopLocalization.Text("ui.catch.bag.status.selling", "正在出售…"));
            StartCoroutine(_api.SellFish(_selectedId, (ok, earned, total, items, message) =>
            {
                _busy = false;
                RefreshReturnButton();
                if (!ok)
                {
                    DesktopLocalization.SetText(_status, DesktopLocalization.Message(message));
                    CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, false, "请重试");
                    return;
                }
                var before = CaptureGridPositions();
                var soldIndex = Array.FindIndex(_items, value => value != null && value.id == _selectedId);
                var soldCard = soldIndex >= 0 ? _grid.Find(DesktopLocalization.Concat("Slot", soldIndex)) as RectTransform : null;
                var canvas = GetComponentInParent<Canvas>();
                var layer = canvas != null ? canvas.transform.Find("MainRoot") as RectTransform : null;
                var coinTarget = layer != null ? layer.Find("Header/FishCoins/Icon") as RectTransform : null;
                CozyCardFragments.Tear(soldCard, layer);
                CozyActionFeedback.CoinsFly(soldCard, coinTarget, layer);
                _items = items ?? new FishInventoryItemDto[0];
                _coinsValue = total;
                DesktopLocalization.SetText(_coins, DesktopLocalization.Format("ui.catch.bag.label.coins", (object)(_coinsValue)));
                _selectedId = null;
                DesktopLocalization.SetText(_status, DesktopLocalization.Format("ui.catch.bag.status.sold", (object)(earned)));
                ShowMessage(_status.text);
                RenderGrid();
                Canvas.ForceUpdateCanvases();
                for (var i = 0; i < _items.Length; i++)
                    if (_items[i] != null && before.TryGetValue(_items[i].id, out var previous))
                        _grid.Find(DesktopLocalization.Concat("Slot", i))?.GetComponent<CozyStickerMotion>()?.ApplyReflow(previous);
            }));
        }

        Dictionary<string, Vector3> CaptureGridPositions()
        {
            var positions = new Dictionary<string, Vector3>();
            for (var i = 0; i < _items.Length; i++)
            {
                var slot = _grid.Find(DesktopLocalization.Concat("Slot", i));
                if (slot != null && _items[i] != null) positions[_items[i].id] = slot.position;
            }
            return positions;
        }

        void ReturnSelected()
        {
            if (_api == null || !_api.CanUse)
            {
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(DesktopLocalization.Text("ui.catch.bag.error.session_expired", "当前会话已失效，请重新登录。")));
                return;
            }
            if (_busy || string.IsNullOrEmpty(_selectedId))
            {
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(DesktopLocalization.Text("ui.catch.bag.error.catch_required", "请先选中一条鱼获。")));
                return;
            }
            if (!CanReturnHere())
            {
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(DesktopLocalization.Text("ui.catch.bag.error.release_seat_required", "需在当前鱼塘钓位才能回鱼。")));
                return;
            }
            _busy = true;
            RefreshReturnButton();
            DesktopLocalization.SetText(_status, DesktopLocalization.Text("ui.catch.bag.status.releasing", "正在回鱼…"));
            StartCoroutine(_api.ReturnFishToPond(_selectedId,
                (ok, gold, playerXp, pondXp, newSizeM, sizeGainM, totalCoins, items, message) =>
                {
                    _busy = false;
                    RefreshReturnButton();
                    if (!ok)
                    {
                        DesktopLocalization.SetText(_status, DesktopLocalization.Message(message ?? DesktopLocalization.Text("ui.catch.bag.error.release_failed", "回鱼失败。")));
                        CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, false, "请重试");
                        return;
                    }
                    _items = items ?? new FishInventoryItemDto[0];
                    if (totalCoins > 0 || gold > 0)
                    {
                        _coinsValue = totalCoins;
                        DesktopLocalization.SetText(_coins, DesktopLocalization.Format("ui.catch.bag.label.coins", (object)(_coinsValue)));
                    }
                    _selectedId = null;
                    DesktopLocalization.SetText(_status, DesktopLocalization.Format("ui.catch.bag.status.released_with_rewards", (object)(gold), (object)(playerXp), (object)(pondXp), (object)(sizeGainM.ToString("0.00")), (object)(newSizeM.ToString("0.00"))));
                    ShowMessage(_status.text);
                    RenderGrid();
                    CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, true, DesktopLocalization.Text("ui.catch.bag.badge.released", "已回鱼"));
                }));
        }

        void ShareSelected()
        {
            if (_api == null || !_api.CanUse)
            {
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(DesktopLocalization.Text("ui.catch.bag.error.session_expired", "当前会话已失效，请重新登录。")));
                return;
            }
            if (_busy || string.IsNullOrEmpty(_selectedId))
            {
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(DesktopLocalization.Text("ui.catch.bag.error.catch_required", "请先选中一条鱼获。")));
                return;
            }
            _busy = true;
            RefreshReturnButton();
            DesktopLocalization.SetText(_status, DesktopLocalization.Text("ui.catch.bag.status.sharing", "正在分享…"));
            var nickname = _pond != null ? _pond.Nickname : "Steam玩家";
            StartCoroutine(_api.ShareFish(_selectedId, nickname, (ok, message) =>
            {
                _busy = false;
                RefreshReturnButton();
                DesktopLocalization.SetText(_status, DesktopLocalization.Message(message));
                CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, ok, ok ? DesktopLocalization.Text("ui.catch.bag.status.shared", "已分享") : "请重试");
            }));
        }
    }
}
