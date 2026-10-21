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
        Button _returnButton;
        Button _sellButton;
        Button _shareButton;
        Button _retry;
        Coroutine _loadRoutine;
        bool _itemsTab;
        Button _equipItem;
        ShopGearDto _gear;
        readonly List<OwnedGearView> _ownedGear = new List<OwnedGearView>();
        string _selectedGear;

        sealed class OwnedGearView
        {
            public string id;
            public string name;
            public string description;
            public string kind;
            public Sprite art;
            public bool equipped;
        }
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
            _equipItem = DesktopModalUi.FindComponent<Button>(transform, "EquipItem");
            DesktopModalUi.BindButton(transform, "EquipItem", EquipSelectedItem);
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
            Refresh();
        }

        public void OnClosed()
        {
            StopAllCoroutines();
            _loadRoutine = null;
            _busy = false;
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
            if (!_itemsTab) RenderGrid();
        }

        void BindCategory(string path, bool items)
        {
            var button = DesktopModalUi.FindComponent<Button>(transform, path);
            if (button == null) return;
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (_busy || _itemsTab == items) return;
                _itemsTab = items;
                ApplyCategoryState();
                if(_categoryMotion!=null) StopCoroutine(_categoryMotion);
                _categoryMotion=StartCoroutine(ChangeBagCategory());
            });
        }

        Coroutine _categoryMotion;
        IEnumerator ChangeBagCategory()
        {
            var motion=GetComponent<CozyJournalPageMotion>();
            if(motion!=null) yield return motion.Exit();
            ClearGrid();
            ShowMessage(_itemsTab ? "正在加载已拥有的道具…" : "正在加载鱼获…");
            Refresh();
            motion?.PlayEntrance(0);
            _categoryMotion=null;
        }

        void ApplyCategoryState()
        {
            if (_sellButton != null) _sellButton.gameObject.SetActive(!_itemsTab);
            if (_shareButton != null) _shareButton.gameObject.SetActive(!_itemsTab);
            if (_returnButton != null) _returnButton.gameObject.SetActive(!_itemsTab);
            if (_equipItem != null) _equipItem.gameObject.SetActive(_itemsTab);
            DesktopModalUi.FindComponent<CozyButtonVisual>(transform, "DesignCategoryFish")?.SetSelected(!_itemsTab);
            DesktopModalUi.FindComponent<CozyButtonVisual>(transform, "DesignCategoryItems")?.SetSelected(_itemsTab);
            CozyItemArt.Text(transform, "Reflow_BagHeading", _itemsTab ? "我的道具" : "我的鱼获");
            CozyItemArt.Text(transform, "DesignDetailHeading", _itemsTab ? "道具详情" : "鱼获详情");
            CozyItemArt.Text(transform, "DesignBagNote", _itemsTab
                ? "已拥有的钓竿和船具\n已解锁的鱼饵" : "背包内展示真实鱼获\n选中后可出售、回鱼或分享");
        }

        IEnumerator LoadOwnedGear()
        {
            if (_api == null || !_api.CanUse)
            {
                _status.text = "请先完成 Steam 登录。";
                yield break;
            }
            _status.text = "正在加载道具…";
            ShopBaitDto[] baits = null;
            ShopTackleDto[] rods = null;
            ShopVesselDto[] vessels = null;
            string error = null;
            var catalogOk = false;
            var gearOk = false;
            yield return _api.GetShopCatalog((ok, loadedBaits, loadedRods, loadedVessels, message) =>
            {
                catalogOk = ok;
                baits = loadedBaits;
                rods = loadedRods;
                vessels = loadedVessels;
                error = message;
            });
            yield return _api.GetShopGear((ok, gear, coins, message) =>
            {
                gearOk = ok && gear != null;
                if (gearOk) { _gear = gear; _coinsValue = coins; }
                else error = message;
            });
            if (!catalogOk || !gearOk)
            {
                _status.text = error ?? "道具加载失败，请重试。";
                if (_retry != null) _retry.gameObject.SetActive(true);
                yield break;
            }
            _ownedGear.Clear();
            var equippedRod = string.IsNullOrEmpty(_gear.equippedRod) ? _gear.equippedTackle : _gear.equippedRod;
            foreach (var rod in rods ?? Array.Empty<ShopTackleDto>())
            {
                if (rod == null || !(Contains(_gear.ownedRods, rod.id) || Contains(_gear.ownedTackles, rod.id) || equippedRod == rod.id)) continue;
                _ownedGear.Add(new OwnedGearView
                {
                    id = rod.id, name = rod.name, kind = "rod", equipped = equippedRod == rod.id,
                    art = CozyItemArt.Product(1, rod.id, rod.name),
                    description = "已拥有的钓竿\n咬钩加成：" + (rod.biteBonus * 100f).ToString("0.#") + "%\n逃脱减免：" +
                        (rod.escapeReduction * 100f).ToString("0.#") + "%\n" + (equippedRod == rod.id ? "当前已装备" : "可装备后用于垂钓")
                });
            }
            foreach (var bait in baits ?? Array.Empty<ShopBaitDto>())
            {
                if (bait == null || !(bait.isDefaultInfinite || Contains(_gear.unlockedBaits, bait.id) || _gear.playerLevel >= Mathf.Max(1, bait.unlockPlayerLevel))) continue;
                _ownedGear.Add(new OwnedGearView
                {
                    id = bait.id, name = bait.name, kind = "bait", equipped = _gear.equippedBait == bait.id,
                    art = CozyItemArt.Product(0, bait.id, bait.name),
                    description = "已解锁的鱼饵\n" + (bait.isDefaultInfinite ? "基础鱼饵 · 无限使用" : "使用费用：" + bait.costGoldPerUse + " 金币 / 次") +
                        "\n鱼饵按实际咬钩规则消耗，不占鱼获库存。"
                });
            }
            foreach (var vessel in vessels ?? Array.Empty<ShopVesselDto>())
            {
                if (vessel == null || !Contains(_gear.ownedVessels, vessel.vesselId)) continue;
                _ownedGear.Add(new OwnedGearView
                {
                    id = vessel.vesselId, name = vessel.name, kind = "vessel",
                    art = CozyItemArt.Product(2, vessel.vesselId, vessel.name),
                    description = "已拥有的船具\n船具使用功能即将开放。"
                });
            }
            _coins.text = "金币：" + _coinsValue;
            _status.text = "已拥有 / 已解锁 " + _ownedGear.Count + " 件道具";
            RenderOwnedGear();
        }

        static bool Contains(string[] values, string id)
        {
            return values != null && Array.IndexOf(values, id) >= 0;
        }

        void RenderOwnedGear()
        {
            if (_grid == null) return;
            ApplyCategoryState();
            ClearGrid();
            var chosen = _ownedGear.Find(item => item.kind + ":" + item.id == _selectedGear);
            if (chosen == null && _ownedGear.Count > 0) chosen = _ownedGear[0];
            if (chosen == null)
            {
                ShowMessage("还没有可展示的道具。可在商店查看钓竿与鱼饵。");
                if (_equipItem != null) _equipItem.interactable = false;
                return;
            }
            _selectedGear = chosen.kind + ":" + chosen.id;
            foreach (var item in _ownedGear)
            {
                var slot = DesktopUiPrefabFactory.Instantiate("CatchSlot", _grid);
                if (slot == null) continue;
                CozyItemArt.Set(slot.transform, "DesignItemArt", item.art);
                CozyItemArt.Text(slot.transform, "Label", item.name);
                CozyItemArt.Text(slot.transform, "Meta", item.equipped ? "已装备" : "已拥有 / 已解锁");
                var backing = slot.transform.Find("DesignArtBacking");
                if (backing != null) backing.gameObject.SetActive(true);
                var button = slot.GetComponent<Button>();
                if (button == null) continue;
                DesktopModalUi.StyleTree(slot.transform);
                button.interactable = !_busy;
                button.GetComponent<CozyButtonVisual>()?.SetSelected(item == chosen);
                button.onClick.RemoveAllListeners();
                var key = item.kind + ":" + item.id;
                CozyStickerMotion.Bind(slot, "gear:" + (_api?.PlayerId ?? "") + ":" + key,
                    slot.transform.GetSiblingIndex(), _grid.parent as RectTransform);
                button.onClick.AddListener(() => { _selectedGear = key; RenderOwnedGear(); });
            }
            CozyItemArt.Set(transform, "DesignArt", chosen.art);
            CozyItemArt.Text(transform, "DesignName", chosen.name);
            CozyItemArt.Rows(transform, "DetailRows");
            _detail.text = chosen.description;
            if (_equipItem != null)
            {
                _equipItem.interactable = !_busy && !chosen.equipped && chosen.kind != "vessel";
                DesktopModalUi.SetButtonLabel(_equipItem, chosen.equipped ? "已装备" : chosen.kind == "vessel" ? "即将开放" : "装备");
            }
        }

        void EquipSelectedItem()
        {
            if (_busy || !_itemsTab || _api == null || !_api.CanUse) return;
            var item = _ownedGear.Find(value => value.kind + ":" + value.id == _selectedGear);
            if (item == null || item.equipped || item.kind == "vessel") return;
            _busy = true;
            RenderOwnedGear();
            StartCoroutine(EquipItemRoutine(item));
        }

        IEnumerator EquipItemRoutine(OwnedGearView item)
        {
            var ok = false;
            string error = null;
            Action<bool, ShopGearDto, string> completed = (success, gear, message) => { ok = success; error = message; };
            yield return item.kind == "rod" ? _api.EquipTackle(item.id, completed) : _api.EquipBait(item.id, completed);
            if (!ok)
            {
                _busy = false;
                _status.text = error ?? "装备失败，请重试。";
                CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, false, "请重试");
                RenderOwnedGear();
                yield break;
            }
            yield return LoadOwnedGear();
            _busy = false;
            RenderOwnedGear();
            CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, true, "已装备");
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
                yield return LoadOwnedGear();
                _loadRoutine = null;
                yield break;
            }
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
            _loadRoutine = null;
            if (!itemsOk)
            {
                if (_retry != null) _retry.gameObject.SetActive(true);
                _status.text = error ?? "背包加载失败。";
                _detail.text = _status.text + "\n点击重试以重新加载。";
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
            if (_itemsTab) { RenderOwnedGear(); return; }
            if (_grid == null)
                return;
            if (SelectedItem() == null)
            {
                _selectedId = null;
                if (_items.Length > 0 && _items[0] != null) Select(_items[0], false);
                else ShowMessage("背包是空的。钓到鱼并领取后会显示在这里。");
            }
            RefreshReturnButton();
            ClearGrid();
            var count = _items != null ? _items.Length : 0;
            if (_items == null || _items.Length == 0)
                ShowMessage("背包是空的。钓到鱼并领取后会显示在这里。");
            var rareOrder = 0;
            for (var i = 0; i < count; i++)
            {
                var item = _items != null && i < _items.Length ? _items[i] : null;
                var slot = DesktopUiPrefabFactory.Instantiate("CatchSlot", _grid);
                if (slot == null)
                    continue;
                slot.name = "Slot" + i;
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
                    labelText.text = label;
                    CozyItemArt.Text(slot.transform, "Meta", item == null ? "" : item.sizeM.ToString("0.00") + " m");
                }
                DesktopModalUi.StyleTree(slot.transform);
                if (slotButton != null)
                    slotButton.onClick.RemoveAllListeners();
                if (item != null)
                {
                    AddUnreadBadge(slot, CozyJournalReadState.IsUnread(_api?.PlayerId, item.id));
                    CozyStickerMotion.Bind(slot, "catch:" + (_api?.PlayerId ?? "") + ":" + item.id, i,
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
            label.text = "NEW";
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
            string returnValue;
            if (!eligible)
            {
                var needsWeight = DesktopGameData.ResolveReturnGoldMul(item.sizeM) <= 0f;
                returnHint = needsWeight
                    ? "回鱼约得：不可回（需达到 " + DesktopGameData.MinReturnWeightJin().ToString("0") + " 斤，且紫品及以上）"
                    : "回鱼约得：不可回（需紫品及以上）";
                returnValue = needsWeight
                    ? "不可回（需 " + DesktopGameData.MinReturnWeightJin().ToString("0") + " 斤+紫品）"
                    : "不可回（需紫品+）";
            }
            else
            {
                returnHint = "回鱼约得：" + returnGold + " 金币（卖价×" + (returnMul * 100f).ToString("0") + "%，" +
                             (jin > DesktopGameData.HeavyReturnWeightJin() ? ">100斤档" : "≥10斤档") + "）";
                returnValue = "约 " + returnGold + " 金币" + (CanReturnHere() ? "" : " · 需钓位");
            }
            var size = item.sizeM.ToString("0.00") + " m";
            var weight = DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(item.sizeM)) +
                         "（约 " + jin.ToString("0.##") + " 斤）";
            CozyItemArt.Text(transform, "DesignName", species);
            if (CozyItemArt.Rows(transform, "DetailRows",
                    "star", "品质", quality,
                    "hook", "体长", size,
                    "fish", "重量", weight,
                    "coin", "参考售价", sell + " 金币",
                    "pond", "回鱼", returnValue))
                _detail.text = string.Empty;
            else
                _detail.text = "鱼种：" + species +
                               "\n品质：" + quality +
                               "\n体长：" + size +
                               "\n重量：" + weight +
                               "\n参考售价：" + sell + " 金币" +
                               "\n" + returnHint +
                               "\n" + seatHint;
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
                transform.Find("JournalBagPhoto")?.GetComponent<CozyJournalStaticPose>()?.PlayReveal(new Vector2(24,0),.26f,.3f);
            }
        }

        void ShowMessage(string message)
        {
            CozyItemArt.Set(transform, "DesignArt", null);
            CozyItemArt.Text(transform, "DesignName", string.Empty);
            CozyItemArt.Rows(transform, "DetailRows");
            _detail.text = message;
        }

        void SellSelected()
        {
            if (_api == null || !_api.CanUse)
            {
                _status.text = "当前会话已失效，请重新登录。";
                return;
            }
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
                    CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, false, "请重试");
                    return;
                }
                var before = CaptureGridPositions();
                var soldIndex = Array.FindIndex(_items, value => value != null && value.id == _selectedId);
                var soldCard = soldIndex >= 0 ? _grid.Find("Slot" + soldIndex) as RectTransform : null;
                var canvas = GetComponentInParent<Canvas>();
                var layer = canvas != null ? canvas.transform.Find("MainRoot") as RectTransform : null;
                var coinTarget = layer != null ? layer.Find("Header/FishCoins/Icon") as RectTransform : null;
                CozyCardFragments.Tear(soldCard, layer);
                CozyActionFeedback.CoinsFly(soldCard, coinTarget, layer);
                _items = items ?? new FishInventoryItemDto[0];
                _coinsValue = total;
                _coins.text = "金币：" + _coinsValue;
                _selectedId = null;
                _status.text = "出售成功，获得 " + earned + " 金币。";
                ShowMessage(_status.text);
                RenderGrid();
                Canvas.ForceUpdateCanvases();
                for (var i = 0; i < _items.Length; i++)
                    if (_items[i] != null && before.TryGetValue(_items[i].id, out var previous))
                        _grid.Find("Slot" + i)?.GetComponent<CozyStickerMotion>()?.ApplyReflow(previous);
            }));
        }

        Dictionary<string, Vector3> CaptureGridPositions()
        {
            var positions = new Dictionary<string, Vector3>();
            for (var i = 0; i < _items.Length; i++)
            {
                var slot = _grid.Find("Slot" + i);
                if (slot != null && _items[i] != null) positions[_items[i].id] = slot.position;
            }
            return positions;
        }

        void ReturnSelected()
        {
            if (_api == null || !_api.CanUse)
            {
                _status.text = "当前会话已失效，请重新登录。";
                return;
            }
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
                        CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, false, "请重试");
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
                    ShowMessage(_status.text);
                    RenderGrid();
                    CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, true, "已回鱼");
                }));
        }

        void ShareSelected()
        {
            if (_api == null || !_api.CanUse)
            {
                _status.text = "当前会话已失效，请重新登录。";
                return;
            }
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
                CozyActionFeedback.Play(transform.Find("DesignArt") ?? transform, ok, ok ? "已分享" : "请重试");
            }));
        }
    }
}
