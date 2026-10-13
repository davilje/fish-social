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
            if (_cards != null)
                return;
            var paper = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            paper.color = CatTheme.Cream;
            CatTheme.ApplyPaper(paper);
            var header = NewRect("PondSelectionHeader", transform);
            Layout(header, new Vector2(0.04f, 0.81f), new Vector2(0.96f, 0.97f));
            AddIcon(header, "MapIcon", "icons/map.png", new Vector2(28f, 0f), new Vector2(48f, 48f));
            var title = AddText(header, "Title", "选择鱼塘", 28);
            Layout(title.rectTransform, new Vector2(0f, 0.48f), Vector2.one);
            title.rectTransform.offsetMin = new Vector2(68f, 0f);
            var description = AddText(header, "Description", "挑选鱼塘后，前往地图查看详情并确认进入。", 17);
            Layout(description.rectTransform, Vector2.zero, new Vector2(1f, 0.46f));
            description.rectTransform.offsetMin = new Vector2(68f, 0f);

            var scrollRoot = NewRect("PondCardScroll", transform);
            Layout(scrollRoot, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.8f));
            _scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;
            var viewport = NewRect("Viewport", scrollRoot);
            Layout(viewport, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<Image>().color = CatTheme.Cream;
            viewport.gameObject.AddComponent<RectMask2D>();
            _cards = NewRect("Cards", viewport);
            _cards.anchorMin = new Vector2(0f, 1f);
            _cards.anchorMax = Vector2.one;
            _cards.pivot = new Vector2(0.5f, 1f);
            _cards.sizeDelta = Vector2.zero;
            var layout = _cards.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _cards.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.viewport = viewport;
            _scroll.content = _cards;
        }

        void BuildCards()
        {
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
                var empty = AddText(_cards, "EmptyCatalog", "暂无可显示的鱼塘，请前往地图检查目录配置。", 18);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;
            }
            _scroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cards);
            _scroll.verticalNormalizedPosition = 1f;
        }

        void BuildCard(WorldMapPondView pond, DesktopGameData.PondDef definition)
        {
            var card = NewRect("PondCard_" + pond.pondId, _cards);
            var layout = card.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 228f;
            layout.preferredHeight = 228f;
            var paper = card.gameObject.AddComponent<Image>();
            paper.color = CatTheme.Cream;
            CatTheme.ApplyPaper(paper);
            var closed = !pond.isOpen || pond.pondCategory == "giant";
            AddIcon(card, "PondIcon", closed ? "icons/lock.png" : "icons/pond.png",
                new Vector2(32f, 82f), new Vector2(38f, 38f));
            var name = AddText(card, "Name", pond.displayName, 23);
            Layout(name.rectTransform, new Vector2(0f, 0.77f), new Vector2(0.73f, 0.97f));
            name.rectTransform.offsetMin = new Vector2(62f, 0f);
            var description = AddText(card, "Description", Describe(pond, definition), 16);
            Layout(description.rectTransform, new Vector2(0.035f, 0.55f), new Vector2(0.72f, 0.76f));
            var fee = AddText(card, "Fee", FeeText(pond), 17);
            Layout(fee.rectTransform, new Vector2(0.035f, 0.3f), new Vector2(0.72f, 0.54f));
            var level = AddText(card, "Level", pond.minPlayerLevel > 0
                ? "需要钓鱼等级 " + pond.minPlayerLevel : "等级要求：无", 17);
            Layout(level.rectTransform, new Vector2(0.035f, 0.12f), new Vector2(0.72f, 0.29f));
            var access = AddText(card, "Access", closed
                ? "暂未开放\n前往地图查看开放条件" : "前往地图查看开放条件", 16);
            Layout(access.rectTransform, new Vector2(0.75f, 0.42f), new Vector2(0.97f, 0.83f));
            access.alignment = TextAnchor.MiddleCenter;
            var actionRect = NewRect("FocusOnMap", card);
            Layout(actionRect, new Vector2(0.75f, 0.12f), new Vector2(0.97f, 0.34f));
            var image = actionRect.gameObject.AddComponent<Image>();
            image.color = CatTheme.Cream;
            var action = actionRect.gameObject.AddComponent<Button>();
            action.targetGraphic = image;
            action.interactable = _focusPond != null;
            var label = AddText(actionRect, "Text", "前往地图", 18);
            Layout(label.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
            label.alignment = TextAnchor.MiddleCenter;
            var pondId = pond.pondId;
            action.onClick.AddListener(() => _focusPond?.Invoke(pondId));
            CatTheme.StyleButton(action, null, true);
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

        static void AddIcon(Transform parent, string name, string path, Vector2 position, Vector2 size)
        {
            var sprite = CatTheme.LoadCozy(path, Vector4.zero);
            if (sprite == null)
                return;
            var rect = NewRect(name, parent);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        static Text AddText(Transform parent, string name, string value, int fontSize)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = CatTheme.Font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.color = CatTheme.Ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return node.GetComponent<RectTransform>();
        }

        static void Layout(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
