using System;
using System.Collections;
using System.Collections.Generic;
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
        IAuthenticatedApiClient _api;
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
        ShopBaitDto[] _baits = new ShopBaitDto[0];
        ShopTackleDto[] _tackles = new ShopTackleDto[0];
        ShopGearDto _gear = new ShopGearDto();
        int _coinBalance;
        bool _baitTabActive = true;
        int _selectedIndex = -1;
        Coroutine _refreshRoutine;

        public void Bind(IAuthenticatedApiClient api)
        {
            _api = api;
            EnsureUi();
            SetStatus("尚未加载商店。");
        }

        public void OnOpened()
        {
            Refresh();
        }

        /// <summary>
        /// Editor baker entry point. It creates the same responsive shell used
        /// at runtime so the prefab remains the editable source of truth.
        /// </summary>
        public void BuildEditorLayout()
        {
            if (transform.Find("Header") == null)
                BuildFallbackUi();
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
            if (_coins == null || _equipped == null || _status == null ||
                _details == null || _buy == null || _equip == null ||
                _content == null || _baitTab == null || _tackleTab == null || _retry == null)
                BuildFallbackUi();

            _baitTab.onClick.RemoveAllListeners();
            _baitTab.onClick.AddListener(() =>
            {
                _baitTabActive = true;
                _selectedIndex = -1;
                Render();
            });
            _tackleTab.onClick.RemoveAllListeners();
            _tackleTab.onClick.AddListener(() =>
            {
                _baitTabActive = false;
                _selectedIndex = -1;
                Render();
            });
            _buy.onClick.RemoveAllListeners();
            _buy.onClick.AddListener(BuySelected);
            _equip.onClick.RemoveAllListeners();
            _equip.onClick.AddListener(EquipSelected);
            _retry.onClick.RemoveAllListeners();
            _retry.onClick.AddListener(Refresh);
        }

        void Refresh()
        {
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

            SetStatus("正在加载商店目录和装备状态…");
            var catalogDone = false;
            var catalogOk = false;
            string catalogError = null;
            yield return StartCoroutine(_api.GetShopCatalog(
                (ok, baits, tackles, error) =>
                {
                    catalogOk = ok;
                    catalogError = error;
                    if (ok)
                    {
                        _baits = baits ?? new ShopBaitDto[0];
                        _tackles = tackles ?? new ShopTackleDto[0];
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
                    : (gearError ?? "商店加载失败，请点击重试。"));
                Render();
                yield break;
            }

            SetStatus("商店数据已同步。");
            Render();
        }

        void BuySelected()
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前会话已失效，请重新登录。");
                return;
            }
            if (_baitTabActive)
            {
                var bait = SelectedBait();
                if (bait == null || bait.price <= 0)
                    return;
                StartCoroutine(BuyBaitRoutine(bait));
            }
            else
            {
                var tackle = SelectedTackle();
                if (tackle == null || tackle.price <= 0)
                    return;
                StartCoroutine(BuyTackleRoutine(tackle));
            }
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
                        SetStatus("购买成功，金币和库存已由服务端刷新。");
                    }
                    else
                        SetStatus(message ?? "购买失败，金币和库存未修改。");
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

        void EquipSelected()
        {
            if (_baitTabActive)
            {
                var bait = SelectedBait();
                if (bait != null)
                    StartCoroutine(EquipBaitRoutine(bait));
            }
            else
            {
                var tackle = SelectedTackle();
                if (tackle != null)
                    StartCoroutine(EquipTackleRoutine(tackle));
            }
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
                        SetStatus("渔具已装备。");
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
            if (_coins == null || _content == null)
                return;
            _coins.text = "金币：" + _coinBalance;
            _equipped.text = "鱼饵：" + _gear.equippedBait +
                             "    渔具：" + _gear.equippedTackle;
            _baitTab.GetComponentInChildren<Text>().color =
                _baitTabActive ? Color.white : new Color(0.65f, 0.7f, 0.75f);
            _tackleTab.GetComponentInChildren<Text>().color =
                !_baitTabActive ? Color.white : new Color(0.65f, 0.7f, 0.75f);

            for (var i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            if (_baitTabActive)
            {
                for (var i = 0; i < _baits.Length; i++)
                    CreateCard(i, _baits[i].name, _baits[i].icon,
                        "价格 " + _baits[i].price + " · 库存 " + _gear.BaitCount(_baits[i].id));
            }
            else
            {
                for (var i = 0; i < _tackles.Length; i++)
                {
                    var owned = Contains(_gear.ownedTackles, _tackles[i].id) ||
                                _tackles[i].id == "basic";
                    CreateCard(i, _tackles[i].name, _tackles[i].icon,
                        owned ? "已拥有 · 价格 " + _tackles[i].price :
                        "未拥有 · 价格 " + _tackles[i].price);
                }
            }

            var selectedBait = SelectedBait();
            var selectedTackle = SelectedTackle();
            if (_baitTabActive && selectedBait != null)
            {
                _details.text = selectedBait.name + "\n\n" +
                                "基础咬钩加成：" + (selectedBait.globalBonus * 100f).ToString("0.#") +
                                "%\n服务端库存：" + _gear.BaitCount(selectedBait.id);
                _buy.interactable = selectedBait.price > 0;
                _equip.interactable = _gear.equippedBait != selectedBait.id;
            }
            else if (!_baitTabActive && selectedTackle != null)
            {
                var owned = Contains(_gear.ownedTackles, selectedTackle.id) ||
                            selectedTackle.id == "basic";
                _details.text = selectedTackle.name + "\n\n逃脱率减免：" +
                                (selectedTackle.escapeReduction * 100f).ToString("0.#") +
                                "%\n状态：" + (owned ? "已拥有" : "未拥有");
                _buy.interactable = !owned && selectedTackle.price > 0;
                _equip.interactable = owned && _gear.equippedTackle != selectedTackle.id;
            }
            else
            {
                _details.text = "请选择商品查看详情。";
                _buy.interactable = false;
                _equip.interactable = false;
            }
        }

        void CreateCard(int index, string name, string icon, string meta)
        {
            var card = new GameObject("Item_" + index,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            card.transform.SetParent(_content, false);
            var layout = card.GetComponent<LayoutElement>();
            layout.minHeight = 64f;
            layout.preferredHeight = 64f;
            card.GetComponent<Image>().color = index == _selectedIndex
                ? new Color(0.2f, 0.45f, 0.55f, 1f)
                : new Color(0.15f, 0.21f, 0.27f, 1f);
            var label = CreateText(card.transform, "Label",
                icon + "  " + name + "\n" + meta, 15);
            Stretch(label.rectTransform);
            var captured = index;
            card.GetComponent<Button>().onClick.AddListener(() =>
            {
                _selectedIndex = captured;
                Render();
            });
        }

        ShopBaitDto SelectedBait()
        {
            return _baitTabActive && _selectedIndex >= 0 &&
                   _selectedIndex < _baits.Length ? _baits[_selectedIndex] : null;
        }

        ShopTackleDto SelectedTackle()
        {
            return !_baitTabActive && _selectedIndex >= 0 &&
                   _selectedIndex < _tackles.Length ? _tackles[_selectedIndex] : null;
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
            var root = GetComponent<RectTransform>();
            if (root.GetComponent<Image>() == null)
                root.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.12f, 0.16f, 1f);
            var header = NewRect("Header", root);
            header.anchorMin = new Vector2(0f, 0.82f);
            header.anchorMax = Vector2.one;
            header.offsetMin = new Vector2(20f, 0f);
            header.offsetMax = new Vector2(-20f, -12f);
            _coins = CreateText(header, "Coins", "金币：0", 20);
            Place(_coins.rectTransform, 0f, 0.5f, 0.45f, 1f);
            _equipped = CreateText(header, "Equipped", "鱼饵：basic    渔具：basic", 14);
            Place(_equipped.rectTransform, 0.45f, 0.5f, 1f, 1f);
            var tabs = NewRect("Tabs", root);
            Place(tabs, 0f, 0.72f, 1f, 0.82f);
            _baitTab = CreateButton(tabs, "Bait", "鱼饵");
            _tackleTab = CreateButton(tabs, "Tackle", "渔具");
            Place(_baitTab.transform as RectTransform, 0f, 0f, 0.5f, 1f);
            Place(_tackleTab.transform as RectTransform, 0.5f, 0f, 1f, 1f);
            _status = CreateText(root, "Status", string.Empty, 14);
            Place(_status.rectTransform, 0f, 0.64f, 1f, 0.72f);
            var items = NewRect("Items", root);
            Place(items, 0.04f, 0.08f, 0.48f, 0.62f);
            var viewport = NewRect("Viewport", items);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _content = NewRect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            var details = NewRect("Details", root);
            Place(details, 0.54f, 0.08f, 0.96f, 0.62f);
            _details = CreateText(details, "Description", "请选择商品查看详情。", 18);
            Place(_details.rectTransform, 0f, 0.35f, 1f, 1f);
            _buy = CreateButton(details, "Buy", "购买");
            Place(_buy.transform as RectTransform, 0.04f, 0f, 0.46f, 0.24f);
            _equip = CreateButton(details, "Equip", "装备");
            Place(_equip.transform as RectTransform, 0.54f, 0f, 0.96f, 0.24f);
            _retry = CreateButton(details, "Retry", "重试");
            Place(_retry.transform as RectTransform, 0.04f, 0.25f, 0.96f, 0.34f);
        }

        static Button CreateButton(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.55f, 1f);
            var label = CreateText(go.transform, "Label", text, 15);
            Stretch(label.rectTransform);
            label.alignment = TextAnchor.MiddleCenter;
            return go.GetComponent<Button>();
        }

        static Text CreateText(Transform parent, string name, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = size;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Place(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
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
