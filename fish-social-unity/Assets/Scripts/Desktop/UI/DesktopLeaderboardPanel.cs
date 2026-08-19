using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Server-authoritative leaderboard page. Prefab owns layout; this view only
    /// binds REST data. Opening or switching boards never calls leave_pond.
    /// </summary>
    public sealed class DesktopLeaderboardPanel : MonoBehaviour
    {
        const int PageLimit = 50;
        const int RowsPerFrame = 12;
        const float TabDebounceSeconds = 0.12f;
        const string BoardDaily = "daily_biggest";
        const string BoardWeekly = "weekly_king";
        const string BoardPond = "pond";
        const string BoardRare = "rare";

        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        Text _status;
        Text _myRank;
        Button _dailyTab;
        Button _weeklyTab;
        Button _pondTab;
        Button _rareTab;
        Button _retry;
        ScrollRect _scroll;
        RectTransform _content;
        PodiumSlot _podium1;
        PodiumSlot _podium2;
        PodiumSlot _podium3;
        string _boardType = BoardDaily;
        Coroutine _loadRoutine;
        Coroutine _debounceRoutine;
        Coroutine _boardRequest;
        Coroutine _myRankRequest;
        int _loadGeneration;
        readonly List<GameObject> _rowPool = new List<GameObject>(48);

        struct PodiumSlot
        {
            public GameObject root;
            public Text nickname;
            public Image avatar;
            public Text value;
        }

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond)
        {
            _api = api;
            _pond = pond;
            EnsureUi();
        }

        public void OnOpened()
        {
            RefreshNow();
        }

        public void OnClosed()
        {
            CancelLoads();
            _loadGeneration++;
        }

        public void BuildEditorLayout()
        {
            if (Find<Button>("Tabs/Daily") == null ||
                Find<RectTransform>("Scroll/Viewport/Content") == null ||
                Find<Transform>("Podium/Slot1") == null)
            {
                ClearChildren();
                BuildFallbackUi();
            }
        }

        void EnsureUi()
        {
            var panelImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.10f, 0.14f, 1f);

            _dailyTab = Find<Button>("Tabs/Daily");
            _weeklyTab = Find<Button>("Tabs/Weekly");
            _pondTab = Find<Button>("Tabs/Pond");
            _rareTab = Find<Button>("Tabs/Rare");
            _status = Find<Text>("Status");
            _myRank = Find<Text>("MyRank");
            _retry = Find<Button>("Retry");
            _scroll = Find<ScrollRect>("Scroll");
            _content = Find<RectTransform>("Scroll/Viewport/Content");
            _podium1 = ResolvePodium("Podium/Slot1");
            _podium2 = ResolvePodium("Podium/Slot2");
            _podium3 = ResolvePodium("Podium/Slot3");

            if (_dailyTab == null || _weeklyTab == null || _pondTab == null ||
                _rareTab == null || _status == null || _myRank == null ||
                _scroll == null || _content == null ||
                _podium1.root == null || _podium2.root == null || _podium3.root == null)
            {
                Debug.LogWarning(
                    "[DesktopUI] PanelLeaderboard Prefab 缺少必需控件，使用运行时回退布局。" +
                    "请在 Unity Prefab Manager 中执行“初始化”。");
                BuildFallbackUi();
            }

            if (_dailyTab == null || _weeklyTab == null || _pondTab == null ||
                _rareTab == null || _scroll == null || _content == null)
                return;

            _scroll.horizontal = false;
            _scroll.vertical = true;

            BindTab(_dailyTab, BoardDaily);
            BindTab(_weeklyTab, BoardWeekly);
            BindTab(_pondTab, BoardPond);
            BindTab(_rareTab, BoardRare);
            if (_retry != null)
            {
                _retry.onClick.RemoveAllListeners();
                _retry.onClick.AddListener(RefreshNow);
            }
        }

        void BindTab(Button button, string boardType)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (_boardType == boardType)
                    return;
                _boardType = boardType;
                ScheduleRefresh();
            });
        }

        void ScheduleRefresh()
        {
            if (_debounceRoutine != null)
                StopCoroutine(_debounceRoutine);
            _debounceRoutine = StartCoroutine(DebouncedRefresh());
        }

        IEnumerator DebouncedRefresh()
        {
            yield return new WaitForSecondsRealtime(TabDebounceSeconds);
            _debounceRoutine = null;
            RefreshNow();
        }

        void RefreshNow()
        {
            if (_debounceRoutine != null)
            {
                StopCoroutine(_debounceRoutine);
                _debounceRoutine = null;
            }
            CancelLoads();
            var generation = ++_loadGeneration;
            _loadRoutine = StartCoroutine(LoadRoutine(generation));
        }

        void CancelLoads()
        {
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }
            if (_boardRequest != null)
            {
                StopCoroutine(_boardRequest);
                _boardRequest = null;
            }
            if (_myRankRequest != null)
            {
                StopCoroutine(_myRankRequest);
                _myRankRequest = null;
            }
        }

        IEnumerator LoadRoutine(int generation)
        {
            SetStatus("正在加载排行榜…");

            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前没有有效的 Steam 会话。");
                HideAllRows();
                RenderPodium(null, null, null, _boardType);
                SetMyRank(null, _boardType);
                _loadRoutine = null;
                yield break;
            }

            var boardType = _boardType;
            var pondId = _pond != null ? _pond.CurrentPondId : null;
            if (boardType == BoardPond && string.IsNullOrEmpty(pondId))
            {
                SetStatus("请先进入鱼塘后再查看鱼塘榜。");
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                SetMyRank(null, boardType);
                _loadRoutine = null;
                yield break;
            }

            var boardDone = false;
            var boardOk = false;
            LeaderboardEntryDto[] entries = null;
            string boardError = null;
            var myDone = false;
            var myOk = false;
            LeaderboardMyRankDto myRank = null;

            _boardRequest = StartCoroutine(RunEnumerator(
                _api.GetLeaderboard(boardType, pondId, PageLimit,
                    (success, items, periodKey, message) =>
                    {
                        boardOk = success;
                        entries = items;
                        boardError = message;
                    }),
                () =>
                {
                    boardDone = true;
                    _boardRequest = null;
                }));
            _myRankRequest = StartCoroutine(RunEnumerator(
                _api.GetMyLeaderboardRank(boardType, pondId,
                    (success, rank, message) =>
                    {
                        myOk = success;
                        if (success)
                            myRank = rank;
                    }),
                () =>
                {
                    myDone = true;
                    _myRankRequest = null;
                }));

            while (!boardDone || !myDone)
            {
                if (generation != _loadGeneration)
                    yield break;
                yield return null;
            }

            if (generation != _loadGeneration)
                yield break;

            if (!boardOk)
            {
                SetStatus(boardError ?? "排行榜加载失败，请点击重试。");
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                SetMyRank(null, boardType);
                _loadRoutine = null;
                yield break;
            }

            if (entries == null || entries.Length == 0)
            {
                SetStatus("当前榜单暂无数据。");
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                SetMyRank(myOk ? myRank : null, boardType);
                _loadRoutine = null;
                yield break;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null ||
                    string.IsNullOrEmpty(entries[i].playerId) ||
                    entries[i].rank <= 0)
                {
                    SetStatus("服务端响应缺少排行榜字段，请重试。");
                    HideAllRows();
                    RenderPodium(null, null, null, boardType);
                    SetMyRank(null, boardType);
                    _loadRoutine = null;
                    yield break;
                }
            }

            var first = FindRank(entries, 1);
            var second = FindRank(entries, 2);
            var third = FindRank(entries, 3);
            RenderPodium(first, second, third, boardType);

            yield return BindRows(entries, boardType, generation);
            if (generation != _loadGeneration)
                yield break;

            SetMyRank(myOk ? myRank : null, boardType);
            SetStatus(string.Empty);
            _loadRoutine = null;
        }

        static IEnumerator RunEnumerator(IEnumerator routine, System.Action onCompleted)
        {
            if (routine != null)
                yield return routine;
            onCompleted?.Invoke();
        }

        IEnumerator BindRows(
            LeaderboardEntryDto[] entries, string boardType, int generation)
        {
            var needed = 0;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].rank >= 4)
                    needed++;
            }

            var bound = 0;
            var sourceIndex = 0;
            while (bound < needed)
            {
                if (generation != _loadGeneration)
                    yield break;

                var batchEnd = Mathf.Min(bound + RowsPerFrame, needed);
                while (bound < batchEnd)
                {
                    while (sourceIndex < entries.Length && entries[sourceIndex].rank < 4)
                        sourceIndex++;
                    if (sourceIndex >= entries.Length)
                        break;

                    EnsureRowSlot(bound);
                    var row = _rowPool[bound];
                    row.SetActive(true);
                    BindRow(row, entries[sourceIndex], boardType);
                    bound++;
                    sourceIndex++;
                }

                if (bound < needed)
                    yield return null;
            }

            for (var i = needed; i < _rowPool.Count; i++)
            {
                if (_rowPool[i] != null)
                    _rowPool[i].SetActive(false);
            }

            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
        }

        void EnsureRowSlot(int index)
        {
            while (_rowPool.Count <= index)
                _rowPool.Add(CreateRowObject());
            if (_rowPool[index] == null)
                _rowPool[index] = CreateRowObject();
        }

        GameObject CreateRowObject()
        {
            if (_content == null)
                return null;
            var row = DesktopUiPrefabFactory.Instantiate("LeaderboardRow", _content);
            if (row != null)
                return row;

            row = new GameObject("LeaderboardRow", typeof(RectTransform),
                typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_content, false);
            row.GetComponent<Image>().color = new Color(0.13f, 0.18f, 0.23f, 1f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = 40f;
            element.preferredHeight = 40f;
            CreateText(row.transform, "Rank", "#4", 80f);
            CreateText(row.transform, "Nickname", "钓友", 180f);
            CreateText(row.transform, "Value", "成绩", 220f);
            return row;
        }

        void BindRow(GameObject row, LeaderboardEntryDto entry, string boardType)
        {
            if (row == null || entry == null)
                return;
            SetChildText(row, "Rank", "#" + entry.rank);
            SetChildText(row, "Nickname", entry.nickname ?? "钓友");
            SetChildText(row, "Value", FormatValue(entry, boardType));
        }

        void HideAllRows()
        {
            for (var i = 0; i < _rowPool.Count; i++)
            {
                if (_rowPool[i] != null)
                    _rowPool[i].SetActive(false);
            }
        }

        static LeaderboardEntryDto FindRank(LeaderboardEntryDto[] entries, int rank)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].rank == rank)
                    return entries[i];
            }
            return null;
        }

        void RenderPodium(
            LeaderboardEntryDto first,
            LeaderboardEntryDto second,
            LeaderboardEntryDto third,
            string boardType)
        {
            BindPodium(_podium1, first, "虚位以待", boardType);
            BindPodium(_podium2, second, "虚位以待", boardType);
            BindPodium(_podium3, third, "虚位以待", boardType);
        }

        void BindPodium(
            PodiumSlot slot, LeaderboardEntryDto entry, string emptyLabel, string boardType)
        {
            if (slot.nickname == null || slot.value == null)
                return;
            if (entry == null)
            {
                slot.nickname.text = emptyLabel;
                slot.value.text = "—";
                if (slot.avatar != null)
                    slot.avatar.color = new Color(0.18f, 0.24f, 0.30f, 1f);
                return;
            }

            slot.nickname.text = entry.nickname ?? "钓友";
            slot.value.text = FormatValue(entry, boardType);
            if (slot.avatar != null)
                slot.avatar.color = new Color(0.28f, 0.42f, 0.52f, 1f);
        }

        static string FormatValue(LeaderboardEntryDto entry, string boardType)
        {
            if (entry == null)
                return "—";
            var extra = entry.extra;
            if (boardType == BoardDaily)
            {
                var size = extra != null && extra.sizeM > 0 ? extra.sizeM : entry.value;
                var species = extra != null && !string.IsNullOrEmpty(extra.speciesId)
                    ? extra.speciesId
                    : "鱼获";
                return species + " · " + size.ToString("0.00") + "m";
            }
            if (boardType == BoardWeekly)
            {
                var count = extra != null ? extra.catchCount : 0;
                return Mathf.RoundToInt(entry.value) + " 金币" +
                       (count > 0 ? " · " + count + " 条" : string.Empty);
            }
            if (boardType == BoardPond)
            {
                var max = extra != null && extra.sizeM > 0
                    ? " · 最大 " + extra.sizeM.ToString("0.00") + "m"
                    : string.Empty;
                return Mathf.RoundToInt(entry.value) + " 条" + max;
            }

            var rareMax = extra != null && extra.sizeM > 0
                ? " · " + extra.sizeM.ToString("0.00") + "m"
                : string.Empty;
            return "史诗+ " + Mathf.RoundToInt(entry.value) + " 条" + rareMax;
        }

        void SetMyRank(LeaderboardMyRankDto myRank, string boardType)
        {
            if (_myRank == null)
                return;
            if (myRank == null || !myRank.hasRank)
            {
                _myRank.text = "我的排名：未上榜";
                return;
            }
            _myRank.text = "我的排名：#" + myRank.rank + " · " +
                           FormatValue(myRank.entry ?? new LeaderboardEntryDto
                           {
                               value = myRank.value,
                               extra = myRank.entry != null ? myRank.entry.extra : null,
                           }, boardType);
        }

        void SetStatus(string value)
        {
            if (_status != null)
                _status.text = value ?? string.Empty;
        }

        PodiumSlot ResolvePodium(string path)
        {
            var root = transform.Find(path);
            if (root == null)
                return default;
            return new PodiumSlot
            {
                root = root.gameObject,
                nickname = FindIn<Text>(root, "Nickname"),
                avatar = FindIn<Image>(root, "Avatar"),
                value = FindIn<Text>(root, "Value"),
            };
        }

        T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }

        static T FindIn<T>(Transform root, string name) where T : Component
        {
            var node = root != null ? root.Find(name) : null;
            return node != null ? node.GetComponent<T>() : null;
        }

        static void SetChildText(GameObject root, string name, string value)
        {
            var node = root.transform.Find(name);
            var text = node != null ? node.GetComponent<Text>() : null;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        static Text CreateText(Transform parent, string name, string value, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = 28f;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.text = value ?? string.Empty;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        void ClearChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        void BuildFallbackUi()
        {
            var tabs = NewGo("Tabs", transform, typeof(HorizontalLayoutGroup));
            PlaceTop(tabs.GetComponent<RectTransform>(), 48f, 8f);
            var tabsLayout = tabs.GetComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 8f;
            tabsLayout.childControlWidth = false;
            tabsLayout.childControlHeight = true;
            _dailyTab = MakeButton(tabs.transform, "Daily", "日榜", 110f);
            _weeklyTab = MakeButton(tabs.transform, "Weekly", "周榜", 110f);
            _pondTab = MakeButton(tabs.transform, "Pond", "鱼塘榜", 110f);
            _rareTab = MakeButton(tabs.transform, "Rare", "稀有榜", 110f);

            var podium = NewGo("Podium", transform, typeof(HorizontalLayoutGroup));
            var podiumRt = podium.GetComponent<RectTransform>();
            podiumRt.anchorMin = new Vector2(0f, 1f);
            podiumRt.anchorMax = new Vector2(1f, 1f);
            podiumRt.pivot = new Vector2(0.5f, 1f);
            podiumRt.anchoredPosition = new Vector2(0f, -56f);
            podiumRt.sizeDelta = new Vector2(-24f, 150f);
            var podiumLayout = podium.GetComponent<HorizontalLayoutGroup>();
            podiumLayout.spacing = 12f;
            podiumLayout.childAlignment = TextAnchor.MiddleCenter;
            podiumLayout.childControlWidth = true;
            podiumLayout.childControlHeight = true;
            podiumLayout.childForceExpandWidth = true;
            _podium2 = MakePodiumSlot(podium.transform, "Slot2", "第2名");
            _podium1 = MakePodiumSlot(podium.transform, "Slot1", "第1名");
            _podium3 = MakePodiumSlot(podium.transform, "Slot3", "第3名");

            var statusGo = NewGo("Status", transform, typeof(Text));
            _status = statusGo.GetComponent<Text>();
            _status.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _status.fontSize = 15;
            _status.color = Color.white;
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 1f);
            statusRt.anchorMax = new Vector2(1f, 1f);
            statusRt.pivot = new Vector2(0.5f, 1f);
            statusRt.anchoredPosition = new Vector2(0f, -214f);
            statusRt.sizeDelta = new Vector2(-24f, 28f);

            var scroll = NewGo("Scroll", transform, typeof(Image), typeof(ScrollRect));
            scroll.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.10f, 1f);
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(12f, 52f);
            scrollRt.offsetMax = new Vector2(-12f, -248f);
            var viewport = NewGo("Viewport", scroll.transform, typeof(RectMask2D));
            Stretch(viewport.GetComponent<RectTransform>());
            var content = NewGo("Content", viewport.transform,
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 8f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            content.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            _scroll = scroll.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.viewport = viewport.GetComponent<RectTransform>();
            _scroll.content = _content;

            var myRankGo = NewGo("MyRank", transform, typeof(Text));
            _myRank = myRankGo.GetComponent<Text>();
            _myRank.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _myRank.fontSize = 15;
            _myRank.color = Color.white;
            var myRankRt = myRankGo.GetComponent<RectTransform>();
            myRankRt.anchorMin = new Vector2(0f, 0f);
            myRankRt.anchorMax = new Vector2(1f, 0f);
            myRankRt.pivot = new Vector2(0.5f, 0f);
            myRankRt.anchoredPosition = new Vector2(0f, 12f);
            myRankRt.sizeDelta = new Vector2(-140f, 28f);

            _retry = MakeButton(transform, "Retry", "重试", 100f);
            var retryRt = _retry.GetComponent<RectTransform>();
            retryRt.anchorMin = new Vector2(1f, 0f);
            retryRt.anchorMax = new Vector2(1f, 0f);
            retryRt.pivot = new Vector2(1f, 0f);
            retryRt.anchoredPosition = new Vector2(-12f, 10f);
            retryRt.sizeDelta = new Vector2(100f, 32f);
        }

        PodiumSlot MakePodiumSlot(Transform parent, string name, string title)
        {
            var root = NewGo(name, parent, typeof(Image), typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            root.GetComponent<Image>().color = new Color(0.13f, 0.18f, 0.23f, 1f);
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            root.GetComponent<LayoutElement>().minHeight = 140f;
            var avatar = NewGo("Avatar", root.transform, typeof(Image), typeof(LayoutElement));
            avatar.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.30f, 1f);
            avatar.GetComponent<LayoutElement>().minHeight = 56f;
            avatar.GetComponent<LayoutElement>().preferredHeight = 56f;
            var nickname = CreateText(root.transform, "Nickname", title, -1f);
            nickname.alignment = TextAnchor.MiddleCenter;
            var value = CreateText(root.transform, "Value", "—", -1f);
            value.alignment = TextAnchor.MiddleCenter;
            return new PodiumSlot
            {
                root = root,
                nickname = nickname,
                avatar = avatar.GetComponent<Image>(),
                value = value,
            };
        }

        static Button MakeButton(Transform parent, string name, string label, float width)
        {
            var go = NewGo(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
            go.GetComponent<Image>().color = new Color(0.18f, 0.27f, 0.34f, 1f);
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = 32f;
            element.preferredHeight = 32f;
            var text = CreateText(go.transform, "Label", label, width);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform);
            return go.GetComponent<Button>();
        }

        static GameObject NewGo(string name, Transform parent, params System.Type[] extras)
        {
            var types = new System.Type[1 + extras.Length];
            types[0] = typeof(RectTransform);
            for (var i = 0; i < extras.Length; i++)
                types[i + 1] = extras[i];
            var go = new GameObject(name, types);
            go.transform.SetParent(parent, false);
            return go;
        }

        static void PlaceTop(RectTransform rt, float height, float top)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(-24f, height);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
