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
        RectTransform _content;
        Button _baitTab;
        Button _tackleTab;
        Button _vesselTab;
        ShopBaitDto[] _baits = new ShopBaitDto[0];
        ShopTackleDto[] _tackles = new ShopTackleDto[0];
        ShopVesselDto[] _vessels = new ShopVesselDto[0];
        ShopGearDto _gear = new ShopGearDto();
        int _coinBalance;
        int _tab;
        int _selectedIndex = -1;
        bool _equipmentMode;
        Coroutine _refreshRoutine;

        public void Bind(IAuthenticatedApiClient api)
        {
            _api = api;
            EnsureUi();
            SetStatus(_equipmentMode ? "尚未加载装备。" : "尚未加载商店。");
            Render();
        }

        public void SetEquipmentMode(bool equipmentMode)
        {
            if (_equipmentMode != equipmentMode)
            {
                _equipmentMode = equipmentMode;
                _tab = equipmentMode ? 1 : 0;
                _selectedIndex = -1;
            }
            EnsureUi();
            SetStatus(_equipmentMode ? "选择已拥有的钓竿或已解锁的鱼饵进行装备。" : "选择商品查看详情。");
            Render();
        }

        public void OnOpened()
        {
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
            if (_refreshRoutine != null)
            {
                StopCoroutine(_refreshRoutine);
                _refreshRoutine = null;
            }
        }

        void EnsureUi()
        {
            _uiReady = false;
            _coins = Find<Text>("Header/Coins");
            _equipped = Find<Text>("Header/Equipped");
            _status = Find<Text>("Status");
            _details = Find<Text>("Details/Description");
            _buy = Find<Button>("Details/Buy");
            _equip = Find<Button>("Details/Equip");
            _retry = Find<Button>("Details/Retry");
            _content = transform.Find("Items/Viewport/Content") as RectTransform;
            _baitTab = Find<Button>("Tabs/Bait");
            _tackleTab = Find<Button>("Tabs/Tackle");
            _vesselTab = Find<Button>("Tabs/Vessel");
            if (_coins == null || _equipped == null || _status == null ||
                _details == null || _buy == null || _equip == null ||
                _content == null || _baitTab == null || _tackleTab == null || _vesselTab == null || _retry == null ||
                Find<Text>("Header/Title") == null)
            {
                Debug.LogError("[DesktopUI] PanelShop requires Header/{Title,Coins,Equipped}, Tabs/{Bait,Tackle,Vessel}, Status, Items/Viewport/Content and Details/{Description,Buy,Equip,Retry}.", this);
                return;
            }
            _uiReady = true;
            RestyleUi();
            _retry.gameObject.SetActive(false);

            var baitLabel = _baitTab.GetComponentInChildren<Text>();
            if (baitLabel != null)
                baitLabel.text = "鱼饵";
            var tackleLabel = _tackleTab.GetComponentInChildren<Text>();
            if (tackleLabel != null)
                tackleLabel.text = "钓竿";
            if (_vesselTab != null)
            {
                var vesselLabel = _vesselTab.GetComponentInChildren<Text>();
                if (vesselLabel != null)
                    vesselLabel.text = "船具";
            }
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
            _retry.onClick.RemoveAllListeners();
            _retry.onClick.AddListener(Refresh);
            SetActionButtons(false, false, false);
        }

        void RestyleUi()
        {
            _heading = Find<Text>("Header/Title");
            if (_heading != null)
                _heading.text = _equipmentMode ? "我的装备" : "钓鱼商店";
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
            if (!_uiReady)
                return;
            if (_refreshRoutine != null)
                StopCoroutine(_refreshRoutine);
            _refreshRoutine = StartCoroutine(RefreshRoutine());
        }

        IEnumerator RefreshRoutine()
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前没有有效的 Steam 会话，请重新登录。");
                yield break;
            }

            SetStatus(_equipmentMode ? "正在加载可用装备和当前状态…" : "正在加载商店目录和装备状态…");
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
            if (!catalogOk || !gearOk)
            {
                SetStatus(!string.IsNullOrEmpty(catalogError)
                    ? catalogError
                    : (gearError ?? (_equipmentMode ? "装备加载失败，请稍后重新打开页面。" : "商店加载失败，请稍后重新打开页面。")));
                Render();
                yield break;
            }

            SetStatus(_equipmentMode ? "装备已同步，可选择钓竿或鱼饵。" : "商店数据已同步。");
            Render();
        }

        void SelectTab(int tab)
        {
            if (_equipmentMode && tab == 2)
                return;
            _tab = tab;
            _selectedIndex = -1;
            Render();
        }

        void BuySelected()
        {
            if (_equipmentMode)
            {
                SetStatus("装备页仅切换已拥有的钓竿和已解锁的鱼饵，不购买商品。");
                return;
            }
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前会话已失效，请重新登录。");
                return;
            }
            if (_tab == 0)
            {
                SetStatus("鱼饵按钓鱼等级解锁，咬钩成功时自动选用并按次扣金，商店不进货。");
                return;
            }
            if (_tab == 2)
            {
                var vessel = SelectedVessel();
                if (vessel == null)
                    return;
                StartCoroutine(BuyVesselRoutine(vessel));
                return;
            }
            var tackle = SelectedTackle();
            if (tackle == null || tackle.price <= 0)
                return;
            StartCoroutine(BuyTackleRoutine(tackle));
        }

        IEnumerator BuyBaitRoutine(ShopBaitDto bait)
        {
            SetStatus("正在购买 " + bait.name + "…");
            var done = false;
            yield return StartCoroutine(_api.BuyBait(bait.id, 1,
                (ok, gear, coins, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        _coinBalance = coins;
                        SetStatus("购买成功，装备状态已由服务端刷新。");
                    }
                    else
                        SetStatus(message ?? "购买失败，当前装备未修改。");
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        IEnumerator BuyTackleRoutine(ShopTackleDto tackle)
        {
            SetStatus("正在购买 " + tackle.name + "…");
            var done = false;
            yield return StartCoroutine(_api.BuyTackle(tackle.id,
                (ok, gear, coins, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        _coinBalance = coins;
                        SetStatus("购买成功，金币和装备状态已由服务端刷新。");
                    }
                    else
                        SetStatus(message ?? "购买失败，金币和装备未修改。");
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        IEnumerator BuyVesselRoutine(ShopVesselDto vessel)
        {
            SetStatus("正在购买 " + vessel.name + "…");
            var done = false;
            yield return StartCoroutine(_api.BuyVessel(vessel.vesselId,
                (ok, gear, coins, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        _coinBalance = coins;
                        SetStatus("已购买。船具暂不可使用（即将开放）。");
                    }
                    else
                        SetStatus(message ?? "购买失败，金币和装备未修改。");
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        void EquipSelected()
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前会话已失效，请重新登录。");
                return;
            }
            if (_equipmentMode && _tab == 0)
            {
                var bait = SelectedBait();
                if (bait != null && BaitUnlocked(bait) && !IsEquippedBait(bait.id))
                    StartCoroutine(EquipBaitRoutine(bait));
                return;
            }
            if (_tab != 1)
            {
                SetStatus(_tab == 2
                    ? "船具即将开放，无法装备使用。"
                    : "进阶饵在咬钩时按鱼种自动选用，无需手动装备。");
                return;
            }
            var tackle = SelectedTackle();
            if (tackle != null && OwnsRod(tackle.id) && !IsEquippedRod(tackle.id))
                StartCoroutine(EquipTackleRoutine(tackle));
        }

        IEnumerator EquipBaitRoutine(ShopBaitDto bait)
        {
            SetStatus("正在装备 " + bait.name + "…");
            var done = false;
            yield return StartCoroutine(_api.EquipBait(bait.id,
                (ok, gear, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        SetStatus("鱼饵已装备。");
                    }
                    else
                        SetStatus(message ?? "装备失败，当前装备未修改。");
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        IEnumerator EquipTackleRoutine(ShopTackleDto tackle)
        {
            SetStatus("正在装备 " + tackle.name + "…");
            var done = false;
            yield return StartCoroutine(_api.EquipTackle(tackle.id,
                (ok, gear, message) =>
                {
                    if (ok)
                    {
                        _gear = gear;
                        SetStatus("钓竿已装备。");
                    }
                    else
                        SetStatus(message ?? "装备失败，当前装备未修改。");
                    done = true;
                }));
            while (!done)
                yield return null;
            Render();
        }

        void Render()
        {
            if (_coins == null || _equipped == null || _details == null || _content == null)
                return;
            _coins.text = "金币：" + _coinBalance;
            _equipped.text = _equipmentMode
                ? "当前钓竿：" + EquippedRodName() + "    当前鱼饵：" + EquippedBaitName() +
                  "\n钓鱼等级：" + Math.Max(1, _gear.playerLevel) + " · 仅显示已拥有 / 已解锁装备"
                : "钓竿：" + EquippedRodName() + "    钓鱼等级：" + Math.Max(1, _gear.playerLevel);
            TintTab(_baitTab, _tab == 0);
            TintTab(_tackleTab, _tab == 1);
            if (_vesselTab != null)
                TintTab(_vesselTab, _tab == 2);

            for (var i = _content.childCount - 1; i >= 0; i--)
            {
                _content.GetChild(i).gameObject.SetActive(false);
                Destroy(_content.GetChild(i).gameObject);
            }

            var visibleCount = 0;
            if (_tab == 0)
            {
                for (var i = 0; i < _baits.Length; i++)
                {
                    var unlocked = BaitUnlocked(_baits[i]);
                    if (_equipmentMode && !unlocked)
                        continue;
                    var cost = BaitCost(_baits[i]);
                    CreateCard(i, _baits[i].name, "icons/leaf.png",
                        unlocked
                            ? (_equipmentMode && IsEquippedBait(_baits[i].id) ? "使用中 · " : "已解锁 · ") +
                              (cost <= 0 ? "无限 · 不扣金" : "每次 " + cost + " 金")
                            : "钓鱼等级 " + BaitUnlockLevel(_baits[i]) + " 解锁");
                    visibleCount++;
                }
            }
            else if (_tab == 1)
            {
                for (var i = 0; i < _tackles.Length; i++)
                {
                    var owned = OwnsRod(_tackles[i].id);
                    if (_equipmentMode && !owned)
                        continue;
                    var subType = RodSubType(_tackles[i]);
                    var price = RodPrice(_tackles[i]);
                    CreateCard(i, _tackles[i].name, "icons/rod.png",
                        owned
                            ? (IsEquippedRod(_tackles[i].id) ? "使用中 · " + subType : "已拥有 · " + subType)
                            : (price <= 0 ? "新手赠送" : "未拥有 · " + price + " 金"));
                    visibleCount++;
                }
            }
            else if (!_equipmentMode)
            {
                for (var i = 0; i < _vessels.Length; i++)
                {
                    var owned = Contains(_gear.ownedVessels, _vessels[i].vesselId);
                    CreateCard(i, _vessels[i].name, "icons/pond.png",
                        owned ? "已拥有 · 暂不可用" :
                        "等级 " + _vessels[i].unlockPlayerLevel + " · " + _vessels[i].priceGold + " 金");
                    visibleCount++;
                }
            }

            var selectedBait = SelectedBait();
            var selectedTackle = SelectedTackle();
            var selectedVessel = SelectedVessel();
            if (_tab == 0 && selectedBait != null)
            {
                _details.text = BuildBaitDetails(selectedBait);
                SetActionButtons(false, false, _equipmentMode && BaitUnlocked(selectedBait) &&
                    !IsEquippedBait(selectedBait.id));
            }
            else if (_tab == 1 && selectedTackle != null)
            {
                var owned = OwnsRod(selectedTackle.id);
                _details.text = BuildRodDetails(selectedTackle, owned);
                SetActionButtons(true, !owned && RodPrice(selectedTackle) > 0,
                    owned && !IsEquippedRod(selectedTackle.id));
            }
            else if (_tab == 2 && selectedVessel != null)
            {
                var owned = Contains(_gear.ownedVessels, selectedVessel.vesselId);
                var levelOk = _gear.playerLevel >= selectedVessel.unlockPlayerLevel;
                _details.text = BuildVesselDetails(selectedVessel, owned);
                SetActionButtons(true, !owned && levelOk, false);
            }
            else
            {
                _details.text = _equipmentMode
                    ? (visibleCount == 0
                        ? (_tab == 0 ? "暂无已解锁的鱼饵。提升钓鱼等级后可在这里选择。" : "暂无已拥有的钓竿。获得钓竿后可在这里装备。")
                        : "选择已拥有的钓竿或已解锁的鱼饵查看详情，再点击装备。\n\n装备页不购买商品；鱼饵仍按原有规则在咬钩成功时扣金。")
                    : _tab == 0
                    ? "选择鱼饵查看食性、解锁等级和每次扣金。鱼饵不进货，没有库存。"
                    : (_tab == 2
                        ? "选择船具查看购入条件。购买后暂不可使用。"
                        : "请选择商品查看详情。");
                SetActionButtons(_tab != 0, false, false);
            }
        }

        void SetActionButtons(bool showBuy, bool buyEnabled, bool equipEnabled)
        {
            showBuy = showBuy && !_equipmentMode;
            if (_buy != null)
            {
                _buy.gameObject.SetActive(showBuy);
                _buy.interactable = showBuy && buyEnabled;
                var buyLabel = _buy.GetComponentInChildren<Text>();
                if (buyLabel != null)
                    buyLabel.text = "购买";
            }
            if (_equip != null)
            {
                var showEquip = _equipmentMode
                    ? (_tab == 0 ? SelectedBait() != null : _tab == 1 && SelectedTackle() != null)
                    : equipEnabled;
                _equip.gameObject.SetActive(showEquip);
                _equip.interactable = equipEnabled;
                var equipLabel = _equip.GetComponentInChildren<Text>(true);
                if (equipLabel != null)
                    equipLabel.text = _equipmentMode &&
                        (_tab == 0 ? IsEquippedBait(SelectedBait()?.id) : IsEquippedRod(SelectedTackle()?.id))
                        ? "使用中" : "装备";
            }
        }

        string EquippedBaitName()
        {
            for (var i = 0; i < _baits.Length; i++)
                if (_baits[i].id == _gear.equippedBait && !string.IsNullOrEmpty(_baits[i].name))
                    return _baits[i].name;
            var local = DesktopGameData.GetBait(_gear.equippedBait);
            return local != null && !string.IsNullOrEmpty(local.name)
                ? local.name : (string.IsNullOrEmpty(_gear.equippedBait) ? "无" : _gear.equippedBait);
        }

        bool IsEquippedBait(string baitId)
        {
            return !string.IsNullOrEmpty(baitId) && _gear.equippedBait == baitId;
        }

        string EquippedRodName()
        {
            if (string.IsNullOrEmpty(_gear.equippedRod))
                return "无";
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
            return string.IsNullOrEmpty(rod.subType) ? "钓竿" : rod.subType;
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
            text.Append("对口食性：").Append(DesktopGameData.DietLabel(diet));
            if (diet == "herbivore")
                text.Append("（鲫、鲤、罗非等）");
            else if (diet == "omnivore")
                text.Append("（锦鲤、鲈、鲶等）");
            else if (diet == "carnivore")
                text.Append("（黑鲈、翘嘴、鳟等）");
            else
                text.Append("（任意鱼种兜底）");
            text.Append('\n');
            text.Append("解锁等级：钓鱼 Lv.").Append(unlock);
            if (infinite)
                text.Append("（新手默认，永不短缺）");
            text.Append('\n');
            text.Append("每次消耗：").Append(cost <= 0 ? "不扣金" : cost + " 金");
            text.Append("（仅在咬钩成功时扣除）\n");
            text.Append("咬钩加成：草食 ").Append(DesktopGameData.FormatPct(herb));
            text.Append("  /  杂食 ").Append(DesktopGameData.FormatPct(omni));
            text.Append("  /  肉食 ").Append(DesktopGameData.FormatPct(carn)).Append('\n');
            text.Append('\n');
            if (_equipmentMode)
            {
                text.Append("选择：仅装备已解锁鱼饵，不购买、不增加库存。\n");
                text.Append("咬钩时仍按原有规则自动选饵和扣金；金币不够则回退到基础杂饵。\n");
                text.Append(IsEquippedBait(bait.id) ? "当前选择：使用中。\n" : "当前选择：可装备。\n");
            }
            else
            {
                text.Append("用法：不进货、不显示库存。咬钩时按鱼的食性自动选用已解锁对口饵；");
                text.Append("金币不够或未解锁则回退到基础杂饵。\n");
            }
            text.Append(unlocked ? "当前状态：已解锁，可自动选用。" : "当前状态：等级不足，暂不会被选用。");
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
            text.Append("类型：").Append(subType).Append('\n');
            if (!_equipmentMode)
                text.Append("价格：").Append(price <= 0 ? "新手赠送（0 金，不可回购）" : price + " 金").Append('\n');
            text.Append("咬钩加成：").Append(DesktopGameData.FormatPct(bite)).Append('\n');
            text.Append("防脱：").Append(DesktopGameData.FormatPct(escape)).Append('\n');
            text.Append("超规格阈值：").Append(breakM.ToString("0.##")).Append("m\n");
            text.Append("超规格成功上限：").Append(breakN).Append(" 次（满则销毁当前竿，不能修理）\n\n");
            text.Append("品质适配（乘区，越接近 1 越合适）：\n");
            text.Append("  灰 ").Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitGray : 0, rod.fitGray)));
            text.Append("  绿 ").Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitGreen : 0, rod.fitGreen)));
            text.Append("  蓝 ").Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitBlue : 0, rod.fitBlue)));
            text.Append("  紫 ").Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitPurple : 0, rod.fitPurple))).Append('\n');
            text.Append("  红 ").Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitRed : 0, rod.fitRed)));
            text.Append("  橙 ").Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitOrange : 0, rod.fitOrange)));
            text.Append("  金 ").Append(DesktopGameData.FormatMul(Fit(def != null ? def.fitGold : 0, rod.fitGold))).Append("\n\n");
            text.Append("鱼种适配：\n");
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
            text.Append("说明：加成刻意做弱，主要靠熟练度和线索。钓上超过阈值的鱼会计入超规格；");
            text.Append("满次数后竿消失，需再买一把才能开钓。\n");
            if (IsEquippedRod(rod.id))
                text.Append("当前状态：使用中。");
            else
                text.Append(owned ? "当前状态：已拥有，可装备。" : "当前状态：未拥有。");
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
            text.Append("解锁等级：钓鱼 Lv.").Append(level).Append('\n');
            text.Append("价格：").Append(price).Append(" 金\n");
            text.Append("占位捕捞次数：").Append(placeholder).Append("（仅表内字段，当前不生效）\n");
            text.Append("使用：暂未开放。购买后也不可装备、不可开船。\n\n");
            text.Append(owned ? "当前状态：已拥有。" : "当前状态：未拥有。");
            return text.ToString();
        }

        static void TintTab(Button tab, bool active)
        {
            if (tab == null)
                return;
            DesktopModalUi.StyleButton(tab);
            var visual = tab.GetComponent<CozyButtonVisual>();
            if (visual != null)
                visual.SetSelected(active);
        }

        void CreateCard(int index, string name, string iconPath, string meta)
        {
            var card = DesktopUiPrefabFactory.Instantiate("ShopItemCard", _content);
            if (card == null)
                return;
            card.name = "Item_" + index;
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
            DesktopModalUi.StyleTree(card.transform);
            var visual = button.GetComponent<CozyButtonVisual>();
            if (visual != null)
                visual.SetSelected(index == _selectedIndex);
            button.onClick.RemoveAllListeners();
            var captured = index;
            button.onClick.AddListener(() =>
            {
                _selectedIndex = captured;
                Render();
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
            return !_equipmentMode || OwnsRod(tackle.id) ? tackle : null;
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
