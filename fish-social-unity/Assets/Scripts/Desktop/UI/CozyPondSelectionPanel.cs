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
        string _selectedId;

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
            _asideBody = DesktopModalUi.FindComponent<Text>(transform, "DesignPondAside/Body");
            _enter = DesktopModalUi.FindComponent<Button>(transform, "DesignPondAside/Enter");
            _locate = DesktopModalUi.FindComponent<Button>(transform, "DesignPondAside/Locate");
            if (_asideBody == null || _enter == null || _locate == null)
                Debug.LogError("[DesktopUI] PanelPondSelection requires DesignPondAside/{Body,Enter,Locate}.", this);
            EnableScrollHit(_scroll);
            DesktopModalUi.StyleTree(transform);
            DesktopModalUi.BindButton(transform, "DesignPondAside/Enter", EnterSelected);
            DesktopModalUi.BindButton(transform, "DesignPondAside/Locate", LocateSelected);
        }

        void BuildCards()
        {
            if (_cards == null || _scroll == null)
                return;
            for (var cardIndex = _cards.childCount - 1; cardIndex >= 0; cardIndex--)
            {
                var oldCard = _cards.GetChild(cardIndex).gameObject;
                oldCard.SetActive(false);
                Destroy(oldCard);
            }
            var definitions = new Dictionary<string, DesktopGameData.PondDef>(StringComparer.Ordinal);
            foreach (var definition in DesktopGameData.Ponds)
                if (definition != null && !string.IsNullOrEmpty(definition.pondId))
                    definitions[definition.pondId] = definition;
            var visiblePonds = WorldMapPondCatalog.LoadVisible();
            var cardCount = 0;
            foreach (var pond in visiblePonds)
            {
                if (pond == null || string.IsNullOrEmpty(pond.pondId))
                    continue;
                DesktopGameData.PondDef definition;
                definitions.TryGetValue(pond.pondId, out definition);
                BuildCard(pond, definition);
                cardCount++;
            }
            if (cardCount == 0)
            {
                var empty = DesktopUiPrefabFactory.Instantiate("TextStatusRow", _cards);
                var message = empty != null ? DesktopModalUi.FindComponent<Text>(empty.transform, "Message") : null;
                if (message != null)
                    message.text = "暂无可显示的鱼塘，请前往地图检查目录配置。";
            }
            _scroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cards);
            _scroll.verticalNormalizedPosition = 1f;
            if (string.IsNullOrEmpty(_selectedId) && visiblePonds.Length > 0 && visiblePonds[0] != null)
                _selectedId = visiblePonds[0].pondId;
            RefreshSelection();
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
            var closed = !pond.isOpen || pond.pondCategory == "giant";
            name.text = pond.displayName;
            description.text = Describe(pond, definition);
            fee.text = FeeText(pond);
            level.text = pond.minPlayerLevel > 0 ? "需要钓鱼等级 " + pond.minPlayerLevel : "等级要求：无";
            access.text = closed ? "暂未开放" : "可进入";
            icon.sprite = CatTheme.LoadCozy(closed ? "icons/lock.png" : WaterIcon(definition), Vector4.zero);
            var pondId = pond.pondId;
            action.onClick.RemoveAllListeners();
            action.onClick.AddListener(() => SelectPond(pondId));
            DesktopModalUi.StyleTree(card.transform);
            var paper = card.transform.Find("DesignPaper")?.GetComponent<Image>();
            if (paper != null)
                paper.raycastTarget = true;
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
            _selectedId = pondId;
            RefreshSelection();
        }

        void RefreshSelection()
        {
            for (var i = 0; i < _cards.childCount; i++)
            {
                var card = _cards.GetChild(i);
                var selected = card.name == "PondCard_" + _selectedId;
                card.GetComponentInChildren<CozyButtonVisual>(true)?.SetSelected(selected);
            }
            WorldMapPondView pond = null;
            var ponds = WorldMapPondCatalog.LoadVisible();
            for (var i = 0; i < ponds.Length; i++)
                if (ponds[i] != null && ponds[i].pondId == _selectedId)
                    pond = ponds[i];
            var closed = pond == null || !pond.isOpen || pond.pondCategory == "giant";
            if (_asideBody != null)
                _asideBody.text = pond == null ? "点选左侧的鱼塘。" : AsideText(pond);
            if (_enter != null)
            {
                _enter.interactable = pond != null && !closed && _enterPond != null;
                var label = _enter.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = closed ? "暂不可进入" : "进入鱼塘";
            }
            if (_locate != null)
                _locate.interactable = pond != null && _focusPond != null;
        }

        void EnterSelected()
        {
            if (!string.IsNullOrEmpty(_selectedId))
                _enterPond?.Invoke(_selectedId);
        }

        void LocateSelected()
        {
            if (!string.IsNullOrEmpty(_selectedId))
                _focusPond?.Invoke(_selectedId);
        }

        static string AsideText(WorldMapPondView pond)
        {
            DesktopGameData.PondDef definition = null;
            var ponds = DesktopGameData.Ponds;
            for (var i = 0; i < ponds.Length; i++)
                if (ponds[i] != null && ponds[i].pondId == pond.pondId)
                    definition = ponds[i];
            var text = new StringBuilder();
            text.Append(pond.displayName).Append('\n');
            text.Append(DesktopGameData.CategoryLabel(pond.pondCategory));
            if (definition != null && !string.IsNullOrEmpty(definition.waterType))
                text.Append(" · ").Append(DesktopGameData.WaterLabel(definition.waterType));
            if (!string.IsNullOrEmpty(pond.theme))
                text.Append(" · ").Append(pond.theme);
            text.Append("\n\n鱼种：").Append(DesktopGameData.PondSpeciesSummary(pond.pondId));
            text.Append("\n\n").Append(FeeText(pond));
            text.Append('\n').Append(pond.minPlayerLevel > 0 ? "需要钓鱼等级 " + pond.minPlayerLevel : "等级要求：无");
            if (definition != null && !string.IsNullOrEmpty(definition.realWorldRef))
                text.Append("\n位置：").Append(definition.realWorldRef);
            return text.ToString();
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

        static string FeeText(WorldMapPondView pond)
        {
            if (pond.allowsAutoReturn && pond.feePer2hSellOnly > 0)
                return "出售档：" + pond.feePer2hSellOnly + " 金币 / 2 小时\n回鱼档：" +
                       pond.feePer2hAutoReturn + " 金币 / 2 小时";
            return pond.feePer2h > 0 ? "入场费：" + pond.feePer2h + " 金币 / 2 小时" : "入场费：免费";
        }

    }
}
