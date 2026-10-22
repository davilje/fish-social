using System;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>Native, prefab-authored fishing journal receipt. Statistics come from the server ledger.</summary>
    public sealed class DesktopPondSettlementModalView : MonoBehaviour, IPointerClickHandler
    {
        const string PrefabPath = "Desktop/Prefabs/PanelPondSettlement";
        public event Action<string> ShareRequested;
        public event Action ProfileRequested;
        PondSessionSummaryDto _data;
        int _filter;
        int _sort;
        RectTransform _paper;
        CanvasGroup _paperAlpha;
        Vector2 _paperRest;
        bool _composerWasOpen;
        public bool ChoicesOpen => transform.Find("Panel/ChoiceLayer")?.gameObject.activeSelf == true;
        public int VisibleRecordCount { get; private set; }
        public static DesktopPondSettlementModalView Show(Transform canvas, PondSessionSummaryDto data)
        {
            if (canvas == null || data == null) return null;
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("Missing settlement prefab: " + PrefabPath); return null; }
            var go = Instantiate(prefab, canvas, false);
            var view = go.GetComponent<DesktopPondSettlementModalView>();
            if (view == null || go.transform.Find("Panel/RecordTemplate") == null)
            { Debug.LogError("Settlement requires the authored journal workspace layout.", go); Destroy(go); return null; }
            foreach (var existing in canvas.GetComponentsInChildren<DesktopPondSettlementModalView>(true))
                if (existing != view) Destroy(existing.gameObject);
            view.PresentSummary(data);
            go.SetActive(true); go.transform.SetAsLastSibling();
            view.PlayOpenMotion();
            return view;
        }
        /// <summary>Editor-only prefab layout generation.</summary>
        public static void BuildLayout(Transform root)
        {
#if !UNITY_EDITOR
            return;
#else
            if (Application.isPlaying || root == null)
                return;

            ClearChildren(root);

            var dim = DesktopModalUi.Panel("Dim", root, new Color(0f, 0f, 0f, 0.5f));
            DesktopModalUi.Stretch(dim);

            var panel = DesktopModalUi.Panel("Panel", root, DesktopModalUi.PanelColor);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.1f);
            rt.anchorMax = new Vector2(0.92f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var title = DesktopModalUi.Label(panel.transform, "Title", "离塘结算", 22, TextAnchor.MiddleLeft);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(-24f, 40f);
            titleRt.anchoredPosition = new Vector2(0f, -8f);

            var summary = DesktopModalUi.Label(panel.transform, "Summary", "", 16, TextAnchor.UpperLeft);
            var summaryRt = summary.rectTransform;
            summaryRt.anchorMin = new Vector2(0f, 1f);
            summaryRt.anchorMax = new Vector2(1f, 1f);
            summaryRt.pivot = new Vector2(0.5f, 1f);
            summaryRt.sizeDelta = new Vector2(-24f, 120f);
            summaryRt.anchoredPosition = new Vector2(0f, -52f);
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            summary.verticalOverflow = VerticalWrapMode.Overflow;

            var scrollGo = new GameObject("ListScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0.12f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(12f, 12f);
            scrollRt.offsetMax = new Vector2(-12f, -180f);
            scrollGo.GetComponent<Image>().color = DesktopModalUi.Slot;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            DesktopModalUi.Stretch(viewport);
            var content = new GameObject("List", typeof(RectTransform), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            content.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var list = DesktopModalUi.Label(content.transform, "ListText", "", 14, TextAnchor.UpperLeft);
            var listRt = list.rectTransform;
            listRt.anchorMin = new Vector2(0f, 1f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.pivot = new Vector2(0.5f, 1f);
            listRt.sizeDelta = new Vector2(-16f, 0f);
            listRt.anchoredPosition = Vector2.zero;
            list.horizontalOverflow = HorizontalWrapMode.Wrap;
            list.verticalOverflow = VerticalWrapMode.Overflow;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;

            DesktopModalUi.MakeButton(panel.transform, "Close", "知道了", null);
            var close = DesktopModalUi.FindComponent<Button>(panel.transform, "Close");
            if (close != null)
            {
                var closeRt = close.GetComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(0.35f, 0f);
                closeRt.anchorMax = new Vector2(0.65f, 0f);
                closeRt.offsetMin = new Vector2(0f, 12f);
                closeRt.offsetMax = new Vector2(0f, 48f);
            }
#endif
        }

        public void BuildEditorLayout()
        {
            BuildLayout(transform);
        }

        static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }


        public void PresentSummary(PondSessionSummaryDto data)
        {
            if (data == null) return;
            _data = data; _filter = 0; _sort = 0;
            BindButton("Panel/Close", Close);
            BindButton("Panel/Dismiss", Close);
            BindButton("Panel/Home", () => { ProfileRequested?.Invoke(); Close(); });
            BindButton("Panel/Filter", () => OpenChoices(false));
            BindButton("Panel/Sort", () => OpenChoices(true));
            for (var i = 0; i < 3; i++)
            {
                var option = i;
                BindButton("Panel/ChoiceLayer/FilterMenu/Option" + i, () => SetFilter(option));
                BindButton("Panel/ChoiceLayer/SortMenu/Option" + i, () => SetSort(option));
            }
            CloseChoices();
            Set("Panel/Title", "收竿啦，带上今天的湖畔记忆");
            Set("Panel/Summary", data.pondName + " · " + Timestamp(data.joinedAt, "yyyy 年 M 月 d 日 · HH:mm") + " — " + Timestamp(data.leftAt,"HH:mm") + " · " + (data.returnFeeMode == "auto_return" ? "自动回鱼" : "留鱼出售"));
            Set("Panel/Stats/Bites/Value", data.biteCount + " 次");
            Set("Panel/Stats/Escapes/Value", data.escapeCount + " 次");
            Set("Panel/Stats/Duration/Value", Duration(data.fishingDurationMs));
            Set("Panel/Stats/Catches/Value", (data.catches?.Length ?? 0) + " 条");
            Set("Panel/Account/Income", "+" + data.totalReturnGold + " 金币");
            Set("Panel/Account/Fees", "−" + data.feesPaid + " 金币");
            Set("Panel/Account/PlayerXp", "+" + (data.totalCatchPlayerXp + data.totalReturnPlayerXp));
            Set("Panel/Account/PondXp", "+" + (data.totalCatchPondXp + data.totalReturnPondXp));
            Set("Panel/Account/Net", (data.netProfit >= 0 ? "+" : "") + data.netProfit + " 金币");
            RefreshRecords();
        }

        public void SetFilter(int value) { _filter = Mathf.Clamp(value, 0, 2); CloseChoices(); RefreshRecords(); }
        public void SetSort(int value) { _sort = Mathf.Clamp(value, 0, 2); CloseChoices(); RefreshRecords(); }
        public static bool CanShare(PondSessionCatchEntryDto entry)
        { return entry != null && entry.outcome == "kept" && !string.IsNullOrEmpty(entry.inventoryItemId); }
        public static string Duration(long milliseconds)
        {
            var seconds = Math.Max(0, milliseconds) / 1000;
            return seconds >= 3600 ? seconds / 3600 + " 时 " + (seconds % 3600) / 60 + " 分"
                : seconds >= 60 ? seconds / 60 + " 分 " + seconds % 60 + " 秒" : seconds + " 秒";
        }
        static string Timestamp(long value, string format)
        {
            if (value <= 0) return "—";
            try { return DateTimeOffset.FromUnixTimeMilliseconds(value).LocalDateTime.ToString(format); }
            catch (ArgumentOutOfRangeException) { return "—"; }
        }
        void RefreshRecords()
        {
            if (_data == null) return;
            var content = transform.Find("Panel/ListScroll/Viewport/List") as RectTransform;
            var template = transform.Find("Panel/RecordTemplate");
            if (content == null || template == null) return;
            for (var i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            var rows = new List<PondSessionCatchEntryDto>();
            foreach (var entry in _data.catches ?? new PondSessionCatchEntryDto[0])
                if (entry != null && (_filter == 0 || entry.outcome == (_filter == 1 ? "kept" : "returned"))) rows.Add(entry);
            rows.Sort((a, b) => _sort == 1 ? b.sizeM.CompareTo(a.sizeM) : _sort == 2 ? b.weightKg.CompareTo(a.weightKg) : b.caughtAt.CompareTo(a.caughtAt));
            VisibleRecordCount = rows.Count;
            Set("Panel/RecordCount", "本次鱼获  " + rows.Count + " 条");
            Set("Panel/Filter/Label", new[] { "全部鱼获 ▾", "只看入包 ▾", "只看回塘 ▾" }[_filter]);
            Set("Panel/Sort/Label", new[] { "最近钓获优先 ▾", "体长从大到小 ▾", "体重从大到小 ▾" }[_sort]);
            var empty = transform.Find("Panel/Empty"); if (empty != null) empty.gameObject.SetActive(rows.Count == 0);
            for (var i = 0; i < rows.Count; i++)
            {
                var entry = rows[i];
                var row = Instantiate(template.gameObject, content, false); row.name = "CatchRecord" + i; row.SetActive(true);
                var rect = (RectTransform)row.transform; rect.anchoredPosition = new Vector2(0, -i * 106f);
                SetAt(row.transform, "Name", DesktopFishCatalog.SpeciesName(entry.speciesId));
                SetAt(row.transform, "Time", Timestamp(entry.caughtAt, "MM.dd · HH:mm:ss"));
                SetAt(row.transform, "Quality", DesktopFishCatalog.QualityName(entry.quality) + " · " + (entry.outcome == "returned" ? "已回塘" : "已入包"));
                SetAt(row.transform, "Size", entry.sizeM.ToString("0.00") + " m");
                SetAt(row.transform, "Weight", entry.weightKg > 0 ? entry.weightKg.ToString("0.00") + " kg" : "—");
                var art = row.transform.Find("FishArt").GetComponent<Image>(); art.sprite = CozyItemArt.Fish(entry.speciesId); art.enabled = art.sprite != null;
                var share = row.transform.Find("Share").GetComponent<Button>(); share.interactable = CanShare(entry);
                SetAt(share.transform, "Label", CanShare(entry) ? "分享" : entry.outcome == "returned" ? "已回塘" : "未留存");
                share.onClick.RemoveAllListeners(); share.onClick.AddListener(() => { _composerWasOpen = true; ShareRequested?.Invoke(entry.inventoryItemId); });
            }
            content.sizeDelta = new Vector2(0, Mathf.Max(318, rows.Count * 106f - 10));
            var scroll = transform.Find("Panel/ListScroll").GetComponent<ScrollRect>(); scroll.verticalNormalizedPosition = 1;
        }
        void BindButton(string path, UnityEngine.Events.UnityAction action)
        {
            var button = transform.Find(path)?.GetComponent<Button>(); if (button == null) return;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(action);
        }
        void Set(string path, string value) { SetAt(transform, path, value); }
        static void SetAt(Transform parent, string path, string value)
        { var label = parent.Find(path)?.GetComponent<Text>(); if (label != null) label.text = value; }
        public void OpenChoices(bool sorting)
        {
            var layer = transform.Find("Panel/ChoiceLayer"); if (layer == null) return;
            layer.gameObject.SetActive(true);
            layer.Find("FilterMenu").gameObject.SetActive(!sorting);
            layer.Find("SortMenu").gameObject.SetActive(sorting);
        }
        public void CloseChoices()
        { var layer = transform.Find("Panel/ChoiceLayer"); if (layer != null) layer.gameObject.SetActive(false); }
        public void PlayOpenMotion()
        {
            if (_paper == null)
            {
                _paper = transform.Find("Panel") as RectTransform; if (_paper == null) return;
                _paperRest = _paper.anchoredPosition;
                _paperAlpha = _paper.GetComponent<CanvasGroup>();
                if (_paperAlpha == null) _paperAlpha = _paper.gameObject.AddComponent<CanvasGroup>();
            }
            ResetOpenMotion();
            var offset = CozyMotion.Reduced ? 0 : 16;
            _paper.anchoredPosition = _paperRest + Vector2.down * offset;
            _paperAlpha.alpha = 0;
            CozyMotion.Animate(_paper, .2f, t =>
            {
                if (_paper == null || _paperAlpha == null) return;
                _paper.anchoredPosition = _paperRest + Vector2.down * (offset * (1 - t));
                _paperAlpha.alpha = Mathf.Clamp01(t);
            }, RestorePaperPose, CozyWebMotion.Ease.OutCubic, cleanup: RestorePaperPose);
        }
        void RestorePaperPose()
        {
            if (_paper != null) _paper.anchoredPosition = _paperRest;
            if (_paperAlpha != null) _paperAlpha.alpha = 1;
        }
        void ResetOpenMotion()
        { if (_paper != null) CozyMotion.Cancel(_paper); RestorePaperPose(); }
        void OnDisable() { ResetOpenMotion(); CloseChoices(); }
        bool ComposerIsOpen()
        {
            if (transform.parent == null) return false;
            foreach (var modal in transform.parent.GetComponentsInChildren<DesktopSocialComposeModal>(false))
                if (modal.gameObject.activeInHierarchy) return true;
            return false;
        }
        void Update()
        {
            var composerOpen = ComposerIsOpen();
            // Retain the previous frame too: the front composer may consume Escape and hide before our Update.
            if (Input.GetKeyDown(KeyCode.Escape) && !composerOpen && !_composerWasOpen)
            { if (ChoicesOpen) CloseChoices(); else Close(); }
            _composerWasOpen = composerOpen;
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || ComposerIsOpen()) return;
            var target = eventData.pointerCurrentRaycast.gameObject;
            var layer = transform.Find("Panel/ChoiceLayer");
            if (layer != null && target == layer.gameObject) { CloseChoices(); return; }
            if (target == gameObject || target == transform.Find("Dim")?.gameObject) Close();
        }
        public void Close() { ResetOpenMotion(); gameObject.SetActive(false); Destroy(gameObject); }
    }
}
