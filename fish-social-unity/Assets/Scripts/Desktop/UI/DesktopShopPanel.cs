using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Steam shop page. The prefab owns the page shell; this view only binds
    /// server catalog/gear data and forwards mutations to the authenticated API.
    /// </summary>
    public sealed class DesktopShopPanel : MonoBehaviour
    {
        bool _uiReady;
        IAuthenticatedApiClient _api;
        Text _heading;
        Text _coins;
        Text _equipped;
        Text _status;
        Text _details;
        Button _buy;
        Button _equip;
        Button _retry;
        Button _browseShop;
        public Action ShopRequested;
        RectTransform _content;
        readonly RectTransform[] _tabRoots = new RectTransform[3];
        readonly bool[] _tabBuilt = new bool[3];
        bool _catalogReady;
        bool _shopTabsStyled;
        Button _baitTab;
        Button _tackleTab;
        Button _vesselTab;
        ShopBaitDto[] _baits = new ShopBaitDto[0];
        ShopTackleDto[] _tackles = new ShopTackleDto[0];
        ShopVesselDto[] _vessels = new ShopVesselDto[0];
        ShopGearDto _gear = new ShopGearDto();
        int _coinBalance;
        int _tab = 1;
        bool _busy;
        bool _loading;
        int _selectedIndex = -1;
        string _detailMotionKey;
        bool _equipmentMode;
        Coroutine _refreshRoutine;

        public void Bind(IAuthenticatedApiClient api)
        {
            _api = api;
            EnsureUi();
            SetStatus(_equipmentMode ? DesktopLocalization.Text("ui.shop.status.equipment_not_loaded", "尚未加载装备。") : DesktopLocalization.Text("ui.shop.status.shop_not_loaded", "尚未加载商店。"));
            Render();
        }

        public void SetEquipmentMode(bool equipmentMode)
        {
            if (_equipmentMode != equipmentMode)
            {
                _equipmentMode = equipmentMode;
                _tab = 1;
                _selectedIndex = -1;
                _tabBuilt[0] = _tabBuilt[1] = _tabBuilt[2] = false;
            }
            EnsureUi();
            SetStatus(_equipmentMode ? DesktopLocalization.Text("ui.shop.hint.select_owned_equipment", "选择已拥有的钓竿或已解锁的鱼饵进行装备。") : DesktopLocalization.Text("ui.shop.hint.select_product", "选择商品查看详情。"));
            Render();
        }

        public void OnOpened()
        {
            if (_catalogReady)
            {
                ShowShopTab();
                RenderContent(false);
                return;
            }
            _detailMotionKey = null;
            if (!_uiReady || _busy)
                return;
            Refresh();
        }

        /// <summary>
        /// Editor-only entry point. Runtime binds the authored prefab without rebuilding its layout.
        /// </summary>
        public void BuildEditorLayout()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            EnsureUi();
#endif
        }

        public void OnClosed()
        {
            StopAllCoroutines();
            _refreshRoutine = null;
            _busy = false;
            _loading = false;
        }

        void EnsureUi()
        {
            _uiReady = false;
            _coins = Find<Text>("Header/Coins");
            _equipped = Find<Text>("Header/Equipped");
            _status = Find<Text>("Status");
            _details = Find<Text>("Details/DescriptionScroll/Description") ?? Find<Text>("Details/Description");
            _buy = Find<Button>("Details/Buy");
            _equip = Find<Button>("Details/Equip");
            _retry = Find<Button>("Details/Retry");
            _browseShop = Find<Button>("Details/BrowseShop");
            if(_browseShop != null)
            {
                _browseShop.onClick.RemoveAllListeners();
                _browseShop.onClick.AddListener(()=>{if(!_busy && !_loading) ShopRequested?.Invoke();});
            }
            _content = transform.Find("Items/Viewport/Content") as RectTransform;
            _baitTab = Find<Button>("Tabs/Bait");
            _tackleTab = Find<Button>("Tabs/Tackle");
            _vesselTab = Find<Button>("Tabs/Vessel");
            if (_coins == null || _equipped == null || _status == null ||
                _details == null || _buy == null || _equip == null ||
                _content == null || _baitTab == null || _tackleTab == null || _vesselTab == null ||
                Find<Text>("Header/Title") == null)
            {
                Debug.LogError("[DesktopUI] PanelShop requires Header/{Title,Coins,Equipped}, Tabs/{Bait,Tackle,Vessel}, Status, Items/Viewport/Content and Details/{Description,Buy,Equip}.", this);
                return;
            }
            _uiReady = true;
            RestyleUi();
            if (_retry != null)
                _retry.gameObject.SetActive(false);

            DesktopModalUi.SetButtonLabel(_baitTab, DesktopLocalization.Text("ui.shop.tab.bait", "鱼饵"));
            DesktopModalUi.SetButtonLabel(_tackleTab, DesktopLocalization.Text("ui.shop.tab.rod", "钓竿"));
            DesktopModalUi.SetButtonLabel(_vesselTab, DesktopLocalization.Text("ui.shop.tab.vessel", "船具"));
            _baitTab.onClick.RemoveAllListeners();
            _baitTab.onClick.AddListener(() => SelectTab(0));
            _tackleTab.onClick.RemoveAllListeners();
            _tackleTab.onClick.AddListener(() => SelectTab(1));
            if (_vesselTab != null)
            {
                _vesselTab.onClick.RemoveAllListeners();
                _vesselTab.onClick.AddListener(() => SelectTab(2));
            }
            _buy.onClick.RemoveAllListeners();
            _buy.onClick.AddListener(BuySelected);
            _equip.onClick.RemoveAllListeners();
            _equip.onClick.AddListener(EquipSelected);
            if (_retry != null)
            {
                _retry.onClick.RemoveAllListeners();
                _retry.onClick.AddListener(Refresh);
            }
            SetActionButtons(false, false, false);
        }

        void RestyleUi()
        {
            _heading = Find<Text>("Header/Title");
            if (_heading != null)
                _heading.text = _equipmentMode ? DesktopLocalization.Text("ui.shop.title.my_equipment", "我的装备") : DesktopLocalization.Text("ui.shop.title.fishing_shop", "钓鱼商店");
            DesktopModalUi.StyleTree(transform);
            DesktopModalUi.StyleButton(_baitTab, "leaf");
            DesktopModalUi.StyleButton(_tackleTab, "rod");
            DesktopModalUi.StyleButton(_vesselTab, "pond");
            DesktopModalUi.StyleButton(_buy, "coin", true);
            DesktopModalUi.StyleButton(_equip, "bag");
            TintTab(_baitTab, _tab == 0);
            TintTab(_tackleTab, _tab == 1);
            TintTab(_vesselTab, _tab == 2);
        }

        void Refresh()
        {
            if (!_uiReady || _busy)
                return;
            if (_refreshRoutine != null)
                StopCoroutine(_refreshRoutine);
            _loading = true;
            if (_buy != null) _buy.interactable = false;
            if (_equip != null) _equip.interactable = false;
            _refreshRoutine = StartCoroutine(RefreshRoutine());
        }

        IEnumerator RefreshRoutine()
        {
            if (_api == null || !_api.CanUse)
            {
                _loading = false;
                SetStatus(DesktopLocalization.Text("ui.shop.error.session_missing", "当前没有有效的 Steam 会话，请重新登录。"));
                yield break;
            }

            if (_retry != null) _retry.gameObject.SetActive(false);
            SetStatus(_equipmentMode ? DesktopLocalization.Text("ui.shop.status.loading_equipment", "正在加载可用装备和当前状态…") : DesktopLocalization.Text("ui.shop.status.loading_shop", "正在加载商店目录和装备状态…"));
            var catalogDone = false;
            var catalogOk = false;
            string catalogError = null;
            yield return StartCoroutine(_api.GetShopCatalog(
                (ok, baits, tackles, vessels, error) =>
                {
                    catalogOk = ok;
                    catalogError = error;
                    if (ok)
                    {
                        _baits = baits ?? new ShopBaitDto[0];
                        _tackles = tackles ?? new ShopTackleDto[0];
                        _vessels = vessels ?? new ShopVesselDto[0];
                    }
                    catalogDone = true;
                }));
            while (!catalogDone)
                yield return null;

            var gearDone = false;
            var gearOk = false;
            string gearError = null;
            yield return StartCoroutine(_api.GetShopGear(
                (ok, gear, coins, error) =>
                {
                    gearOk = ok;
                    gearError = error;
                    if (ok)
                    {
                        _gear = gear ?? new ShopGearDto();
                        _coinBalance = coins;
                    }
                    gearDone = true;
                }));
            while (!gearDone)
                yield return null;

            _refreshRoutine = null;
            _loading = false;
            if (!catalogOk || !gearOk)
            {
                if (_retry != null) _retry.gameObject.SetActive(true);
                SetStatus(!string.IsNullOrEmpty(catalogError)
                    ? catalogError
                    : (gearError ?? (_equipmentMode ? DesktopLocalization.Text("ui.shop.error.equipment_load_failed", "装备加载失败，请稍后重新打开页面。") : DesktopLocalization.Text("ui.shop.error.shop_load_failed", "商店加载失败，请稍后重新打开页面。"))));
                Render();
                yield break;
            }

            SetStatus(_equipmentMode ? DesktopLocalization.Text("ui.shop.status.equipment_synced", "装备已同步，可选择钓竿或鱼饵。") : DesktopLocalization.Text("ui.shop.status.shop_synced", "商店数据已同步。"));
            _catalogReady = true;
            _tabBuilt[0] = _tabBuilt[1] = _tabBuilt[2] = false;
            Render();
        }

        Coroutine _tabMotionRoutine;
        void SelectTab(int tab)
        {
            if (_equipmentMode && tab == 2)
                return;
            if (_tab == tab) return;
            if (!Application.isPlaying)
            {
                _tab = tab;
                _selectedIndex = -1;
                TintTab(_baitTab, tab == 0); TintTab(_tackleTab, tab == 1); TintTab(_vesselTab, tab == 2);
                ShowShopTab();
                if (_tabBuilt[tab]) RenderContent(false);
                else if (_catalogReady) RenderCurrent();
                return;
            }
            if (_tabMotionRoutine != null) StopCoroutine(_tabMotionRoutine);
            _tabMotionRoutine = StartCoroutine(ChangeCategory(tab));
        }

        IEnumerator ChangeCategory(int tab)
        {
            var motion = GetComponent<CozyJournalPageMotion>();
            if (motion != null) yield return motion.Exit();
            var built = _tabBuilt[tab];
            _tab = tab;
            _selectedIndex = -1;
            TintTab(_baitTab, tab == 0); TintTab(_tackleTab, tab == 1); TintTab(_vesselTab, tab == 2);
            ShowShopTab();
            if (built)
            {
                RenderContent(false);
                if (_content != null)
                {
                    var stickers = _content.GetComponentsInChildren<CozyStickerMotion>();
                    for (var i = 0; i < stickers.Length; i++)
                        stickers[i].RestartEntrance();
                }
            }
            else if (_catalogReady)
                RenderCurrent();
            motion?.PlayEntrance(_content != null ? _content.childCount : 0);
            _tabMotionRoutine = null;
        }

        void BuySelected()
        {
            if (_busy || _loading) return;
            if (_equipmentMode)
            {
                SetStatus(DesktopLocalization.Text("ui.shop.hint.equipment_page_policy", "装备页仅切换已拥有的钓竿和已解锁的鱼饵，不购买商品。"));
                return;
            }
            if (_api == null || !_api.CanUse)
            {
                SetStatus(DesktopLocalization.Text("ui.shop.error.session_expired", "当前会话已失效，请重新登录。"));
                return;
            }
            if (_tab == 0)
            {
                SetStatus(DesktopLocalization.Text("ui.shop.hint.bait_unlock_policy", "鱼饵按钓鱼等级解锁，咬钩成功时自动选用并按次扣金，商店不进货。"));
                return;
            }
            if (_tab == 2)
            {
                var vessel = SelectedVessel();
                if (vessel == null)
                    return;
                StartCoroutine(Mutate(BuyVesselRoutine(vessel)));
                return;
            }
            var tackle = SelectedTackle();
            if (tackle == null || tackle.price <= 0)
                return;
            StartCoroutine(Mutate(BuyTackleRoutine(tackle)));
        }

        IEnumerator Mutate(IEnumerator request)
        {
            _busy = true;
            SetActionButtons(_buy != null && _buy.gameObject.activeSelf, false, false);
            try
            {
                yield return request;
            }
            finally
            {
                _busy = false;
                if (this != null && isActiveAndEnabled) Render();
            }
        }

        IEnumerator BuyBaitRoutine(ShopBaitDto bait)
        {
            SetStatus(DesktopLocalization.Format("ui.shop.status.purchasing", (object)(bait.name)));
            var done = false;
            yield return StartCoroutine(_api.BuyBait(bait.id, 1,
                (ok, gear, coins, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        _coinBalance = coins;
                        SetStatus(DesktopLocalization.Text("ui.shop.status.bait_purchase_success", "购买成功，装备状态已由服务端刷新。"));
                        CozyActionFeedback.Play(transform.Find("Details"), true, DesktopLocalization.Text("ui.shop.feedback.purchased", "已购买"));
                    }
                    else
                    {
                        SetStatus(message ?? DesktopLocalization.Text("ui.shop.error.bait_purchase_failed", "购买失败，当前装备未修改。"));
                        CozyActionFeedback.Play(transform.Find("Details"), false, "请重试");
                    }
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        IEnumerator BuyTackleRoutine(ShopTackleDto tackle)
        {
            SetStatus(DesktopLocalization.Format("ui.shop.status.purchasing", (object)(tackle.name)));
            var done = false;
            yield return StartCoroutine(_api.BuyTackle(tackle.id,
                (ok, gear, coins, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        _coinBalance = coins;
                        SetStatus(DesktopLocalization.Text("ui.shop.status.tackle_purchase_success", "购买成功，金币和装备状态已由服务端刷新。"));
                        CozyActionFeedback.Play(transform.Find("Details"), true, DesktopLocalization.Text("ui.shop.feedback.purchased", "已购买"));
                    }
                    else
                    {
                        SetStatus(message ?? DesktopLocalization.Text("ui.shop.error.tackle_purchase_failed", "购买失败，金币和装备未修改。"));
                        CozyActionFeedback.Play(transform.Find("Details"), false, "请重试");
                    }
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        IEnumerator BuyVesselRoutine(ShopVesselDto vessel)
        {
            SetStatus(DesktopLocalization.Format("ui.shop.status.purchasing", (object)(vessel.name)));
            var done = false;
            yield return StartCoroutine(_api.BuyVessel(vessel.vesselId,
                (ok, gear, coins, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        _coinBalance = coins;
                        SetStatus(DesktopLocalization.Text("ui.shop.status.vessel_purchased_unavailable", "已购买。船具暂不可使用（即将开放）。"));
                        CozyActionFeedback.Play(transform.Find("Details"), true, DesktopLocalization.Text("ui.shop.feedback.purchased", "已购买"));
                    }
                    else
                    {
                        SetStatus(message ?? DesktopLocalization.Text("ui.shop.error.tackle_purchase_failed", "购买失败，金币和装备未修改。"));
                        CozyActionFeedback.Play(transform.Find("Details"), false, "请重试");
                    }
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        void EquipSelected()
        {
            if (_busy || _loading) return;
            if (_api == null || !_api.CanUse)
            {
                SetStatus(DesktopLocalization.Text("ui.shop.error.session_expired", "当前会话已失效，请重新登录。"));
                return;
            }
            if (_equipmentMode && _tab == 0)
            {
                var bait = SelectedBait();
                if (bait != null && BaitUnlocked(bait) && !IsEquippedBait(bait.id))
                    StartCoroutine(Mutate(EquipBaitRoutine(bait)));
                return;
            }
            if (_tab != 1)
            {
                SetStatus(_tab == 2
                    ? DesktopLocalization.Text("ui.shop.hint.vessel_unavailable", "船具即将开放，无法装备使用。")
                    : DesktopLocalization.Text("ui.shop.hint.bait_auto_selection", "进阶饵在咬钩时按鱼种自动选用，无需手动装备。"));
                return;
            }
            var tackle = SelectedTackle();
            if (tackle != null && OwnsRod(tackle.id) && !IsEquippedRod(tackle.id))
                StartCoroutine(Mutate(EquipTackleRoutine(tackle)));
        }

        IEnumerator EquipBaitRoutine(ShopBaitDto bait)
        {
            SetStatus(DesktopLocalization.Format("ui.shop.status.equipping", (object)(bait.name)));
            var done = false;
            yield return StartCoroutine(_api.EquipBait(bait.id,
                (ok, gear, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        SetStatus(DesktopLocalization.Text("ui.shop.status.bait_equipped", "鱼饵已装备。"));
                        CozyActionFeedback.Play(transform.Find("Details"), true, DesktopLocalization.Text("ui.shop.feedback.equipped", "已装备"));
                    }
                    else
                    {
                        SetStatus(message ?? DesktopLocalization.Text("ui.shop.error.equip_failed", "装备失败，当前装备未修改。"));
                        CozyActionFeedback.Play(transform.Find("Details"), false, "请重试");
                    }
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        IEnumerator EquipTackleRoutine(ShopTackleDto tackle)
        {
            SetStatus(DesktopLocalization.Format("ui.shop.status.equipping", (object)(tackle.name)));
            var done = false;
            yield return StartCoroutine(_api.EquipTackle(tackle.id,
                (ok, gear, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        SetStatus(DesktopLocalization.Text("ui.shop.status.rod_equipped", "钓竿已装备。"));
                        CozyActionFeedback.Play(transform.Find("Details"), true, DesktopLocalization.Text("ui.shop.feedback.equipped", "已装备"));
                    }
                    else
                    {
                        SetStatus(message ?? DesktopLocalization.Text("ui.shop.error.equip_failed", "装备失败，当前装备未修改。"));
                        CozyActionFeedback.Play(transform.Find("Details"), false, "请重试");
                    }
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        void Render()
        {
            for (var i = 0; i < _tabBuilt.Length; i++)
                if (i != _tab)
                    _tabBuilt[i] = false;
            RenderCurrent();
        }

        void RenderCurrent()
        {
            ShowShopTab();
            RenderContent(true);
            _tabBuilt[_tab] = true;
        }

        void ShowShopTab()
        {
            if (_content == null)
                return;
            if (_tabRoots[_tab] == null)
            {
                var any = _tabRoots[0] != null || _tabRoots[1] != null || _tabRoots[2] != null;
                _tabRoots[_tab] = any ? CozyKeptPage.DuplicateList(_content, "ShopTab" + _tab) : _content;
            }
            var root = _tabRoots[_tab];
            _content = root;
            var scroll = root.GetComponentInParent<ScrollRect>();
            if (scroll != null)
                scroll.content = root;
            if (!root.gameObject.activeSelf)
                root.gameObject.SetActive(true);
            for (var i = 0; i < _tabRoots.Length; i++)
            {
                if (i == _tab || _tabRoots[i] == null || !_tabRoots[i].gameObject.activeSelf)
                    continue;
                _tabRoots[i].gameObject.SetActive(false);
            }
        }

        void RenderContent(bool rebuildCards)
        {
            if (_coins == null || _equipped == null || _details == null || _content == null)
                return;
            _coins.text = DesktopLocalization.Format("ui.shop.balance.coins", (object)(_coinBalance));
            _equipped.text = _equipmentMode
                ? DesktopLocalization.Format("ui.shop.summary.equipped_rod_and_bait", (object)(EquippedRodName()), (object)(EquippedBaitName()), (object)(Math.Max(1, _gear.playerLevel)))
                : DesktopLocalization.Format("ui.shop.summary.equipped_rod", (object)(EquippedRodName()), (object)(Math.Max(1, _gear.playerLevel)));
            TintTab(_baitTab, _tab == 0);
            TintTab(_tackleTab, _tab == 1);
            if (_vesselTab != null)
                TintTab(_vesselTab, _tab == 2);
            _shopTabsStyled = true;

            var visibleCount = _content.childCount;
            if (rebuildCards)
            {
                visibleCount = 0;
            for (var i = _content.childCount - 1; i >= 0; i--)
            {
                _content.GetChild(i).gameObject.SetActive(false);
                Destroy(_content.GetChild(i).gameObject);
            }

            var total=_tab==0?_baits.Length:_tab==1?_tackles.Length:_vessels.Length;
            if(_selectedIndex<0 && total>0) _selectedIndex=0;
            CozyItemArt.Text(transform,"DesignItemsHeading",DesktopLocalization.Format("ui.shop.category.item_count", (object)((_tab==0?DesktopLocalization.Text("ui.shop.section.bait", "垂钓补给"):_tab==1?DesktopLocalization.Text("ui.shop.section.rods", "顺手的竿"):DesktopLocalization.Text("ui.shop.section.vessels", "出海装备"))), (object)(total)));
            if (_tab == 0)
            {
                for (var i = 0; i < _baits.Length; i++)
                {
                    var unlocked = BaitUnlocked(_baits[i]);
                    if (_equipmentMode && !unlocked)
                        continue;
                    var cost = BaitCost(_baits[i]);
                    CreateCard(i, _baits[i].id, _baits[i].name, "icons/leaf.png",
                        unlocked
                            ? (_equipmentMode && IsEquippedBait(_baits[i].id) ? DesktopLocalization.Text("ui.shop.badge.equipped", "使用中 · ") : DesktopLocalization.Text("ui.shop.badge.unlocked", "已解锁 · ")) +
                              (cost <= 0 ? DesktopLocalization.Text("ui.shop.badge.unlimited_free", "无限 · 不扣金") : DesktopLocalization.Format("ui.shop.price.per_use", (object)(cost)))
                            : DesktopLocalization.Format("ui.shop.requirement.unlock_level", (object)(BaitUnlockLevel(_baits[i]))));
                    visibleCount++;
                }
            }
            else if (_tab == 1)
            {
                for (var i = 0; i < _tackles.Length; i++)
                {
                    var owned = OwnsRod(_tackles[i].id);

                    var subType = RodSubType(_tackles[i]);
                    var price = RodPrice(_tackles[i]);
                    CreateCard(i, _tackles[i].id, _tackles[i].name, "icons/rod.png",
                        _equipmentMode ? owned ? subType : DesktopLocalization.Text("ui.shop.badge.shop_purchase", "商店购买") : owned ? DesktopLocalization.Text("ui.shop.badge.owned", "已拥有") : price <= 0 ? DesktopLocalization.Text("ui.shop.badge.starter_gift", "新手赠送") : price.ToString("N0"));
                    visibleCount++;
                }
            }
            else if (!_equipmentMode)
            {
                for (var i = 0; i < _vessels.Length; i++)
                {
                    var owned = Contains(_gear.ownedVessels, _vessels[i].vesselId);
                    CreateCard(i, _vessels[i].vesselId, _vessels[i].name, "icons/pond.png",
                        owned ? DesktopLocalization.Text("ui.shop.badge.owned_unavailable", "已拥有 · 暂不可用") :
                        DesktopLocalization.Format("ui.shop.price.level_and_coins", (object)(_vessels[i].unlockPlayerLevel), (object)(_vessels[i].priceGold)));
                    visibleCount++;
                }
            }

            }
            else
            {
                foreach (Transform child in _content)
                {
                    var visual = child.GetComponent<CozyButtonVisual>();
                    if (visual != null) visual.SetSelected(child.name == "Item_" + _selectedIndex);
                }
            }

            var selectedBait = SelectedBait();
            var selectedArt = _tab == 0 && selectedBait != null
                ? CozyItemArt.Product(0, selectedBait.id, selectedBait.name)
                : _tab == 1 && SelectedTackle() != null
                    ? CozyItemArt.Product(1, SelectedTackle().id, SelectedTackle().name)
                    : _tab == 2 && SelectedVessel() != null
                        ? CozyItemArt.Product(2, SelectedVessel().vesselId, SelectedVessel().name) : null;
            CozyItemArt.Set(transform, "Details/DesignArt", selectedArt);
            var selectedTackle = SelectedTackle();
            var selectedVessel = SelectedVessel();
            if (_tab == 0 && selectedBait != null)
            {
                ShowDetails(selectedBait.name, BuildBaitDetails(selectedBait));
                SetActionButtons(false, false, _equipmentMode && BaitUnlocked(selectedBait) &&
                    !IsEquippedBait(selectedBait.id));
            }
            else if (_tab == 1 && selectedTackle != null)
            {
                var owned = OwnsRod(selectedTackle.id);
                ShowDetails(selectedTackle.name, BuildRodDetails(selectedTackle, owned));
                SetActionButtons(true, !owned && RodPrice(selectedTackle) > 0,
                    owned && !IsEquippedRod(selectedTackle.id));
            }
            else if (_tab == 2 && selectedVessel != null)
            {
                var owned = Contains(_gear.ownedVessels, selectedVessel.vesselId);
                var levelOk = _gear.playerLevel >= selectedVessel.unlockPlayerLevel;
                ShowDetails(selectedVessel.name, BuildVesselDetails(selectedVessel, owned));
                SetActionButtons(true, !owned && levelOk, false);
            }
            else
            {
                ShowDetails(null, _equipmentMode
                    ? (visibleCount == 0
                        ? (_tab == 0 ? DesktopLocalization.Text("ui.shop.empty.no_unlocked_bait", "暂无已解锁的鱼饵。提升钓鱼等级后可在这里选择。") : DesktopLocalization.Text("ui.shop.empty.no_owned_rods", "暂无已拥有的钓竿。获得钓竿后可在这里装备。"))
                        : DesktopLocalization.Text("ui.shop.hint.equipment_selection_policy", "选择已拥有的钓竿或已解锁的鱼饵查看详情，再点击装备。\n\n装备页不购买商品；鱼饵仍按原有规则在咬钩成功时扣金。"))
                    : _tab == 0
                    ? DesktopLocalization.Text("ui.shop.hint.bait_details", "选择鱼饵查看食性、解锁等级和每次扣金。鱼饵不进货，没有库存。")
                    : (_tab == 2
                        ? DesktopLocalization.Text("ui.shop.hint.vessel_details", "选择船具查看购入条件。购买后暂不可使用。")
                        : DesktopLocalization.Text("ui.shop.hint.select_product_details", "请选择商品查看详情。")));
                SetActionButtons(_tab != 0, false, false);
            }
            RevealDetail();
        }

        void RevealDetail()
        {
            var key = _equipmentMode + ":" + _tab + ":" + _selectedIndex;
            if (key == _detailMotionKey)
                return;
            _detailMotionKey = key;
            var polaroid = transform.Find("Details/JournalPolaroid");
            if (polaroid != null)
                polaroid.GetComponent<CozyJournalStaticPose>()?.PlayReveal(new Vector2(24f, 0f), .26f, .3f);
            else
                CozyJournalStaticPose.RevealAside(transform.Find("Details"));
        }

        void ShowDetails(string name, string text)
        {
            var nameLabel = Find<Text>("Details/DesignName");
            if (nameLabel != null)
            {
                nameLabel.text = name ?? string.Empty;
                if (!string.IsNullOrEmpty(name) && text.StartsWith(name + "\n", StringComparison.Ordinal))
                    text = text.Substring(name.Length + 1);
            }
            _details.text = text;
        }

        void SetActionButtons(bool showBuy, bool buyEnabled, bool equipEnabled)
        {
            var selectedRod=SelectedTackle();
            var isOwned=selectedRod!=null && OwnsRod(selectedRod.id);
            if(_browseShop!=null)
            {
                _browseShop.gameObject.SetActive(_equipmentMode && selectedRod!=null && !isOwned);
                _browseShop.interactable=!_busy && !_loading;
            }
            showBuy = showBuy && !_equipmentMode;
            if (_buy != null)
            {
                _buy.gameObject.SetActive(showBuy);
                _buy.interactable = showBuy && buyEnabled && !_busy && !_loading;
                DesktopModalUi.SetButtonLabel(_buy, selectedRod!=null ? isOwned ? DesktopLocalization.Text("ui.shop.badge.owned", "已拥有") : DesktopLocalization.Format("ui.shop.button.buy_with_price", (object)(RodPrice(selectedRod).ToString("N0"))) : DesktopLocalization.Text("ui.shop.button.buy", "购买"));
            }
            if (_equip != null)
            {
                var showEquip = equipEnabled || (_equipmentMode && isOwned);
                _equip.gameObject.SetActive(showEquip);
                _equip.interactable = equipEnabled && !_busy && !_loading;
                DesktopModalUi.SetButtonLabel(_equip, selectedRod!=null && IsEquippedRod(selectedRod.id) ? DesktopLocalization.Text("ui.shop.button.equipped", "使用中") : DesktopLocalization.Text("ui.shop.button.equip", "装备"));
            }
        }

        string EquippedBaitName()
        {
            for (var i = 0; i < _baits.Length; i++)
                if (_baits[i].id == _gear.equippedBait && !string.IsNullOrEmpty(_baits[i].name))
                    return _baits[i].name;
            var local = DesktopGameData.GetBait(_gear.equippedBait);
            return local != null && !string.IsNullOrEmpty(local.name)
                ? local.name : (string.IsNullOrEmpty(_gear.equippedBait) ? DesktopLocalization.Text("ui.shop.equipment.none", "无") : _gear.equippedBait);
        }

        bool IsEquippedBait(string baitId)
        {
            return !string.IsNullOrEmpty(baitId) && _gear.equippedBait == baitId;
        }

        string EquippedRodName()
        {
            if (string.IsNullOrEmpty(_gear.equippedRod))
                return DesktopLocalization.Text("ui.shop.equipment.none", "无");
            var local = DesktopGameData.GetRod(_gear.equippedRod);
            if (local != null && !string.IsNullOrEmpty(local.name))
                return local.name;
            for (var i = 0; i < _tackles.Length; i++)
            {
                if (_tackles[i].id == _gear.equippedRod && !string.IsNullOrEmpty(_tackles[i].name))
                    return _tackles[i].name;
            }
            return _gear.equippedRod;
        }

        bool OwnsRod(string rodId)
        {
            if (string.IsNullOrEmpty(rodId))
                return false;
            if (Contains(_gear.ownedRods, rodId))
                return true;
            return IsEquippedRod(rodId);
        }

        bool IsEquippedRod(string rodId)
        {
            return !string.IsNullOrEmpty(rodId) && _gear.equippedRod == rodId;
        }

        bool BaitUnlocked(ShopBaitDto bait)
        {
            var def = DesktopGameData.GetBait(bait.id);
            if (def != null && def.isDefaultInfinite)
                return true;
            if (bait.isDefaultInfinite)
                return true;
            if (Contains(_gear.unlockedBaits, bait.id))
                return true;
            return _gear.playerLevel >= BaitUnlockLevel(bait);
        }

        int BaitUnlockLevel(ShopBaitDto bait)
        {
            var def = DesktopGameData.GetBait(bait.id);
            return def != null ? def.unlockPlayerLevel : bait.unlockPlayerLevel;
        }

        int BaitCost(ShopBaitDto bait)
        {
            var def = DesktopGameData.GetBait(bait.id);
            return def != null ? def.costGoldPerUse : bait.costGoldPerUse;
        }

        string RodSubType(ShopTackleDto rod)
        {
            var def = DesktopGameData.GetRod(rod.id);
            if (def != null && !string.IsNullOrEmpty(def.subType))
                return def.subType;
            return string.IsNullOrEmpty(rod.subType) ? DesktopLocalization.Text("ui.shop.tab.rod", "钓竿") : rod.subType;
        }

        int RodPrice(ShopTackleDto rod)
        {
            var def = DesktopGameData.GetRod(rod.id);
            return def != null ? def.priceGold : rod.price;
        }

        string BuildBaitDetails(ShopBaitDto bait)
        {
            var def = DesktopGameData.GetBait(bait.id);
            var diet = def != null ? def.diet : bait.diet;
            var unlock = BaitUnlockLevel(bait);
            var cost = BaitCost(bait);
            var infinite = def != null ? def.isDefaultInfinite : bait.isDefaultInfinite;
            var herb = def != null ? def.biteBonusHerbivore : bait.biteBonusHerbivore;
            var omni = def != null ? def.biteBonusOmnivore : bait.biteBonusOmnivore;
            var carn = def != null ? def.biteBonusCarnivore : bait.biteBonusCarnivore;
            var unlocked = BaitUnlocked(bait);
            var text = new StringBuilder();
            text.Append(bait.name).Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.bait_details.diet", "对口食性：")).Append(DesktopGameData.DietLabel(diet));
            if (diet == "herbivore")
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.herbivore_examples", "（鲫、鲤、罗非等）"));
            else if (diet == "omnivore")
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.omnivore_examples", "（锦鲤、鲈、鲶等）"));
            else if (diet == "carnivore")
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.carnivore_examples", "（黑鲈、翘嘴、鳟等）"));
            else
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.all_species", "（任意鱼种兜底）"));
            text.Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.bait_details.unlock_level", "解锁等级：钓鱼 Lv.")).Append(unlock);
            if (infinite)
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.starter_unlimited", "（新手默认，永不短缺）"));
            text.Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.bait_details.cost_per_use", "每次消耗：")).Append(cost <= 0 ? DesktopLocalization.Text("ui.shop.bait_details.no_coin_cost", "不扣金") : DesktopLocalization.Format("ui.shop.bait_details.coin_cost", (object)(cost)));
            text.Append(DesktopLocalization.Text("ui.shop.bait_details.charge_on_bite", "（仅在咬钩成功时扣除）\n"));
            text.Append(DesktopLocalization.Text("ui.shop.bait_details.herbivore_bonus", "咬钩加成：草食 ")).Append(DesktopGameData.FormatPct(herb));
            text.Append(DesktopLocalization.Text("ui.shop.bait_details.omnivore_bonus", "  /  杂食 ")).Append(DesktopGameData.FormatPct(omni));
            text.Append(DesktopLocalization.Text("ui.shop.bait_details.carnivore_bonus", "  /  肉食 ")).Append(DesktopGameData.FormatPct(carn)).Append('\n');
            text.Append('\n');
            if (_equipmentMode)
            {
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.equip_without_stock", "选择：仅装备已解锁鱼饵，不购买、不增加库存。\n"));
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.auto_select_and_fallback", "咬钩时仍按原有规则自动选饵和扣金；金币不够则回退到基础杂饵。\n"));
                text.Append(IsEquippedBait(bait.id) ? DesktopLocalization.Text("ui.shop.bait_details.currently_equipped", "当前选择：使用中。\n") : DesktopLocalization.Text("ui.shop.bait_details.available_to_equip", "当前选择：可装备。\n"));
            }
            else
            {
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.automatic_use_policy", "用法：不进货、不显示库存。咬钩时按鱼的食性自动选用已解锁对口饵；"));
                text.Append(DesktopLocalization.Text("ui.shop.bait_details.basic_bait_fallback", "金币不够或未解锁则回退到基础杂饵。\n"));
            }
            text.Append(unlocked ? DesktopLocalization.Text("ui.shop.bait_details.auto_use_unlocked", "当前状态：已解锁，可自动选用。") : DesktopLocalization.Text("ui.shop.bait_details.level_locked", "当前状态：等级不足，暂不会被选用。"));
            return text.ToString();
        }

        string BuildRodDetails(ShopTackleDto rod, bool owned)
        {
            var def = DesktopGameData.GetRod(rod.id);
            var subType = RodSubType(rod);
            var price = RodPrice(rod);
            var bite = def != null ? def.biteBonus : rod.biteBonus;
            var escape = def != null ? def.escapeReduction : rod.escapeReduction;
            var breakM = def != null ? def.breakSizeM : rod.breakSizeM;
            var breakN = def != null ? def.breakMaxLandings : rod.breakMaxLandings;
            float Fit(float fromDef, float fromDto)
            {
                return def != null ? fromDef : fromDto;
            }

            var text = new StringBuilder();
            text.Append(rod.name).Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.type", "类型：")).Append(subType).Append('\n');
            if (!_equipmentMode)
                text.Append(DesktopLocalization.Text("ui.shop.rod_details.price", "价格：")).Append(price <= 0 ? DesktopLocalization.Text("ui.shop.rod_details.starter_gift_policy", "新手赠送（0 金，不可回购）") : DesktopLocalization.Format("ui.shop.bait_details.coin_cost", (object)(price))).Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.bite_bonus", "咬钩加成：")).Append(DesktopGameData.FormatPct(bite)).Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.escape_reduction", "防脱：")).Append(DesktopGameData.FormatPct(escape)).Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.oversize_threshold", "超规格阈值：")).Append(breakM.ToString("0.##")).Append("m\n");
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.oversize_limit", "超规格成功上限：")).Append(breakN).Append(DesktopLocalization.Text("ui.shop.rod_details.oversize_break_policy", " 次（满则销毁当前竿，不能修理）\n\n"));
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.quality_fit", "品质适配（乘区，越接近 1 越合适）：\n"));
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.common_quality", "  灰 ")).Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitGray : 0, rod.fitGray)));
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.uncommon_quality", "  绿 ")).Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitGreen : 0, rod.fitGreen)));
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.rare_quality", "  蓝 ")).Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitBlue : 0, rod.fitBlue)));
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.epic_quality", "  紫 ")).Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitPurple : 0, rod.fitPurple))).Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.legendary_quality", "  红 ")).Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitRed : 0, rod.fitRed)));
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.mythic_quality", "  橙 ")).Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitOrange : 0, rod.fitOrange)));
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.supreme_quality", "  金 ")).Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitGold : 0, rod.fitGold))).Append("\n\n");
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.species_fit", "鱼种适配：\n"));
            text.Append("  ").Append(DesktopGameData.CatchGroupLabel("still_bait")).Append(' ')
                .Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitStillBait : 0, rod.fitStillBait))).Append('\n');
            text.Append("  ").Append(DesktopGameData.CatchGroupLabel("stream_light")).Append(' ')
                .Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitStreamLight : 0, rod.fitStreamLight))).Append('\n');
            text.Append("  ").Append(DesktopGameData.CatchGroupLabel("lure_predator")).Append(' ')
                .Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitLurePredator : 0, rod.fitLurePredator))).Append('\n');
            text.Append("  ").Append(DesktopGameData.CatchGroupLabel("cast_heavy")).Append(' ')
                .Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitCastHeavy : 0, rod.fitCastHeavy))).Append('\n');
            text.Append("  ").Append(DesktopGameData.CatchGroupLabel("giant_game")).Append(' ')
                .Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitGiantGame : 0, rod.fitGiantGame))).Append("\n\n");
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.oversize_explanation", "说明：加成刻意做弱，主要靠熟练度和线索。钓上超过阈值的鱼会计入超规格；"));
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.break_repurchase_policy", "满次数后竿消失，需再买一把才能开钓。\n"));
            if (IsEquippedRod(rod.id))
                text.Append(DesktopLocalization.Text("ui.shop.rod_details.equipped", "当前状态：使用中。"));
            else
                text.Append(owned ? DesktopLocalization.Text("ui.shop.rod_details.owned_available", "当前状态：已拥有，可装备。") : DesktopLocalization.Text("ui.shop.rod_details.not_owned", "当前状态：未拥有。"));
            return text.ToString();
        }

        string BuildVesselDetails(ShopVesselDto vessel, bool owned)
        {
            var def = DesktopGameData.GetVessel(vessel.vesselId);
            var price = def != null ? def.priceGold : vessel.priceGold;
            var level = def != null ? def.unlockPlayerLevel : vessel.unlockPlayerLevel;
            var placeholder = def != null ? def.placeholderCatchCount : vessel.placeholderCatchCount;
            var text = new StringBuilder();
            text.Append(vessel.name).Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.bait_details.unlock_level", "解锁等级：钓鱼 Lv.")).Append(level).Append('\n');
            text.Append(DesktopLocalization.Text("ui.shop.rod_details.price", "价格：")).Append(price).Append(DesktopLocalization.Text("ui.shop.vessel_details.coins_suffix", " 金\n"));
            text.Append(DesktopLocalization.Text("ui.shop.vessel_details.placeholder_uses", "占位捕捞次数：")).Append(placeholder).Append(DesktopLocalization.Text("ui.shop.vessel_details.placeholder_note", "（仅表内字段，当前不生效）\n"));
            text.Append(DesktopLocalization.Text("ui.shop.vessel_details.unavailable_policy", "使用：暂未开放。购买后也不可装备、不可开船。\n\n"));
            text.Append(owned ? DesktopLocalization.Text("ui.shop.vessel_details.owned", "当前状态：已拥有。") : DesktopLocalization.Text("ui.shop.rod_details.not_owned", "当前状态：未拥有。"));
            return text.ToString();
        }

        void TintTab(Button tab, bool active)
        {
            if (tab == null)
                return;
            if (!_shopTabsStyled)
                DesktopModalUi.StyleButton(tab);
            var visual = tab.GetComponent<CozyButtonVisual>();
            if (visual != null)
                visual.SetSelected(active);
        }

        void CreateCard(int index, string itemId, string name, string iconPath, string meta)
        {
            var card = DesktopUiPrefabFactory.Instantiate("ShopItemCard", _content);
            if (card == null)
                return;
            card.name = "Item_" + index;
            var productArt=CozyItemArt.Product(_tab, itemId, name);
            CozyItemArt.Set(card.transform, "DesignItemArt", productArt);
            if(_equipmentMode && _tab==1 && !OwnsRod(itemId))
            {
                var picture=card.transform.Find("DesignItemArt")?.GetComponent<Image>();
                if(picture!=null) { picture.material=Resources.Load<Material>(CozyJournalTheme.ResourceRoot+"materials/Grayscale"); picture.color=new Color(1,1,1,.45f); }
            }
            var label = DesktopModalUi.FindComponent<Text>(card.transform, "Label");
            var metadata = DesktopModalUi.FindComponent<Text>(card.transform, "Meta");
            var icon = DesktopModalUi.FindComponent<Image>(card.transform, "Icon");
            var button = card.GetComponent<Button>();
            if (label == null || metadata == null || icon == null || button == null)
            {
                Debug.LogError("[DesktopUI] ShopItemCard requires root Button, Label/Meta Text and Icon Image.", card);
                card.SetActive(false);
                Destroy(card);
                return;
            }
            label.text = name;
            metadata.text = meta;
            icon.overrideSprite = null;
            icon.sprite = CatTheme.LoadCozy(iconPath, Vector4.zero);
            icon.enabled = icon.sprite != null;
            if (!Application.isPlaying)
                DesktopModalUi.StyleTree(card.transform);
            else
            {
                var skin = card.GetComponent<CozyPanelSkin>() ?? card.AddComponent<CozyPanelSkin>();
                skin.MarkSkinned();
                skin.RememberChildren();
            }
            var visual = button.GetComponent<CozyButtonVisual>();
            if (visual != null)
                visual.SetSelected(index == _selectedIndex);
            CozyStickerMotion.Bind(card, "shop:" + _equipmentMode + ":" + _tab + ":" + itemId, index,
                _content.parent as RectTransform,sourceId:productArt!=null?productArt.name:itemId);
            button.onClick.RemoveAllListeners();
            var captured = index;
            button.onClick.AddListener(() =>
            {
                if (_selectedIndex == captured) return;
                _selectedIndex = captured;
                RenderContent(false);
                transform.Find("Details/JournalPolaroid")?.GetComponent<CozyJournalStaticPose>()?.PlayReveal(new Vector2(24,0),.26f,.3f);
            });
        }

        ShopBaitDto SelectedBait()
        {
            if (_tab != 0 || _selectedIndex < 0 || _selectedIndex >= _baits.Length)
                return null;
            var bait = _baits[_selectedIndex];
            return !_equipmentMode || BaitUnlocked(bait) ? bait : null;
        }

        ShopTackleDto SelectedTackle()
        {
            if (_tab != 1 || _selectedIndex < 0 || _selectedIndex >= _tackles.Length)
                return null;
            var tackle = _tackles[_selectedIndex];
            return tackle;
        }

        ShopVesselDto SelectedVessel()
        {
            return !_equipmentMode && _tab == 2 && _selectedIndex >= 0 &&
                   _selectedIndex < _vessels.Length ? _vessels[_selectedIndex] : null;
        }

        void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message ?? string.Empty;
        }

        T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path);
            return node == null ? null : node.GetComponent<T>();
        }

        void BuildFallbackUi()
        {
            Debug.LogError("[DesktopUI] Prefab is missing required controls. Runtime will not build a second layout.");
            return;
        }

        static Button CreateButton(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = CatTheme.Cream;
            var label = CreateText(go.transform, "Label", text, 15);
            Stretch(label.rectTransform);
            label.alignment = TextAnchor.MiddleCenter;
            var button = go.GetComponent<Button>();
            DesktopModalUi.StyleButton(button);
            return button;
        }

        static Text CreateText(Transform parent, string name, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = CatTheme.Font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = size;
            label.color = CatTheme.Ink;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        static void StyleText(Text label, int size)
        {
            if (label == null)
                return;
            if (CatTheme.Font != null)
                label.font = CatTheme.Font;
            label.fontSize = size;
            label.resizeTextForBestFit = false;
            label.color = CatTheme.Ink;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static void IgnoreLayout(Component component)
        {
            var layout = component.GetComponent<LayoutElement>();
            if (layout == null)
                layout = component.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Place(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rt)
        {
            Place(rt, 0f, 0f, 1f, 1f);
        }

        static bool Contains(string[] values, string value)
        {
            if (values == null)
                return false;
            for (var i = 0; i < values.Length; i++)
                if (values[i] == value)
                    return true;
            return false;
        }
    }
}
