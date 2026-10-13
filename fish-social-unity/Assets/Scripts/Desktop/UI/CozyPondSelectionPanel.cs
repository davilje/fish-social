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
        RectTransform _cards;
        ScrollRect _scroll;

        public void Bind(Action<string> focusPond)
        {
            _focusPond = focusPond;
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
            DesktopModalUi.StyleTree(transform);
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
            access.text = closed ? "暂未开放\n前往地图查看开放条件" : "前往地图查看开放条件";
            icon.sprite = CatTheme.LoadCozy(closed ? "icons/lock.png" : "icons/pond.png", Vector4.zero);
            action.interactable = _focusPond != null;
            var pondId = pond.pondId;
            action.onClick.RemoveAllListeners();
            action.onClick.AddListener(() => _focusPond?.Invoke(pondId));
            DesktopModalUi.StyleTree(card.transform);
        }

        static string Describe(WorldMapPondView pond, DesktopGameData.PondDef definition)
        {
            var description = new StringBuilder(DesktopGameData.CategoryLabel(pond.pondCategory));
            if (definition != null)
            {
                if (!string.IsNullOrEmpty(definition.bioRegion))
                    description.Append(" · ").Append(definition.bioRegion);
                if (!string.IsNullOrEmpty(definition.waterType))
                    description.Append(" · ").Append(definition.waterType);
                if (!string.IsNullOrEmpty(definition.realWorldRef))
                    description.Append("\n").Append(definition.realWorldRef);
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
