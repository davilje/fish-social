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
        bool _uiReady;
        const int PageLimit = 50;
        const int RowsPerFrame = 12;
        const float ClientCacheSeconds = 300f;
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
        Coroutine _prefetchRoutine;
        int _loadGeneration;
        int _prefetchEpoch;
        readonly List<GameObject> _rowPool = new List<GameObject>(48);
        readonly Dictionary<string, BoardCache> _clientCache =
            new Dictionary<string, BoardCache>();

        struct BoardCache
        {
            public LeaderboardEntryDto[] entries;
            public LeaderboardMyRankDto myRank;
            public bool hasMyRank;
            public float fetchedAt;
        }

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
            if (!_uiReady)
                return;
            _boardType = BoardDaily;
            EnsureUi();
            RefreshNow();
            StartPrefetchOthers(BoardDaily);
        }

        public void OnClosed()
        {
            CancelLoads();
            StopPrefetch();
            _loadGeneration++;
        }

        public void BuildEditorLayout()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            if (Find<Button>("Tabs/Daily") == null ||
                Find<RectTransform>("Scroll/Viewport/Content") == null ||
                Find<Transform>("Podium/Slot1") == null)
            {
                ClearChildren();
                BuildFallbackUi();
            }
            EnsureUi();
#endif
        }

        void EnsureUi()
        {
            _uiReady = false;

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
                _scroll == null || _content == null || _retry == null ||
                _podium1.root == null || _podium2.root == null || _podium3.root == null ||
                _podium1.nickname == null || _podium2.nickname == null || _podium3.nickname == null ||
                _podium1.value == null || _podium2.value == null || _podium3.value == null ||
                _podium1.avatar == null || _podium2.avatar == null || _podium3.avatar == null)
            {
                Debug.LogError("[DesktopUI] PanelLeaderboard requires Tabs/{Daily,Weekly,Pond,Rare}, Status, MyRank, Retry, Scroll/Viewport/Content and complete Podium/Slot1..3.", this);
                return;
            }

            if (_dailyTab == null || _weeklyTab == null || _pondTab == null ||
                _rareTab == null || _scroll == null || _content == null)
                return;

            _uiReady = true;
            RestyleUi();

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
            button.onClick.AddListener(() => SelectBoard(boardType));
        }

        void RestyleUi()
        {
            DesktopModalUi.StyleTree(transform);
            RestylePodium(_podium1, 1);
            RestylePodium(_podium2, 2);
            RestylePodium(_podium3, 3);
            RestyleTabs();
            ApplyLeaderboardLayout();
            if (_retry != null)
                _retry.gameObject.SetActive(false);
        }

        void ApplyLeaderboardLayout()
        {
            var podium = transform.Find("Podium") as RectTransform;
            if (podium != null)
            {
                AnchorRect(podium, new Vector2(0f, 1f), new Vector2(0.68f, 1f),
                    new Vector2(28f, -310f), new Vector2(-16f, -88f));
                var group = podium.GetComponent<HorizontalLayoutGroup>();
                if (group != null)
                    group.enabled = false;
            }

            PlacePodiumSlot(_podium1, -168f);
            PlacePodiumSlot(_podium2, 0f);
            PlacePodiumSlot(_podium3, 168f);

            var scrollRect = _scroll != null ? _scroll.GetComponent<RectTransform>() : null;
            AnchorRect(scrollRect, Vector2.zero, new Vector2(0.68f, 1f),
                new Vector2(28f, 26f), new Vector2(-16f, -332f));

            var tabs = transform.Find("Tabs") as RectTransform;
            AnchorRect(tabs, new Vector2(0.70f, 0.48f), new Vector2(0.98f, 0.92f),
                Vector2.zero, Vector2.zero);
            if (tabs != null)
            {
                var layout = tabs.GetComponent<VerticalLayoutGroup>() ?? tabs.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(12, 12, 12, 12);
                layout.spacing = 8f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            AnchorRect(_status != null ? _status.rectTransform : null,
                new Vector2(0.70f, 0.30f), new Vector2(0.98f, 0.45f),
                Vector2.zero, Vector2.zero);
            AnchorRect(_myRank != null ? _myRank.rectTransform : null,
                new Vector2(0.70f, 0.12f), new Vector2(0.98f, 0.27f),
                Vector2.zero, Vector2.zero);
        }

        static void PlacePodiumSlot(PodiumSlot slot, float x)
        {
            if (slot.root == null)
                return;
            var rect = slot.root.GetComponent<RectTransform>();
            AnchorRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x - 75f, -194f), new Vector2(x + 75f, -4f));
            var layout = slot.root.GetComponent<LayoutElement>() ?? slot.root.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            layout.minHeight = 190f;
            layout.preferredHeight = 190f;
            layout.minWidth = 150f;
            layout.preferredWidth = 150f;
        }

        static void AnchorRect(RectTransform rect, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
                return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        void RestyleTabs()
        {
            StyleAction(_dailyTab, "fish", "今日大鱼");
            StyleAction(_weeklyTab, "trophy", "每周钓王");
            StyleAction(_pondTab, "pond", "当前鱼塘");
            StyleAction(_rareTab, "star", "稀有鱼获");
            var tabs = new[] { _dailyTab, _weeklyTab, _pondTab, _rareTab };
            var boards = new[] { BoardDaily, BoardWeekly, BoardPond, BoardRare };
            for (var index = 0; index < tabs.Length; index++)
            {
                var visual = tabs[index] != null ? tabs[index].GetComponent<CozyButtonVisual>() : null;
                if (visual != null)
                    visual.SetSelected(_boardType == boards[index]);
            }
        }

        void RestylePodium(PodiumSlot slot, int rank)
        {
            if (slot.root == null)
                return;
            var position = FindIn<Text>(slot.root.transform, "Rank") ?? FindIn<Text>(slot.root.transform, "Position");
            if (position != null)
                position.text = "第 " + rank + " 名";
            DesktopModalUi.StyleTree(slot.root.transform);
        }

        static void RestyleRow(GameObject row)
        {
            if (row != null)
                DesktopModalUi.StyleTree(row.transform);
        }

        static void StyleAction(Button button, string icon, string title)
        {
            if (button == null)
                return;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = title;
            }
            DesktopModalUi.StyleButton(button, icon);
        }

        static void StyleText(Text label, int size)
        {
            if (label == null)
                return;
            if (CatTheme.Font != null)
                label.font = CatTheme.Font;
            label.color = CatTheme.Ink;
            label.fontSize = size;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
        }

        static void Paper(Image image)
        {
            if (image == null || image.type == Image.Type.Filled)
                return;
            var color = CatTheme.Cream;
            color.a = image.color.a;
            image.color = color;
            if (image.GetComponent<Mask>() != null || image.GetComponent<RectMask2D>() != null)
                return;
            CatTheme.ApplyPaper(image);
            if (image.GetComponent<ScrollRect>() != null)
            {
                var paper = CatTheme.LoadCozy("textures/paper.png", Vector4.zero);
                if (paper != null)
                {
                    image.overrideSprite = null;
                    image.sprite = paper;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = false;
                    image.color = new Color(1f, 1f, 1f, color.a);
                }
            }
        }

        static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
        {
            if (rect == null)
                return;
            var element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = insetMin;
            rect.offsetMax = insetMax;
        }

        void SelectBoard(string boardType)
        {
            if (_boardType == boardType && _loadRoutine == null)
                return;
            _boardType = boardType;
            RestyleTabs();
            RefreshNow();
            StartPrefetchOthers(boardType);
        }

        void RefreshNow()
        {
            if (!_uiReady)
                return;
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
        }

        void StopPrefetch()
        {
            _prefetchEpoch++;
            if (_prefetchRoutine != null)
            {
                StopCoroutine(_prefetchRoutine);
                _prefetchRoutine = null;
            }
        }

        bool IsPrefetchCancelled(int epoch)
        {
            return epoch != _prefetchEpoch;
        }

        bool TryGetFreshCache(string cacheKey, out BoardCache cached)
        {
            return _clientCache.TryGetValue(cacheKey, out cached) &&
                   Time.unscaledTime - cached.fetchedAt < ClientCacheSeconds;
        }

        void StartPrefetchOthers(string exceptBoardType)
        {
            if (!isActiveAndEnabled)
                return;
            StopPrefetch();
            _prefetchRoutine = StartCoroutine(PrefetchOthersRoutine(exceptBoardType));
        }

        IEnumerator PrefetchOthersRoutine(string exceptBoardType)
        {
            var epoch = _prefetchEpoch;
            if (_api == null || !_api.CanUse)
            {
                _prefetchRoutine = null;
                yield break;
            }

            var pondId = _pond != null ? _pond.CurrentPondId : null;
            var boards = new[]
            {
                BoardDaily,
                BoardWeekly,
                BoardRare,
                BoardPond,
            };
            for (var i = 0; i < boards.Length; i++)
            {
                if (IsPrefetchCancelled(epoch))
                    yield break;
                var boardType = boards[i];
                if (boardType == exceptBoardType)
                    continue;
                if (boardType == BoardPond && string.IsNullOrEmpty(pondId))
                    continue;
                yield return PrefetchBoardSilent(boardType, pondId, epoch);
            }

            _prefetchRoutine = null;
        }

        IEnumerator PrefetchBoardSilent(string boardType, string pondId, int epoch)
        {
            var cacheKey = BuildCacheKey(boardType, pondId);
            if (TryGetFreshCache(cacheKey, out _))
                yield break;

            var done = false;
            var ok = false;
            LeaderboardEntryDto[] entries = null;
            yield return RunBoardRequestPrefetch(
                boardType, pondId, epoch,
                (success, items, error) =>
                {
                    ok = success;
                    entries = items;
                    done = true;
                });
            while (!done)
            {
                if (IsPrefetchCancelled(epoch))
                    yield break;
                yield return null;
            }

            if (IsPrefetchCancelled(epoch) || !ok || entries == null)
                yield break;

            StoreCache(cacheKey, entries, false, null);

            var myDone = false;
            var myOk = false;
            LeaderboardMyRankDto myRank = null;
            yield return RunMyRankRequestPrefetch(
                boardType, pondId, epoch,
                (success, rank) =>
                {
                    myOk = success;
                    if (success)
                        myRank = rank;
                    myDone = true;
                });
            while (!myDone)
            {
                if (IsPrefetchCancelled(epoch))
                    yield break;
                yield return null;
            }

            if (IsPrefetchCancelled(epoch))
                yield break;

            if (myOk)
                StoreCache(cacheKey, entries, true, myRank);
        }

        bool IsStaleGeneration(int generation, string boardType)
        {
            return generation != _loadGeneration || _boardType != boardType;
        }

        IEnumerator LoadRoutine(int generation)
        {
            try
            {
                yield return LoadRoutineBody(generation);
            }
            finally
            {
                if (generation == _loadGeneration)
                    _loadRoutine = null;
            }
        }

        IEnumerator LoadRoutineBody(int generation)
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前没有有效的 Steam 会话。");
                HideAllRows();
                RenderPodium(null, null, null, _boardType);
                SetMyRank(null, _boardType);
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
                yield break;
            }

            var cacheKey = BuildCacheKey(boardType, pondId);
            BoardCache cached;
            var hasCached = TryGetFreshCache(cacheKey, out cached);
            if (hasCached)
            {
                yield return ApplyBoardData(
                    cached.entries, boardType, generation, cached.hasMyRank ? cached.myRank : null);
                if (IsStaleGeneration(generation, boardType))
                    yield break;
                SetStatus(string.Empty);
                if (!cached.hasMyRank)
                    yield return FetchMyRankOnly(boardType, pondId, cacheKey, generation, cached.entries);
                yield break;
            }

            SetStatus("正在加载排行榜…");

            var boardDone = false;
            var boardOk = false;
            LeaderboardEntryDto[] entries = null;
            string boardError = null;
            yield return RunBoardRequest(
                boardType, pondId, generation,
                (success, items, error) =>
                {
                    boardOk = success;
                    entries = items;
                    boardError = error;
                    boardDone = true;
                });
            while (!boardDone)
            {
                if (IsStaleGeneration(generation, boardType))
                    yield break;
                yield return null;
            }

            if (IsStaleGeneration(generation, boardType))
                yield break;

            if (!boardOk)
            {
                SetStatus(boardError ?? "排行榜加载失败，请稍后重新打开页面。");
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                SetMyRank(null, boardType);
                yield break;
            }

            if (entries == null || entries.Length == 0)
            {
                StoreCache(cacheKey, new LeaderboardEntryDto[0], false, null);
                SetStatus("当前榜单暂无数据。");
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                SetMyRank(null, boardType);
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
                    yield break;
                }
            }

            yield return ApplyBoardData(entries, boardType, generation, null);
            if (IsStaleGeneration(generation, boardType))
                yield break;
            StoreCache(cacheKey, entries, false, null);
            SetStatus(string.Empty);
            yield return FetchMyRankOnly(boardType, pondId, cacheKey, generation, entries);
        }

        IEnumerator FetchMyRankOnly(
            string boardType,
            string pondId,
            string cacheKey,
            int generation,
            LeaderboardEntryDto[] entries)
        {
            var myDone = false;
            var myOk = false;
            LeaderboardMyRankDto myRank = null;
            yield return RunMyRankRequest(
                boardType, pondId, generation,
                (success, rank) =>
                {
                    myOk = success;
                    if (success)
                        myRank = rank;
                    myDone = true;
                });
            while (!myDone)
            {
                if (IsStaleGeneration(generation, boardType))
                    yield break;
                yield return null;
            }

            if (IsStaleGeneration(generation, boardType))
                yield break;

            if (myOk)
            {
                SetMyRank(myRank, boardType);
                StoreCache(cacheKey, entries, true, myRank);
            }
            else
            {
                SetMyRank(null, boardType);
            }
        }

        IEnumerator RunBoardRequest(
            string boardType,
            string pondId,
            int generation,
            System.Action<bool, LeaderboardEntryDto[], string> onCompleted)
        {
            var completed = false;
            var request = _api.GetLeaderboard(boardType, pondId, PageLimit,
                (success, items, periodKey, message) =>
                {
                    if (!IsStaleGeneration(generation, boardType))
                        onCompleted?.Invoke(success, items, message);
                    completed = true;
                });
            if (request != null)
                yield return request;
            if (!completed)
                onCompleted?.Invoke(false, null, "排行榜请求已取消。");
        }

        IEnumerator RunBoardRequestPrefetch(
            string boardType,
            string pondId,
            int epoch,
            System.Action<bool, LeaderboardEntryDto[], string> onCompleted)
        {
            var completed = false;
            var request = _api.GetLeaderboard(boardType, pondId, PageLimit,
                (success, items, periodKey, message) =>
                {
                    if (!IsPrefetchCancelled(epoch))
                        onCompleted?.Invoke(success, items, message);
                    completed = true;
                });
            if (request != null)
                yield return request;
            if (!completed)
                onCompleted?.Invoke(false, null, "排行榜请求已取消。");
        }

        IEnumerator RunMyRankRequest(
            string boardType,
            string pondId,
            int generation,
            System.Action<bool, LeaderboardMyRankDto> onCompleted)
        {
            var completed = false;
            var request = _api.GetMyLeaderboardRank(boardType, pondId,
                (success, rank, message) =>
                {
                    if (!IsStaleGeneration(generation, boardType))
                        onCompleted?.Invoke(success, rank);
                    completed = true;
                });
            if (request != null)
                yield return request;
            if (!completed)
                onCompleted?.Invoke(false, null);
        }

        IEnumerator RunMyRankRequestPrefetch(
            string boardType,
            string pondId,
            int epoch,
            System.Action<bool, LeaderboardMyRankDto> onCompleted)
        {
            var completed = false;
            var request = _api.GetMyLeaderboardRank(boardType, pondId,
                (success, rank, message) =>
                {
                    if (!IsPrefetchCancelled(epoch))
                        onCompleted?.Invoke(success, rank);
                    completed = true;
                });
            if (request != null)
                yield return request;
            if (!completed)
                onCompleted?.Invoke(false, null);
        }

        static string BuildCacheKey(string boardType, string pondId)
        {
            return boardType + "|" + (pondId ?? string.Empty);
        }

        void StoreCache(
            string cacheKey,
            LeaderboardEntryDto[] entries,
            bool hasMyRank,
            LeaderboardMyRankDto myRank)
        {
            _clientCache[cacheKey] = new BoardCache
            {
                entries = entries,
                myRank = myRank,
                hasMyRank = hasMyRank,
                fetchedAt = Time.unscaledTime,
            };
        }

        IEnumerator ApplyBoardData(
            LeaderboardEntryDto[] entries,
            string boardType,
            int generation,
            LeaderboardMyRankDto myRank)
        {
            if (entries == null || entries.Length == 0)
            {
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                if (myRank != null)
                    SetMyRank(myRank, boardType);
                yield break;
            }

            var first = FindRank(entries, 1);
            var second = FindRank(entries, 2);
            var third = FindRank(entries, 3);
            RenderPodium(first, second, third, boardType);
            yield return BindRows(entries, boardType, generation);
            if (IsStaleGeneration(generation, boardType))
                yield break;
            if (myRank != null)
                SetMyRank(myRank, boardType);
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
                if (IsStaleGeneration(generation, boardType))
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
                    if (row == null)
                        yield break;
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
            {
                RestyleRow(row);
                return row;
            }

            Debug.LogError("[DesktopUI] Required data prefab is missing: Desktop/Prefabs/LeaderboardRow", this);
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
                    slot.avatar.color = slot.avatar.sprite != null ? Color.white : CatTheme.Cream;
                return;
            }

            slot.nickname.text = entry.nickname ?? "钓友";
            slot.value.text = FormatValue(entry, boardType);
            if (slot.avatar != null)
                slot.avatar.color = slot.avatar.sprite != null ? Color.white : CatTheme.Cream;
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
            text.font = CatTheme.Font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 16;
            text.color = CatTheme.Ink;
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
            Debug.LogError("[DesktopUI] Prefab is missing required controls. Runtime will not build a second layout.");
            return;
        }

        PodiumSlot MakePodiumSlot(Transform parent, string name, string title)
        {
            var root = NewGo(name, parent, typeof(Image), typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            CatTheme.ApplyPaper(root.GetComponent<Image>());
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            root.GetComponent<LayoutElement>().minHeight = 140f;
            var avatar = NewGo("Avatar", root.transform, typeof(Image), typeof(LayoutElement));
            avatar.GetComponent<Image>().sprite = CatTheme.LoadCozy("icons/user.png", Vector4.zero);
            avatar.GetComponent<Image>().color = Color.white;
            avatar.GetComponent<Image>().preserveAspect = true;
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
            go.GetComponent<Image>().color = CatTheme.Cream;
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = 40f;
            element.preferredHeight = 40f;
            var text = CreateText(go.transform, "Label", label, width);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform);
            var button = go.GetComponent<Button>();
            DesktopModalUi.StyleButton(button);
            return button;
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
