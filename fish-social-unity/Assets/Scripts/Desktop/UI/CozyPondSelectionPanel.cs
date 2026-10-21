using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public sealed class CozyPondSelectionPanel : MonoBehaviour
    {
        Action<string> _focusPond;
        Action<string> _enterPond;
        RectTransform _cards;
        ScrollRect _scroll;
        Text _asideBody;
        Button _enter;
        Button _locate;
        PondSelectionState _selection;
        string _selectedId => _selection?.Selected?.pondId;
        InputField _search;
        Text _resultCount;
        int _filter;
        bool _building;
        bool _lastProgressLoading;
        FishSocial.Desktop.Auth.FishingProgressDto _lastProgress;
        readonly HashSet<string> _revealed = new HashSet<string>(StringComparer.Ordinal);

        public void BindSelection(PondSelectionState selection)
        {
            if (_selection != null) _selection.Changed -= OnSelectionChanged;
            _selection = selection;
            if (_selection != null) _selection.Changed += OnSelectionChanged;
            BuildCards();
        }

        void OnSelectionChanged()
        {
            if (_building) return;
            var progressChanged = _selection != null &&
                (_lastProgress != _selection.Progress || _lastProgressLoading != _selection.ProgressLoading);
            if (_selection != null)
            {
                _lastProgress = _selection.Progress;
                _lastProgressLoading = _selection.ProgressLoading;
            }
            if (progressChanged && _filter == 1 && isActiveAndEnabled && !_selection.ProgressLoading) BuildCards();
            else RefreshSelection();
        }

        public void Bind(Action<string> focusPond, Action<string> enterPond)
        {
            _focusPond = focusPond;
            _enterPond = enterPond;
            EnsureUi();
            BuildCards();
        }

        public void OnOpened()
        {
            EnsureUi();
            BuildCards();
            if (_selection != null) StartCoroutine(_selection.LoadProgress(this));
        }

        void EnsureUi()
        {
            _scroll = DesktopModalUi.FindComponent<ScrollRect>(transform, "PondCardScroll");
            _cards = DesktopModalUi.FindComponent<RectTransform>(transform, "PondCardScroll/Viewport/Cards");
            if (_scroll == null || _cards == null ||
                !DesktopModalUi.RequireNodes(transform, "PanelPondSelection", "PondSelectionHeader/Title",
                    "PondSelectionHeader/Description", "PondCardScroll/Viewport"))
            {
                Debug.LogError("[DesktopUI] PanelPondSelection requires a complete header and PondCardScroll/Viewport/Cards; runtime layout generation is disabled.", this);
                _cards = null;
                return;
            }
            _asideBody = DesktopModalUi.FindComponent<Text>(transform, "DesignPondAside/FactsScroll/Body") ??
                         DesktopModalUi.FindComponent<Text>(transform, "DesignPondAside/Body");
            _enter = DesktopModalUi.FindComponent<Button>(transform, "DesignPondAside/Enter");
            _locate = DesktopModalUi.FindComponent<Button>(transform, "DesignPondAside/Locate");
            if (_asideBody == null || _enter == null || _locate == null)
                Debug.LogError("[DesktopUI] PanelPondSelection requires DesignPondAside/{Body,Enter,Locate}.", this);
            EnableScrollHit(_scroll);
            _scroll.onValueChanged.RemoveListener(RevealVisibleCards);
            _scroll.onValueChanged.AddListener(RevealVisibleCards);
            DesktopModalUi.StyleTree(transform);
            DesktopModalUi.BindButton(transform, "DesignPondAside/Enter", EnterSelected);
            DesktopModalUi.BindButton(transform, "DesignPondAside/Locate", LocateSelected);
            DesktopModalUi.BindButton(transform, "DesignPondAside/Retry", Retry);
            DesktopModalUi.BindButton(transform, "DesignPondAside/ModeSell", () => _selection?.SelectFeeMode("sell_only"));
            DesktopModalUi.BindButton(transform, "DesignPondAside/ModeAuto", () => _selection?.SelectFeeMode("auto_return"));
            DesktopModalUi.BindButton(transform, "FilterAll", () => SetFilter(0));
            DesktopModalUi.BindButton(transform, "FilterAccessible", () => SetFilter(1));
            DesktopModalUi.BindButton(transform, "FilterFree", () => SetFilter(2));
            DesktopModalUi.BindButton(transform, "ClearFilters", ClearFilters);
            _resultCount = DesktopModalUi.FindComponent<Text>(transform, "ResultCount");
            _search = DesktopModalUi.FindComponent<InputField>(transform, "Search");
            if (_search != null)
            {
                _search.onValueChanged.RemoveListener(OnSearchChanged);
                _search.onValueChanged.AddListener(OnSearchChanged);
            }
        }

        void BuildCards()
        {
            if (_cards == null || _scroll == null || _building)
                return;
            _building = true;
            for (var cardIndex = _cards.childCount - 1; cardIndex >= 0; cardIndex--)
            {
                var oldCard = _cards.GetChild(cardIndex).gameObject;
                CozyMotion.Cancel(oldCard.transform as RectTransform, true);
                oldCard.SetActive(false);
                Destroy(oldCard);
            }
            var definitions = new Dictionary<string, DesktopGameData.PondDef>(StringComparer.Ordinal);
            foreach (var definition in DesktopGameData.Ponds)
                if (definition != null && !string.IsNullOrEmpty(definition.pondId))
                    definitions[definition.pondId] = definition;
            var visiblePonds = WorldMapPondCatalog.LoadVisible();
            var cardCount = 0;
            WorldMapPondView first = null;
            var selectionVisible = false;
            foreach (var pond in visiblePonds)
            {
                if (pond == null || string.IsNullOrEmpty(pond.pondId))
                    continue;
                DesktopGameData.PondDef definition;
                definitions.TryGetValue(pond.pondId, out definition);
                if (!Matches(pond, definition)) continue;
                if (first == null) first = pond;
                if (pond.pondId == _selectedId) selectionVisible = true;
                BuildCard(pond, definition);
                cardCount++;
            }
            if (cardCount == 0)
            {
                var empty = DesktopUiPrefabFactory.Instantiate("TextStatusRow", _cards);
                var message = empty != null ? DesktopModalUi.FindComponent<Text>(empty.transform, "Message") : null;
                if (message != null)
                    message.text = "没有符合条件的鱼塘，请清除筛选或修改搜索。";
            }
            _scroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cards);
            _scroll.verticalNormalizedPosition = 1f;
            if (_resultCount != null) _resultCount.text = cardCount + " / " + visiblePonds.Length + " 个鱼塘";
            if (!selectionVisible && !(_selection?.Busy ?? false) && !(_selection?.ProgressLoading ?? false))
                _selection?.Select(first);
            _building = false;
            RefreshSelection();
            RevealVisibleCards(Vector2.zero);
        }

        void BuildCard(WorldMapPondView pond, DesktopGameData.PondDef definition)
        {
            var card = DesktopUiPrefabFactory.Instantiate("PondSelectionCard", _cards);
            if (card == null)
                return;
            card.name = "PondCard_" + pond.pondId;
            var name = DesktopModalUi.FindComponent<Text>(card.transform, "Name");
            var description = DesktopModalUi.FindComponent<Text>(card.transform, "Description");
            var fee = DesktopModalUi.FindComponent<Text>(card.transform, "Fee");
            var level = DesktopModalUi.FindComponent<Text>(card.transform, "Level");
            var access = DesktopModalUi.FindComponent<Text>(card.transform, "Access");
            var icon = DesktopModalUi.FindComponent<Image>(card.transform, "PondIcon");
            var action = DesktopModalUi.FindComponent<Button>(card.transform, "FocusOnMap");
            if (name == null || description == null || fee == null || level == null || access == null || icon == null || action == null)
            {
                Debug.LogError("[DesktopUI] PondSelectionCard requires Name/Description/Fee/Level/Access Text, PondIcon Image and FocusOnMap Button.", card);
                card.SetActive(false);
                Destroy(card);
                return;
            }
            var closed = _selection == null || !_selection.CanEnter(pond);
            name.text = pond.displayName;
            description.text = Describe(pond, definition);
            fee.text = FeeText(pond);
            level.text = pond.minPlayerLevel > 0 ? "需要钓鱼等级 " + pond.minPlayerLevel : "等级要求：无";
            access.text = _selection != null ? _selection.AccessReason(pond) : "等待等级信息";
            icon.sprite = CatTheme.LoadCozy(closed ? "icons/lock.png" : WaterIcon(definition), Vector4.zero);
            var photo = DesktopModalUi.FindComponent<Image>(card.transform, "Photo");
            if (photo != null)
            {
                photo.sprite = Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/ponds/" + pond.pondId);
                photo.enabled = photo.sprite != null;
                photo.preserveAspect = true;
                photo.raycastTarget = false;
            }
            var pondId = pond.pondId;
            action.onClick.RemoveAllListeners();
            action.onClick.AddListener(() =>
            {
                if (_selection == null || _selection.Busy) return;
                SelectPond(pondId);
                _focusPond?.Invoke(pondId);
            });
            DesktopModalUi.StyleTree(card.transform);
            var paper = card.transform.Find("DesignPaper")?.GetComponent<Image>();
            if (paper != null) paper.raycastTarget = true;
            var select = card.GetComponent<Button>() ?? card.AddComponent<Button>();
            select.targetGraphic = card.GetComponent<Image>();
            select.transition = Selectable.Transition.None;
            select.onClick.RemoveAllListeners();
            select.onClick.AddListener(() => SelectPond(pondId));
            icon.color = DesktopGameData.CategoryColor(pond.pondCategory, closed);
        }

        static void EnableScrollHit(ScrollRect scroll)
        {
            if (scroll == null)
                return;
            var background = scroll.GetComponent<Image>() ?? scroll.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = true;
            if (scroll.viewport == null)
                return;
            var viewportImage = scroll.viewport.GetComponent<Image>() ?? scroll.viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
        }

        void SelectPond(string pondId)
        {
            if (_selection == null || _selection.Busy) return;
            foreach (var pond in WorldMapPondCatalog.LoadVisible())
                if (pond != null && pond.pondId == pondId) { _selection.Select(pond); break; }
        }

        void RefreshSelection()
        {
            if (_cards == null) return;
            var ponds = WorldMapPondCatalog.LoadVisible();
            for (var i = 0; i < _cards.childCount; i++)
            {
                var card = _cards.GetChild(i);
                if (!card.gameObject.activeSelf) continue;
                var selected = card.name == "PondCard_" + _selectedId;
                card.GetComponent<CozyButtonVisual>()?.SetSelected(selected);
                var access = DesktopModalUi.FindComponent<Text>(card, "Access");
                foreach (var pond in ponds)
                    if (pond != null && card.name == "PondCard_" + pond.pondId)
                    {
                        if (access != null) access.text = _selection != null ? _selection.AccessReason(pond) : "等待等级信息";
                        var icon = DesktopModalUi.FindComponent<Image>(card, "PondIcon");
                        var closed = _selection == null || !_selection.CanEnter(pond);
                        if (icon != null)
                        {
                            icon.sprite = CatTheme.LoadCozy(closed ? "icons/lock.png" : "icons/pond.png", Vector4.zero);
                            icon.color = DesktopGameData.CategoryColor(pond.pondCategory, closed);
                        }
                    }
            }
            PondDetailView.Refresh(transform.Find("DesignPondAside"), _selection, "Body");
            var busy = _selection != null && _selection.Busy;
            var allowed = _selection != null && _selection.CanEnter(_selection.Selected);
            if (_enter != null)
            {
                _enter.interactable = allowed && !busy && _enterPond != null;
                var label = _enter.GetComponentInChildren<Text>(true);
                if (label != null) label.text = busy ? "正在进入…" : allowed ? "进入鱼塘" : "暂不可进入";
            }
            if (_locate != null) _locate.interactable = !busy && !string.IsNullOrEmpty(_selectedId) && _focusPond != null;
            if (_search != null) _search.interactable = !busy;
            var filterNames = new[] { "FilterAll", "FilterAccessible", "FilterFree" };
            for (var i = 0; i < filterNames.Length; i++)
            {
                var button = DesktopModalUi.FindComponent<Button>(transform, filterNames[i]);
                if (button == null) continue;
                button.interactable = !busy;
                button.GetComponent<CozyButtonVisual>()?.SetSelected(i == _filter);
            }
        }

        void SetFilter(int filter)
        {
            if (_selection != null && _selection.Busy) return;
            _filter = filter;
            _revealed.Clear();
            BuildCards();
        }

        void OnSearchChanged(string _) { _revealed.Clear(); BuildCards(); }

        void RevealVisibleCards(Vector2 _)
        {
            if (_cards == null || _scroll == null || _scroll.viewport == null || !isActiveAndEnabled) return;
            var viewport = _scroll.viewport;
            for (var i = 0; i < _cards.childCount; i++)
            {
                var card = _cards.GetChild(i) as RectTransform;
                if (card == null || !card.gameObject.activeSelf || _revealed.Contains(card.name)) continue;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, card);
                if (bounds.max.y < viewport.rect.yMin || bounds.min.y > viewport.rect.yMax) continue;
                _revealed.Add(card.name);
                var visual = card.Find("DesignPaper") as RectTransform;
                if (visual != null) CozyMotion.Punch(visual);
            }
        }

        void ClearFilters()
        {
            if (_selection != null && _selection.Busy) return;
            _filter = 0;
            _revealed.Clear();
            if (_search != null) _search.SetTextWithoutNotify(string.Empty);
            BuildCards();
        }

        bool Matches(WorldMapPondView pond, DesktopGameData.PondDef definition)
        {
            if (_filter == 1 && (_selection == null || !_selection.CanEnter(pond))) return false;
            if (_filter == 2 && !PondSelectionState.IsFree(pond)) return false;
            var query = _search != null ? _search.text.Trim() : string.Empty;
            if (query.Length == 0) return true;
            var haystack = pond.displayName + " " + pond.pondId + " " + Describe(pond, definition);
            return haystack.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Retry()
        {
            if (_selection == null || _selection.Busy) return;
            if (_selection.EntryFailed && _selection.Progress != null) EnterSelected();
            else StartCoroutine(_selection.LoadProgress(this));
        }

        void EnterSelected()
        {
            if (_selection != null && !_selection.Busy && _selection.CanEnter(_selection.Selected))
                _enterPond?.Invoke(_selectedId);
        }

        void LocateSelected()
        {
            if (!string.IsNullOrEmpty(_selectedId))
                _focusPond?.Invoke(_selectedId);
        }

        static string WaterIcon(DesktopGameData.PondDef definition)
        {
            var water = definition != null ? definition.waterType : null;
            if (water == "coastal" || water == "estuary")
                return "icons/fish.png";
            if (water == "river")
                return "icons/map.png";
            return "icons/pond.png";
        }

        static string Describe(WorldMapPondView pond, DesktopGameData.PondDef definition)
        {
            var description = new StringBuilder(DesktopGameData.CategoryLabel(pond.pondCategory));
            if (definition != null)
            {
                if (!string.IsNullOrEmpty(definition.waterType))
                    description.Append(" · ").Append(DesktopGameData.WaterLabel(definition.waterType));
                if (!string.IsNullOrEmpty(definition.bioRegion))
                    description.Append("\n").Append(DesktopGameData.RegionLabel(definition.bioRegion));
                if (!string.IsNullOrEmpty(definition.realWorldRef))
                    description.Append(" · ").Append(definition.realWorldRef);
            }
            else if (!string.IsNullOrEmpty(pond.theme))
                description.Append(" · ").Append(pond.theme);
            return description.ToString();
        }

        void OnDisable()
        {
            _selection?.CancelProgress(this);
            if (_cards != null) CozyMotion.Cancel(_cards, true);
        }

        void OnDestroy()
        {
            if (_selection != null) _selection.Changed -= OnSelectionChanged;
            if (_search != null) _search.onValueChanged.RemoveListener(OnSearchChanged);
            if (_scroll != null) _scroll.onValueChanged.RemoveListener(RevealVisibleCards);
        }

        static string FeeText(WorldMapPondView pond)
        {
            if (pond.allowsAutoReturn && pond.feePer2hSellOnly > 0)
                return "出售档：" + pond.feePer2hSellOnly + " 金币 / 2 小时\n回鱼档：" +
                       pond.feePer2hAutoReturn + " 金币 / 2 小时";
            return pond.feePer2h > 0 ? "入场费：" + pond.feePer2h + " 金币 / 2 小时" : "入场费：免费";
        }

    }
}
