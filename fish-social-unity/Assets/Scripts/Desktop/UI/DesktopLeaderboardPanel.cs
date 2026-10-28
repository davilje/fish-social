using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Server-authoritative leaderboard page. The authored prefab owns chrome
    /// and geometry; opening only binds data and selected states.
    /// Opening or switching boards never calls leave_pond.
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
        System.Action<string> _openPlayer;
        string _selectedPlayerId;
        Coroutine _playerRoutine;
        int _playerGeneration;
        CozyJournalPageMotion _pageMotion;
        Coroutine _boardTransition;
        string _pendingBoard;
        readonly Dictionary<GameObject, string> _cardPlayers = new Dictionary<GameObject, string>();
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
        List<GameObject> _rowPool = new List<GameObject>(48);
        readonly Dictionary<string, RectTransform> _boardRoots = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, List<GameObject>> _boardPools = new Dictionary<string, List<GameObject>>();
        readonly HashSet<string> _builtBoards = new HashSet<string>();
        bool _leaderTabsStyled;
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

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond, System.Action<string> openPlayer = null)
        {
            _api = api;
            _pond = pond;
            _openPlayer = openPlayer;
            EnsureUi();
        }

        public void OnOpened()
        {
            if (!_uiReady)
                EnsureUi();
            if (!_uiReady)
                return;
            ShowBoard(_boardType);
            if (_builtBoards.Contains(_boardType))
                return;
            RefreshNow();
            StartPrefetchOthers(_boardType);
        }

        public void OnClosed()
        {
            if (_boardTransition != null) StopCoroutine(_boardTransition);
            _boardTransition = null;
            _pendingBoard = null;
            _pageMotion?.Finish();
            CancelLoads();
            ClearSelectedPlayer();
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
                _scroll == null || _content == null ||
                _podium1.root == null || _podium2.root == null || _podium3.root == null ||
                _podium1.nickname == null || _podium2.nickname == null || _podium3.nickname == null ||
                _podium1.value == null || _podium2.value == null || _podium3.value == null ||
                _podium1.avatar == null || _podium2.avatar == null || _podium3.avatar == null)
            {
                Debug.LogError("[DesktopUI] PanelLeaderboard requires Tabs/{Daily,Weekly,Pond,Rare}, Status, MyRank, Scroll/Viewport/Content and complete Podium/Slot1..3.", this);
                return;
            }

            if (_dailyTab == null || _weeklyTab == null || _pondTab == null ||
                _rareTab == null || _scroll == null || _content == null)
                return;

            _uiReady = true;
            _pageMotion = GetComponent<CozyJournalPageMotion>();
            EnsureRankScrollbar();
            AlignJournalPodium();
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
            if (!CozyItemArt.IsAuthored(transform))
            {
                RestylePodium(_podium1, 1);
                RestylePodium(_podium2, 2);
                RestylePodium(_podium3, 3);
                ApplyLeaderboardLayout();
            }
            RestyleTabs();
            if (_retry != null)
                _retry.gameObject.SetActive(false);
        }

        void ApplyCompactCozyLeaderboard()
        {
            var podium = transform.Find("Podium") as RectTransform;
            if (podium != null)
            {
                var group = podium.GetComponent<HorizontalLayoutGroup>();
                if (group != null)
                    group.enabled = false;
            }

            PlaceTopLeft(transform.Find("Reflow_PodiumPaper") as RectTransform, 24f, 112f, 980f, 168f);
            PlaceTopLeft(podium, 24f, 112f, 980f, 168f);
            PlaceCompactSlot(2, 70f, 12f, 152f);
            PlaceCompactSlot(1, 358f, 0f, 164f);
            PlaceCompactSlot(3, 646f, 12f, 152f);
            PlaceTopLeft(transform.Find("DesignTableHeading") as RectTransform, 24f, 288f, 980f, 34f);
            PlaceTopLeft(transform.Find("DesignRankColumn") as RectTransform, 44f, 288f, 100f, 34f);
            PlaceTopLeft(transform.Find("DesignNameColumn") as RectTransform, 164f, 288f, 410f, 34f);
            PlaceTopLeft(transform.Find("DesignValueColumn") as RectTransform, 650f, 288f, 330f, 34f);
            PlaceTopLeft(transform.Find("Scroll") as RectTransform, 24f, 328f, 980f, 244f);
        }

        void PlaceCompactSlot(int rank, float x, float y, float height)
        {
            var slot = transform.Find("Podium/Slot" + rank) as RectTransform;
            if (slot == null)
                return;
            var element = slot.GetComponent<LayoutElement>() ?? slot.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            PlaceTopLeft(slot, x, y, 264f, height);
            PlaceTopLeft(slot.Find("DesignBacking") as RectTransform, 0f, 0f, 264f, height);
            PlaceCompactLabel(slot.Find("Rank") as RectTransform, 16f, 2f, 232f, 26f, 22, TextAnchor.MiddleCenter);
            PlaceTopLeft(slot.Find("Avatar") as RectTransform, 96f, 28f, 72f, 72f);
            PlaceCompactLabel(slot.Find("Nickname") as RectTransform, 12f, 102f, 240f, 22f, 16, TextAnchor.MiddleCenter);
            PlaceCompactLabel(slot.Find("Value") as RectTransform, 12f, 124f, 240f, 22f, 14, TextAnchor.MiddleCenter);
        }

        static void PlaceCompactLabel(
            RectTransform rect, float x, float y, float width, float height, int fontSize, TextAnchor alignment)
        {
            PlaceTopLeft(rect, x, y, width, height);
            var label = rect != null ? rect.GetComponent<Text>() : null;
            if (label == null)
                return;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        void ApplyLeaderboardLayout()
        {
            var podium = transform.Find("Podium") as RectTransform;
            if (podium != null)
            {
                PlaceStaticLayout("Podium", 24f, 112f, 1270f, 192f);
                var group = podium.GetComponent<HorizontalLayoutGroup>();
                if (group != null)
                    group.enabled = false;
            }

            DesktopModalUi.PlacePageSealLeft(transform);
            PlaceStaticLayout("Reflow_InfoRail", 1334f, 256f, 246f, 336f);
            PlaceStaticLayout("Reflow_RailSeal", 1358f, 280f, 72f, 72f);
            PlaceStaticLayout("Reflow_RailHeading", 1358f, 372f, 198f, 36f);
            PlaceStaticLayout("Reflow_RailBody", 1358f, 428f, 198f, 116f);
            PlaceStaticLayout("Reflow_PodiumPaper", 24f, 112f, 1270f, 192f);
            PlaceStaticLayout("Reflow_RankTableHeading", 24f, 324f, 1270f, 44f);
            PlaceStaticLayout("Reflow_RankColumn", 68f, 333f, 124f, 28f);
            PlaceStaticLayout("Reflow_NameColumn", 218f, 333f, 748f, 28f);
            PlaceStaticLayout("Reflow_ValueColumn", 990f, 333f, 274f, 28f);
            PlaceStaticLayout("Reflow_Scroll", 24f, 380f, 1270f, 166f);

            PlacePodiumSlot(_podium1, -144f);
            PlacePodiumSlot(_podium2, 0f);
            PlacePodiumSlot(_podium3, 144f);

            PlaceStaticLayout("Scroll", 24f, 380f, 1270f, 166f);

            var tabs = transform.Find("Tabs") as RectTransform;
            PlaceStaticLayout("Tabs", 1334f, 270f, 246f, 170f);
            if (tabs != null)
            {
                var layout = tabs.GetComponent<VerticalLayoutGroup>();
                if (layout == null)
                    layout = tabs.gameObject.AddComponent<VerticalLayoutGroup>();

                if (layout != null)
                {
                    layout.padding = new RectOffset(8, 8, 8, 8);
                    layout.spacing = 6f;
                    layout.childControlWidth = true;
                    layout.childControlHeight = false;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                }
            }

            foreach (var tab in new[] { _dailyTab, _weeklyTab, _pondTab, _rareTab })
            {
                if (tab == null)
                    continue;
                var element = tab.GetComponent<LayoutElement>();
                if (element == null)
                    element = tab.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 34f;
                element.preferredHeight = 34f;
                element.flexibleHeight = 0f;
            }

            PlaceStaticLayout("MyRank", 24f, 558f, 1270f, 32f);
            PlaceStaticLayout("Status", 24f, 600f, 1270f, 26f);
        }

        static void PlacePodiumSlot(PodiumSlot slot, float x)
        {
            if (slot.root == null)
                return;
            var rect = slot.root.GetComponent<RectTransform>();
            AnchorRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x - 64f, -196f), new Vector2(x + 64f, -4f));
            var layout = slot.root.GetComponent<LayoutElement>() ?? slot.root.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            layout.minHeight = 192f;
            layout.preferredHeight = 192f;
            layout.minWidth = 128f;
            layout.preferredWidth = 128f;
        }

        void PlaceStaticLayout(string name, float x, float y, float width, float height)
        {
            var rect = transform.Find(name) as RectTransform;
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
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
            CozyItemArt.Text(transform, "JournalBoardHeading", _boardType == BoardWeekly ? DesktopLocalization.Text("ui.leaderboard.panel.title.weekly", "每周钓王") : _boardType == BoardPond ? DesktopLocalization.Text("ui.leaderboard.panel.title.pond", "当前鱼塘") : _boardType == BoardRare ? DesktopLocalization.Text("ui.leaderboard.panel.title.rare", "稀有鱼获") : DesktopLocalization.Text("ui.leaderboard.panel.title.daily", "今日大鱼"));
            if (!_leaderTabsStyled)
            {
                StyleAction(_dailyTab, "fish", DesktopLocalization.Text("ui.leaderboard.panel.title.daily", "今日大鱼"));
                StyleAction(_weeklyTab, "trophy", DesktopLocalization.Text("ui.leaderboard.panel.title.weekly", "每周钓王"));
                StyleAction(_pondTab, "pond", DesktopLocalization.Text("ui.leaderboard.panel.title.pond", "当前鱼塘"));
                StyleAction(_rareTab, "star", DesktopLocalization.Text("ui.leaderboard.panel.title.rare", "稀有鱼获"));
                _leaderTabsStyled = true;
            }
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
            {
                position.text = DesktopLocalization.Format("ui.leaderboard.panel.label.podium_rank", (object)(rank));
                var place = position.rectTransform;
                place.anchorMin = place.anchorMax = new Vector2(0f, 1f);
                place.pivot = new Vector2(0f, 1f);
                place.anchoredPosition = new Vector2(10f, -6f);
                place.sizeDelta = new Vector2(120f, 28f);
                position.fontSize = 18;
                position.resizeTextForBestFit = false;
                position.alignment = TextAnchor.MiddleLeft;
                position.color = new Color(198f / 255f, 160f / 255f, 94f / 255f);
                position.horizontalOverflow = HorizontalWrapMode.Overflow;
                position.transform.SetAsLastSibling();
            }
            PlacePodiumImage(slot.avatar != null ? slot.avatar.rectTransform : null,
                new Vector2(56f, 56f), 0f, 38f);
            PlacePodiumText(slot.nickname != null ? slot.nickname.rectTransform : null,
                0f, 74f, 112f, 24f, TextAnchor.MiddleCenter);
            PlacePodiumText(slot.value != null ? slot.value.rectTransform : null,
                0f, 102f, 112f, 24f, TextAnchor.MiddleCenter);
            if (slot.value != null)
            {
                slot.value.fontSize = 22;
                slot.value.resizeTextForBestFit = false;
            }
            DesktopModalUi.StyleTree(slot.root.transform);
            if (position != null)
            {
                position.fontSize = 18;
                position.resizeTextForBestFit = false;
                position.color = new Color(198f / 255f, 160f / 255f, 94f / 255f);
                position.transform.SetAsLastSibling();
            }
            TuckPodiumPill(slot.root.transform.Find("JournalRankBacking") as RectTransform);
            TuckPodiumPill(slot.root.transform.Find("JournalRankLabel") as RectTransform);
        }

        void AlignJournalPodium()
        {
            var podium = _podium1.root != null ? _podium1.root.transform.parent as RectTransform : null;
            if (podium == null)
                podium = transform.Find("Scroll/Viewport/Content/Podium") as RectTransform
                    ?? transform.Find("Podium") as RectTransform;
            if (podium == null)
                return;
            // The scroll content already reserves 232px for this box. Flowing the podium
            // as a layout child drops the three cards to the bottom of the page.
            var element = podium.GetComponent<LayoutElement>() ?? podium.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            podium.anchorMin = podium.anchorMax = new Vector2(0f, 1f);
            podium.pivot = new Vector2(0f, 1f);
            podium.anchoredPosition = new Vector2(15f, -4f);
            podium.sizeDelta = new Vector2(680f, 220f);
            var rows = podium.parent != null ? podium.parent.GetComponent<VerticalLayoutGroup>() : null;
            if (rows != null && rows.padding.top < 240)
            {
                var pad = rows.padding;
                rows.padding = new RectOffset(pad.left, pad.right, 240, pad.bottom);
            }
            for (var rank = 1; rank <= 3; rank++)
            {
                var card = podium.Find("Slot" + rank);
                if (card == null) continue;
                HangPodiumLabel(card.Find("JournalRankBacking") as RectTransform, rank);
                HangPodiumLabel(card.Find("JournalRankLabel") as RectTransform, rank);
                var place = card.Find("Rank");
                if (place != null)
                {
                    var placeLabel = place.GetComponent<Text>();
                    if (placeLabel != null)
                    {
                        placeLabel.fontSize = 27;
                        placeLabel.resizeTextForBestFit = false;
                    }
                }
            }
            // Slot1 is authored first, so the side cards paint over its tape and shadow.
            var first = podium.Find("Slot1");
            if (first != null) first.SetAsLastSibling();
            // Rank rows are later siblings of this box, so they cover the 10px label that hangs
            // out of the first-place card. The podium itself has no graphic, so it does not block the list.
            podium.SetAsLastSibling();
        }

        static void HangPodiumLabel(RectTransform rect, int rank)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            // Preview: bottom -10px, padding 4px 15px, font 13, so the pill is 21px tall and hangs under the card.
            rect.sizeDelta = new Vector2(70f, 21f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.SetAsLastSibling();
            var plate = rect.GetComponent<Image>();
            if (plate != null)
            {
                plate.sprite = null;
                plate.type = Image.Type.Simple;
                plate.color = Color.clear;
                plate.raycastTarget = false;
                var washNode = rect.Find("RankPill");
                if (washNode == null)
                {
                    var go = new GameObject("RankPill", typeof(RectTransform), typeof(CozyCssGraphic));
                    go.transform.SetParent(rect, false);
                    washNode = go.transform;
                }
                var washRect = (RectTransform)washNode;
                washRect.anchorMin = Vector2.zero;
                washRect.anchorMax = Vector2.one;
                washRect.offsetMin = washRect.offsetMax = Vector2.zero;
                washNode.SetAsFirstSibling();
                var wash = washNode.GetComponent<CozyCssGraphic>();
                if (wash != null)
                {
                    wash.shape = CozyCssGraphic.Shape.Paper;
                    wash.radius = 8f;
                    wash.border = 0f;
                    wash.raycastTarget = false;
                    wash.color = rank == 1
                        ? new Color(248f / 255f, 223f / 255f, 153f / 255f)
                        : rank == 2
                            ? new Color(220f / 255f, 234f / 255f, 241f / 255f)
                            : new Color(238f / 255f, 209f / 255f, 185f / 255f);
                }
            }
            var label = rect.GetComponent<Text>();
            if (label == null) return;
            label.text = rank == 1 ? DesktopLocalization.Text("ui.leaderboard.panel.label.first_place", "第一名") : rank == 2 ? DesktopLocalization.Text("ui.leaderboard.panel.label.second_place", "第二名") : DesktopLocalization.Text("ui.leaderboard.panel.label.third_place", "第三名");
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = rank == 1
                ? new Color(139f / 255f, 97f / 255f, 43f / 255f)
                : rank == 2
                    ? new Color(109f / 255f, 140f / 255f, 153f / 255f)
                    : new Color(174f / 255f, 119f / 255f, 82f / 255f);
        }

        static void TuckPodiumPill(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            rect.SetAsLastSibling();
        }

        static void PlacePodiumImage(RectTransform rect, Vector2 size, float x, float y)
        {
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = size;
        }

        static void PlacePodiumText(RectTransform rect, float x, float y,
            float width, float height, TextAnchor alignment)
        {
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            var label = rect.GetComponent<Text>();
            if (label != null)
            {
                label.alignment = alignment;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
            }
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
            if (_boardType == boardType && _boardTransition == null) return;
            if (_pendingBoard == boardType) return;
            CancelLoads();
            if (_boardTransition != null) StopCoroutine(_boardTransition);
            _boardTransition = null;
            _pendingBoard = boardType;
            if (!Application.isPlaying)
            {
                ApplyBoardSwitch(boardType);
                _pendingBoard = null;
                return;
            }
            _boardTransition = StartCoroutine(SwitchBoardRoutine(boardType));
        }

        IEnumerator SwitchBoardRoutine(string boardType)
        {
            if (_pageMotion != null) yield return _pageMotion.Exit();
            if (_pendingBoard != boardType)
                yield break;
            ApplyBoardSwitch(boardType);
            _pendingBoard = null;
            _boardTransition = null;
        }

        void ApplyBoardSwitch(string boardType)
        {
            _boardType = boardType;
            RestyleTabs();
            ShowBoard(boardType);
            var pondId = _pond != null ? _pond.CurrentPondId : null;
            BoardCache cached;
            if (_builtBoards.Contains(boardType) && TryGetFreshCache(BuildCacheKey(boardType, pondId), out cached))
            {
                RenderPodium(FindRank(cached.entries, 1), FindRank(cached.entries, 2), FindRank(cached.entries, 3), boardType);
                if (cached.hasMyRank)
                    SetMyRank(cached.myRank, boardType);
                SetStatus(cached.entries != null && cached.entries.Length > 0 ? string.Empty : DesktopLocalization.Text("ui.leaderboard.panel.empty.board", "当前榜单暂无数据。"));
                ReplayBoardMotion();
                return;
            }
            RenderPodium(null, null, null, boardType);
            _pageMotion?.Finish();
            RefreshNow();
            StartPrefetchOthers(boardType);
        }

        void ReplayBoardMotion()
        {
            if (_content != null)
            {
                var stickers = _content.GetComponentsInChildren<CozyStickerMotion>();
                for (var i = 0; i < stickers.Length; i++)
                    stickers[i].RestartEntrance();
            }
            _pageMotion?.PlayEntrance((_content != null ? _content.childCount : 0) + 3);
        }

        void ShowBoard(string boardType)
        {
            EnsureBoard(boardType);
            RectTransform root;
            List<GameObject> pool;
            if (!_boardRoots.TryGetValue(boardType, out root) || root == null || !_boardPools.TryGetValue(boardType, out pool))
                return;
            _content = root;
            _rowPool = pool;
            if (_scroll != null)
                _scroll.content = root;
            var podium = _podium1.root != null ? _podium1.root.transform.parent as RectTransform : null;
            if (podium != null && podium.parent != root)
            {
                podium.SetParent(root, false);
                if (!podium.gameObject.activeSelf)
                    podium.gameObject.SetActive(true);
            }
            if (!root.gameObject.activeSelf)
                root.gameObject.SetActive(true);
            foreach (var pair in _boardRoots)
            {
                if (pair.Key == boardType || pair.Value == null)
                    continue;
                if (pair.Value.gameObject.activeSelf)
                    pair.Value.gameObject.SetActive(false);
            }
            AlignJournalPodium();
        }

        void EnsureBoard(string boardType)
        {
            if (_boardRoots.ContainsKey(boardType) || _content == null)
                return;
            if (_boardRoots.Count == 0)
            {
                _boardRoots[boardType] = _content;
                _boardPools[boardType] = _rowPool;
                return;
            }
            var root = CozyKeptPage.DuplicateList(_content, "Board_" + boardType);
            _boardRoots[boardType] = root;
            _boardPools[boardType] = new List<GameObject>(48);
        }

        void MarkBoardBuilt(string boardType)
        {
            if (!string.IsNullOrEmpty(boardType))
                _builtBoards.Add(boardType);
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
                SetStatus(DesktopLocalization.Text("ui.leaderboard.panel.error.session_required", "当前没有有效的 Steam 会话。"));
                HideAllRows();
                RenderPodium(null, null, null, _boardType);
                SetMyRank(null, _boardType);
                yield break;
            }

            var boardType = _boardType;
            var pondId = _pond != null ? _pond.CurrentPondId : null;
            if (boardType == BoardPond && string.IsNullOrEmpty(pondId))
            {
                SetStatus(DesktopLocalization.Text("ui.leaderboard.panel.error.pond_required", "请先进入鱼塘后再查看鱼塘榜。"));
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

            SetStatus(DesktopLocalization.Text("ui.leaderboard.panel.status.loading", "正在加载排行榜…"));

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
                SetStatus(boardError ?? DesktopLocalization.Text("ui.leaderboard.panel.error.load_failed", "排行榜加载失败，请稍后重新打开页面。"));
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                SetMyRank(null, boardType);
                yield break;
            }

            if (entries == null || entries.Length == 0)
            {
                StoreCache(cacheKey, new LeaderboardEntryDto[0], false, null);
                SetStatus(DesktopLocalization.Text("ui.leaderboard.panel.empty.board", "当前榜单暂无数据。"));
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                SetMyRank(null, boardType);
                MarkBoardBuilt(boardType);
                yield break;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null ||
                    string.IsNullOrEmpty(entries[i].playerId) ||
                    entries[i].rank <= 0)
                {
                    SetStatus(DesktopLocalization.Text("ui.leaderboard.panel.error.missing_response_data", "服务端响应缺少排行榜字段，请重试。"));
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
                onCompleted?.Invoke(false, null, DesktopLocalization.Text("ui.leaderboard.panel.status.request_cancelled", "排行榜请求已取消。"));
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
                onCompleted?.Invoke(false, null, DesktopLocalization.Text("ui.leaderboard.panel.status.request_cancelled", "排行榜请求已取消。"));
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
                if (IsStaleGeneration(generation, boardType))
                    yield break;
                HideAllRows();
                RenderPodium(null, null, null, boardType);
                if (myRank != null)
                    SetMyRank(myRank, boardType);
                MarkBoardBuilt(boardType);
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
            SyncPlayerSelection();
            MarkBoardBuilt(boardType);
            _pageMotion?.ContentReady(entries.Length + 3);
        }

        IEnumerator BindRows(
            LeaderboardEntryDto[] entries, string boardType, int generation)
        {
            var needed = entries.Length;

            var bound = 0;
            var sourceIndex = 0;
            while (bound < needed)
            {
                if (IsStaleGeneration(generation, boardType))
                    yield break;

                var batchEnd = Mathf.Min(bound + RowsPerFrame, needed);
                while (bound < batchEnd)
                {
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
            AlignJournalPodium();
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
            _cardPlayers[row] = entry.playerId;
            SetChildText(row, "Rank", row.transform.Find("JournalDetailParity") != null ? entry.rank.ToString("00") : "#" + entry.rank);
            SetChildText(row, "Nickname", entry.nickname ?? DesktopLocalization.Text("ui.leaderboard.panel.default_nickname", "钓友"));
            SetChildText(row, "Value", FormatValue(entry, boardType));
            var valueLabel = row.transform.Find("Value")?.GetComponent<Text>();
            if (valueLabel != null)
            {
                valueLabel.fontSize = row.transform.Find("JournalDetailParity") != null ? 23 : 22;
                valueLabel.resizeTextForBestFit = false;
                valueLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                valueLabel.verticalOverflow = VerticalWrapMode.Truncate;
            }
            if (row.transform.Find("JournalDetailParity") != null)
            {
                var rankLabel = row.transform.Find("Rank")?.GetComponent<Text>();
                if (rankLabel != null)
                {
                    rankLabel.fontSize = 25;
                    rankLabel.resizeTextForBestFit = false;
                    rankLabel.color = new Color(197f / 255f, 168f / 255f, 136f / 255f);
                }
                var nickname = row.transform.Find("Nickname")?.GetComponent<Text>();
                if (nickname != null)
                {
                    nickname.fontSize = 19;
                    nickname.resizeTextForBestFit = false;
                }
            }
            var button = row.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectPlayer(entry, boardType));
                button.interactable = !string.IsNullOrEmpty(entry.playerId);
                button.GetComponent<CozyButtonVisual>()?.SetSelected(entry.playerId == _selectedPlayerId);
            }
            var sticker = CozyStickerMotion.Bind(row, "rank:" + boardType + ":" + entry.playerId, row.transform.GetSiblingIndex() + 2,
                _scroll != null ? _scroll.viewport : null, entry.playerId, 0);
            if (!Application.isPlaying) sticker.ApplyFinalPose();
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
            for (var i = 0; entries != null && i < entries.Length; i++)
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
            BindPodium(_podium1, first, DesktopLocalization.Text("ui.leaderboard.panel.empty.podium", "虚位以待"), boardType);
            BindPodium(_podium2, second, DesktopLocalization.Text("ui.leaderboard.panel.empty.podium", "虚位以待"), boardType);
            BindPodium(_podium3, third, DesktopLocalization.Text("ui.leaderboard.panel.empty.podium", "虚位以待"), boardType);
            if (first == null && second == null && third == null) ClearSelectedPlayer();
            else if (string.IsNullOrEmpty(_selectedPlayerId)) SelectPlayer(first ?? second ?? third, boardType);
        }

        void BindPodium(
            PodiumSlot slot, LeaderboardEntryDto entry, string emptyLabel, string boardType)
        {
            if (slot.nickname == null || slot.value == null)
                return;
            var button = slot.root != null ? slot.root.GetComponent<Button>() : null;
            if (slot.root != null) _cardPlayers[slot.root] = entry != null ? entry.playerId : null;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = entry != null && !string.IsNullOrEmpty(entry.playerId);
                if (entry != null) button.onClick.AddListener(() => SelectPlayer(entry, boardType));
            }
            if (entry == null)
            {
                slot.nickname.text = emptyLabel;
                slot.value.text = "—";
                if (slot.avatar != null)
                {
                    slot.avatar.sprite = null;
                    slot.avatar.color = Color.clear;
                }
                return;
            }

            var staticPose = slot.root.GetComponent<CozyJournalStaticPose>();
            if (staticPose != null) staticPose.enabled = false;
            var order = entry.rank == 2 ? 0 : entry.rank == 1 ? 1 : 2;
            var sticker = CozyStickerMotion.Bind(slot.root, "podium:" + boardType + ":" + entry.playerId, order,
                _scroll != null ? _scroll.viewport : null, entry.playerId);
            if (!Application.isPlaying) sticker.ApplyFinalPose();
            slot.nickname.text = entry.nickname ?? DesktopLocalization.Text("ui.leaderboard.panel.default_nickname", "钓友");
            slot.value.text = FormatValue(entry, boardType);
            if (slot.avatar != null)
            {
                slot.avatar.sprite = DesktopDefaultAvatars.Portrait(entry.avatarUrl, entry.playerId);
                slot.avatar.preserveAspect = true;
                slot.avatar.color = Color.white;
            }
            if (slot.root != null && slot.root.name == "Slot1")
                slot.root.transform.SetAsLastSibling();
        }

        void ClearSelectedPlayer()
        {
            _playerGeneration++;
            if (_playerRoutine != null) StopCoroutine(_playerRoutine);
            _playerRoutine = null;
            _selectedPlayerId = null;
            CozyItemArt.Text(transform, "DesignPlayerCard/Name", DesktopLocalization.Text("ui.leaderboard.panel.title.select_angler", "选择一位钓友"));
            CozyItemArt.Text(transform, "DesignPlayerCard/Value", "");
            CozyItemArt.Text(transform, "DesignPlayerCard/Bio", DesktopLocalization.Text("ui.leaderboard.panel.hint.select_profile", "点击榜单查看钓友资料。"));
            CozyItemArt.Set(transform, "DesignPlayerCard/Avatar", null);
            var open = Find<Button>("DesignPlayerCard/OpenProfile");
            if (open != null) open.interactable = false;
        }

        public void SelectPlayer(LeaderboardEntryDto entry, string boardType = null)
        {
            if (entry == null || string.IsNullOrEmpty(entry.playerId)) return;
            boardType = boardType ?? _boardType;
            _playerGeneration++;
            if (_playerRoutine != null) StopCoroutine(_playerRoutine);
            _selectedPlayerId = entry.playerId;
            SyncPlayerSelection();
            foreach(var pair in _cardPlayers)
                if(pair.Key!=null && pair.Value==entry.playerId) pair.Key.GetComponent<CozyStickerMotion>()?.ReplayTape();
            transform.Find("DesignPlayerCard/JournalPhoto")?.GetComponent<CozyJournalStaticPose>()?.PlayReveal(Vector2.zero, CozyMotion.Snap, .3f);
            CozyItemArt.Text(transform, "DesignPlayerCard/Name", entry.nickname ?? DesktopLocalization.Text("ui.leaderboard.panel.default_nickname", "钓友"));
            CozyItemArt.Text(transform, "DesignPlayerCard/Value", DesktopLocalization.Format("ui.leaderboard.panel.label.selected_rank", (object)(entry.rank), (object)(FormatValue(entry, boardType))));
            CozyItemArt.Text(transform, "DesignPlayerCard/Bio", DesktopLocalization.Text("ui.leaderboard.panel.status.loading_profile", "正在加载资料…"));
            CozyItemArt.Set(transform, "DesignPlayerCard/Avatar", DesktopDefaultAvatars.Portrait(entry.avatarUrl, entry.playerId));
            var open = Find<Button>("DesignPlayerCard/OpenProfile");
            if (open != null)
            {
                open.onClick.RemoveAllListeners();
                open.onClick.AddListener(() => _openPlayer?.Invoke(entry.playerId));
                open.interactable = _openPlayer != null;
                DesktopModalUi.SetButtonLabel(open, DesktopLocalization.Text("ui.leaderboard.panel.button.view_profile", "查看钓友资料"));
            }
            if (_api != null && _api.CanUse)
                _playerRoutine = StartCoroutine(LoadPlayer(entry.playerId, _playerGeneration));
            else CozyItemArt.Text(transform, "DesignPlayerCard/Bio", DesktopLocalization.Text("ui.leaderboard.panel.hint.login_required", "登录后可查看钓友资料。"));
        }

        /// <summary>Bind a complete real-shaped snapshot through the same card handlers as network loads; useful for deterministic Editor rendering.</summary>
        public void PresentRows(LeaderboardEntryDto[] entries, string boardType = "daily_biggest")
        {
            EnsureUi();
            if (!_uiReady) return;
            _boardType = boardType;
            RestyleTabs();
            ClearSelectedPlayer();
            entries = entries ?? new LeaderboardEntryDto[0];
            RenderPodium(FindRank(entries, 1), FindRank(entries, 2), FindRank(entries, 3), boardType);
            for (var i = 0; i < entries.Length; i++)
            {
                EnsureRowSlot(i);
                _rowPool[i].SetActive(true);
                BindRow(_rowPool[i], entries[i], boardType);
            }
            for (var i = entries.Length; i < _rowPool.Count; i++) _rowPool[i]?.SetActive(false);
            SyncPlayerSelection();
            foreach (var motion in GetComponentsInChildren<CozyStickerMotion>(true)) motion.ApplyFinalPose();
            transform.Find("DesignPlayerCard/JournalPhoto")?.GetComponent<CozyJournalStaticPose>()?.ApplyFinalPose();
            _pageMotion?.Finish();
        }

        /// <summary>Advance a newly triggered selection by the requested seconds. Use a fresh snapshot/selection for each absolute capture.</summary>
        public void SampleMotion(float seconds)
        {
            foreach (var motion in GetComponentsInChildren<CozyStickerMotion>(true)) motion.Advance(Mathf.Max(float.Epsilon, seconds));
            transform.Find("DesignPlayerCard/JournalPhoto")?.GetComponent<CozyJournalStaticPose>()?.SampleReveal(seconds);
        }

        void SyncPlayerSelection()
        {
            foreach (var pair in _cardPlayers)
                if (pair.Key != null)
                    pair.Key.GetComponent<CozyButtonVisual>()?.SetSelected(!string.IsNullOrEmpty(_selectedPlayerId) && pair.Value == _selectedPlayerId);
        }

        IEnumerator LoadPlayer(string playerId, int generation)
        {
            yield return _api.GetPublicPlayerView(playerId, 1, (ok, view, message) =>
            {
                if (this == null || generation != _playerGeneration || playerId != _selectedPlayerId) return;
                if (!ok || view == null || view.profile == null)
                {
                    CozyItemArt.Text(transform, "DesignPlayerCard/Bio", message ?? DesktopLocalization.Text("ui.leaderboard.panel.error.profile_unavailable", "资料暂不可用。"));
                    return;
                }
                CozyItemArt.Text(transform, "DesignPlayerCard/Name", view.profile.nickname ?? DesktopLocalization.Text("ui.leaderboard.panel.default_nickname", "钓友"));
                CozyItemArt.Text(transform, "DesignPlayerCard/Bio", string.IsNullOrEmpty(view.profile.bio) ? DesktopLocalization.Text("ui.leaderboard.panel.empty.biography", "这位钓友还没有填写简介。") : view.profile.bio);
                CozyItemArt.Set(transform, "DesignPlayerCard/Avatar", DesktopDefaultAvatars.Portrait(view.profile.avatarUrl, playerId));
            });
            if (generation == _playerGeneration) _playerRoutine = null;
        }

        static string FormatValue(LeaderboardEntryDto entry, string boardType)
        {
            if (entry == null)
                return "—";
            var extra = entry.extra;
            if (boardType == BoardDaily)
            {
                var size = extra != null && extra.sizeM > 0 ? extra.sizeM : entry.value;
                return size.ToString("0.00") + " m";
            }
            if (boardType == BoardWeekly)
            {
                // Server weekly_king.value is the week's biggest fish in meters, not coins.
                var size = extra != null && extra.sizeM > 0 ? extra.sizeM : entry.value;
                if (size <= 0f) return "—";
                return DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(size));
            }
            if (boardType == BoardPond)
                return DesktopLocalization.Format("ui.leaderboard.panel.format.fish_count", (object)(Mathf.RoundToInt(entry.value)));
            return DesktopLocalization.Format("ui.leaderboard.panel.format.fish_count", (object)(Mathf.RoundToInt(entry.value)));
        }

        void EnsureRankScrollbar()
        {
            if (_scroll == null)
                return;
            var existing = _scroll.transform.Find("RankScrollbar");
            if (existing == null && _scroll.viewport != null)
                existing = _scroll.viewport.Find("RankScrollbar");
            var barGo = existing != null
                ? existing.gameObject
                : new GameObject("RankScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            // The list is the 710px column. A bar inside that rect covers the rows.
            // The profile card starts 20px further right, so the track sits in that gutter.
            barGo.transform.SetParent(_scroll.transform, false);
            barGo.transform.SetAsLastSibling();
            var bar = (RectTransform)barGo.transform;
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.offsetMin = new Vector2(8f, 8f);
            bar.offsetMax = new Vector2(16f, -8f);
            var track = barGo.GetComponent<Image>();
            track.color = new Color(0.886f, 0.812f, 0.686f, 0.95f);
            track.raycastTarget = true;
            var sliding = barGo.transform.Find("Sliding Area");
            if (sliding == null)
            {
                var slidingGo = new GameObject("Sliding Area", typeof(RectTransform));
                slidingGo.transform.SetParent(barGo.transform, false);
                sliding = slidingGo.transform;
                var slide = (RectTransform)sliding;
                slide.anchorMin = Vector2.zero;
                slide.anchorMax = Vector2.one;
                slide.offsetMin = new Vector2(1f, 1f);
                slide.offsetMax = new Vector2(-1f, -1f);
            }
            var handleTransform = sliding.Find("Handle");
            if (handleTransform == null)
            {
                var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                handleGo.transform.SetParent(sliding, false);
                handleTransform = handleGo.transform;
            }
            var handle = (RectTransform)handleTransform;
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            var handleImage = handleTransform.GetComponent<Image>();
            handleImage.color = new Color(0.72f, 0.58f, 0.40f, 1f);
            var scrollbar = barGo.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scroll.vertical = true;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        void SetMyRank(LeaderboardMyRankDto myRank, string boardType)
        {
            if (_myRank == null)
                return;
            if (myRank == null || !myRank.hasRank)
            {
                _myRank.text = DesktopLocalization.Text("ui.leaderboard.panel.label.my_rank_unlisted", "我的排名：未上榜");
                return;
            }
            _myRank.text = DesktopLocalization.Format("ui.leaderboard.panel.label.my_rank", (object)(myRank.rank), (object)(FormatValue(myRank.entry ?? new LeaderboardEntryDto
                           {
                               value = myRank.value,
                               extra = myRank.entry != null ? myRank.entry.extra : null,
                           }, boardType)));
        }

        void SetStatus(string value)
        {
            if (_status != null)
                _status.text = value ?? string.Empty;
        }

        PodiumSlot ResolvePodium(string path)
        {
            var root = transform.Find(path) ?? transform.Find("Scroll/Viewport/Content/" + path);
            if (root == null)
                return default;
            return new PodiumSlot
            {
                root = root.gameObject,
                nickname = FindIn<Text>(root, "Nickname"),
                avatar = EnsureAvatar(root.Find("Avatar")),
                value = FindIn<Text>(root, "Value"),
            };
        }

        T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path) ?? (path.StartsWith("Podium") ? transform.Find("Scroll/Viewport/Content/" + path) : null);
            return node != null ? node.GetComponent<T>() : null;
        }

        static Image EnsureAvatar(Transform node)
        {
            if (node == null)
                return null;
            var image = node.GetComponent<Image>();
            if (image != null)
                return image;
            image = node.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
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
