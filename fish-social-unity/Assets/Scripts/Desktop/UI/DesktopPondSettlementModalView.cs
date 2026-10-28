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
        bool _closing;
        bool _pendingOpen;
        CozyWorkspaceBodyMotion _bodyMotion;
        float _bodySeconds;
        Image _dim;
        Color _dimRest;
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

            var title = DesktopModalUi.Label(panel.transform, "Title", DesktopLocalization.Text("ui.catch.settlement.title.fallback", "离塘结算"), 22, TextAnchor.MiddleLeft);
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

            DesktopModalUi.MakeButton(panel.transform, "Close", DesktopLocalization.Text("ui.catch.settlement.button.dismiss", "知道了"), null);
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
            BindButton("Panel/Home", () => CloseThen(() => ProfileRequested?.Invoke()));
            BindButton("Panel/Filter", () => OpenChoices(false));
            BindButton("Panel/Sort", () => OpenChoices(true));
            for (var i = 0; i < 3; i++)
            {
                var option = i;
                BindButton("Panel/ChoiceLayer/FilterMenu/Option" + i, () => SetFilter(option));
                BindButton("Panel/ChoiceLayer/SortMenu/Option" + i, () => SetSort(option));
            }
            CloseChoices();
            Set("Panel/Title", DesktopLocalization.Text("ui.catch.settlement.title.main", "收竿啦，带上今天的湖畔记忆"));
            Set("Panel/Summary", data.pondName + " · " + Timestamp(data.joinedAt, DesktopLocalization.Text("ui.catch.settlement.format.date_time", "yyyy 年 M 月 d 日 · HH:mm")) + " — " + Timestamp(data.leftAt,"HH:mm") + " · " + (data.returnFeeMode == "auto_return" ? DesktopLocalization.Text("ui.catch.settlement.mode.auto_release", "自动回鱼") : DesktopLocalization.Text("ui.catch.settlement.mode.keep_to_sell", "留鱼出售")));
            Set("Panel/Stats/Bites/Value", DesktopLocalization.Format("ui.catch.settlement.format.bite_count", (object)(data.biteCount)));
            Set("Panel/Stats/Escapes/Value", DesktopLocalization.Format("ui.catch.settlement.format.bite_count", (object)(data.escapeCount)));
            Set("Panel/Stats/Duration/Value", Duration(data.fishingDurationMs));
            Set("Panel/Stats/Catches/Value", DesktopLocalization.Format("ui.catch.settlement.format.fish_count", (object)((data.catches?.Length ?? 0))));
            Set("Panel/Account/Income", DesktopLocalization.Format("ui.catch.settlement.format.income", (object)(data.totalReturnGold)));
            Set("Panel/Account/Fees", DesktopLocalization.Format("ui.catch.settlement.format.expense", (object)(data.feesPaid)));
            Set("Panel/Account/PlayerXp", "+" + (data.totalCatchPlayerXp + data.totalReturnPlayerXp));
            Set("Panel/Account/PondXp", "+" + (data.totalCatchPondXp + data.totalReturnPondXp));
            Set("Panel/Account/Net", DesktopLocalization.Format("ui.catch.settlement.format.net_coins", (object)((data.netProfit >= 0 ? "+" : "")), (object)(data.netProfit)));
            RefreshRecords();
        }

        public void SetFilter(int value) { _filter = Mathf.Clamp(value, 0, 2); CloseChoices(); RefreshRecords(); }
        public void SetSort(int value) { _sort = Mathf.Clamp(value, 0, 2); CloseChoices(); RefreshRecords(); }
        public static bool CanShare(PondSessionCatchEntryDto entry)
        { return entry != null && entry.outcome == "kept" && !string.IsNullOrEmpty(entry.inventoryItemId); }
        public static string Duration(long milliseconds)
        {
            var seconds = Math.Max(0, milliseconds) / 1000;
            return seconds >= 3600 ? DesktopLocalization.Format("ui.catch.settlement.format.hours_minutes", (object)(seconds / 3600), (object)((seconds % 3600) / 60))
                : seconds >= 60 ? DesktopLocalization.Format("ui.catch.settlement.format.minutes_seconds", (object)(seconds / 60), (object)(seconds % 60)) : DesktopLocalization.Format("ui.catch.settlement.format.seconds", (object)(seconds));
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
            Set("Panel/RecordCount", DesktopLocalization.Format("ui.catch.settlement.title.catch_count", (object)(rows.Count)));
            Set("Panel/Filter/Label", new[] { DesktopLocalization.Text("ui.catch.settlement.dropdown.all_catches", "全部鱼获 ▾"), DesktopLocalization.Text("ui.catch.settlement.dropdown.kept", "只看入包 ▾"), DesktopLocalization.Text("ui.catch.settlement.dropdown.released", "只看回塘 ▾") }[_filter]);
            Set("Panel/Sort/Label", new[] { DesktopLocalization.Text("ui.catch.settlement.dropdown.newest_first", "最近钓获优先 ▾"), DesktopLocalization.Text("ui.catch.settlement.dropdown.length_descending", "体长从大到小 ▾"), DesktopLocalization.Text("ui.catch.settlement.dropdown.weight_descending", "体重从大到小 ▾") }[_sort]);
            var empty = transform.Find("Panel/Empty"); if (empty != null) empty.gameObject.SetActive(rows.Count == 0);
            for (var i = 0; i < rows.Count; i++)
            {
                var entry = rows[i];
                var row = Instantiate(template.gameObject, content, false); row.name = "CatchRecord" + i; row.SetActive(true);
                var rect = (RectTransform)row.transform; rect.anchoredPosition = new Vector2(0, -i * 106f);
                SetAt(row.transform, "Name", DesktopFishCatalog.SpeciesName(entry.speciesId));
                SetAt(row.transform, "Time", Timestamp(entry.caughtAt, "MM.dd · HH:mm:ss"));
                SetAt(row.transform, "Quality", DesktopFishCatalog.QualityName(entry.quality) + " · " + (entry.outcome == "returned" ? DesktopLocalization.Text("ui.catch.settlement.status.released", "已回塘") : DesktopLocalization.Text("ui.catch.settlement.status.kept", "已入包")));
                SetAt(row.transform, "Size", entry.sizeM.ToString("0.00") + " m");
                SetAt(row.transform, "Weight", entry.weightKg > 0 ? entry.weightKg.ToString("0.00") + " kg" : "—");
                var art = row.transform.Find("FishArt").GetComponent<Image>(); art.sprite = CozyItemArt.Fish(entry.speciesId); art.enabled = art.sprite != null;
                var share = row.transform.Find("Share").GetComponent<Button>(); share.interactable = CanShare(entry);
                CozyWorkspaceInteraction.Bind(share);
                SetAt(share.transform, "Label", CanShare(entry) ? DesktopLocalization.Text("ui.catch.settlement.button.share", "分享") : entry.outcome == "returned" ? DesktopLocalization.Text("ui.catch.settlement.status.released", "已回塘") : DesktopLocalization.Text("ui.catch.settlement.status.not_kept", "未留存"));
                share.onClick.RemoveAllListeners(); share.onClick.AddListener(() => { _composerWasOpen = true; ShareRequested?.Invoke(entry.inventoryItemId); });
            }
            content.sizeDelta = new Vector2(0, Mathf.Max(318, rows.Count * 106f - 10));
            var scroll = transform.Find("Panel/ListScroll").GetComponent<ScrollRect>(); scroll.verticalNormalizedPosition = 1;
        }
        void BindButton(string path, UnityEngine.Events.UnityAction action)
        {
            var button = transform.Find(path)?.GetComponent<Button>(); if (button == null) return;
            if (path == "Panel/Home") CozyWorkspaceInteraction.Bind(button);
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
            // Also bind explicit Show/preview calls; do not rely on an earlier OnEnable.
            CozyMotion.PausedChanged -= OnMotionPauseChanged;
            CozyMotion.PausedChanged += OnMotionPauseChanged;
            if (_paper == null)
            {
                _paper = transform.Find("Panel") as RectTransform; if (_paper == null) return;
                _paperRest = _paper.anchoredPosition;
                _paperAlpha = _paper.GetComponent<CanvasGroup>();
                if (_paperAlpha == null) _paperAlpha = _paper.gameObject.AddComponent<CanvasGroup>();
            }
            ResetOpenMotion();
            _closing = false;
            if (_dim == null)
            {
                _dim = transform.Find("Dim")?.GetComponent<Image>();
                if (_dim != null) _dimRest = _dim.color;
            }
            SetDimProgress(0);
            _pendingOpen = CozyMotion.Paused;
            if (_pendingOpen) { _paperAlpha.alpha = 0; return; }
            _paperAlpha.interactable = true;
            CozyMotion.OpenJournalSheet(_paper, _paperRest, progress: SetDimProgress);
            _bodyMotion = _paper.GetComponent<CozyWorkspaceBodyMotion>() ?? _paper.gameObject.AddComponent<CozyWorkspaceBodyMotion>();
            _bodyMotion.Configure(
                new[] { _paper.Find("Stats/Bites") }, new[] { _paper.Find("Stats/Escapes") },
                new[] { _paper.Find("Stats/Duration") }, new[] { _paper.Find("Stats/Catches") },
                new[] { _paper.Find("RecordCount"), _paper.Find("Filter"), _paper.Find("Sort"), _paper.Find("ListScroll"), _paper.Find("Empty"), _paper.Find("DurationHint") },
                new[] { _paper.Find("Account") },
                new[] { _paper.Find("FooterNote"), _paper.Find("Home"), _paper.Find("FooterRule") });
            _bodySeconds = 0;
            _bodyMotion.Sample(0);
            CozyMotion.Animate((RectTransform)transform, .68f,
                t => { _bodySeconds = CozyMotion.Reduced ? t*.12f : t*.68f-.2f; _bodyMotion.Sample(_bodySeconds); },
                _bodyMotion.Finish, CozyWebMotion.Ease.Linear, cleanup: _bodyMotion.Finish, fadeWhenReduced: true);
        }
        void RestorePaperPose()
        {
            if (_paper != null) _paper.anchoredPosition = _paperRest;
            if (_paperAlpha != null) _paperAlpha.alpha = 1;
            SetDimProgress(1);
        }
        void SetDimProgress(float t)
        {
            if (_dim != null) _dim.color = new Color(_dimRest.r, _dimRest.g, _dimRest.b, _dimRest.a*t);
        }
        void ResetOpenMotion()
        {
            CozyMotion.Cancel((RectTransform)transform);
            if (_paper != null) CozyMotion.Cancel(_paper);
            _bodyMotion?.Finish();
            RestorePaperPose();
        }
        void OnEnable() { CozyMotion.PausedChanged += OnMotionPauseChanged; }
        void OnMotionPauseChanged(bool paused)
        {
            if (!paused && _pendingOpen && !_closing) PlayOpenMotion();
        }
        void OnDisable()
        {
            CozyMotion.PausedChanged -= OnMotionPauseChanged;
            _pendingOpen = false;
            ResetOpenMotion(); CloseChoices();
        }
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
        public void Close() => CloseThen(null);
        void CloseThen(System.Action completed)
        {
            if (_closing) return;
            _closing = true;
            CloseChoices();
            if (_paper == null) { Remove(); return; }
            // Freeze the current entrance pose, including an early close, before fading it out.
            var position = _paper.anchoredPosition;
            var opacity = _paperAlpha.alpha;
            var dim = transform.Find("Dim")?.GetComponent<Image>();
            var tint = dim != null ? dim.color : Color.clear;
            CozyMotion.Cancel((RectTransform)transform);
            CozyMotion.Cancel(_paper);
            _bodyMotion?.Sample(_bodySeconds);
            _paper.anchoredPosition = position;
            _paperAlpha.alpha = opacity;
            if (dim != null) dim.color = tint;
            _paperAlpha.interactable = false;
            CozyMotion.Animate(_paper, .14f, t =>
            {
                _paper.anchoredPosition = position + (CozyMotion.Reduced ? Vector2.zero : Vector2.down*12*t);
                _paperAlpha.alpha = opacity*(1-t);
                if (dim != null) dim.color = new Color(tint.r,tint.g,tint.b,tint.a*(1-t));
            }, Remove, CozyWebMotion.Ease.InQuad, fadeWhenReduced: true);
            void Remove()
            {
                gameObject.SetActive(false);
                completed?.Invoke();
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }
    }
}
