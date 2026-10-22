using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// FEAT-ALBUM-01：个人中心壳（侧栏 资料/展示柜/图鉴/相册/成就）。
    /// </summary>
    public sealed class DesktopProfileHubPanel : MonoBehaviour
    {
        bool _uiReady;
        public enum HubTab
        {
            Profile = 0,
            Showcase = 1,
            Codex = 2,
            Album = 3,
            Achievements = 4,
        }

        const int AlbumPinsPerPage = 2;

        static readonly string[] TabLabels = { "资料", "展示柜", "图鉴", "相册", "成就" };

        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        Action _openEdit;
        Action _openWorldMap;
        int _showcaseSelection;
        bool _showcaseBusy;
        Action<string> _toast;

        HubTab _tab = HubTab.Profile;
        string _viewingOtherPlayerId;
        ProfileHubDto _hub;
        Coroutine _loadRoutine;
        Coroutine _avatarRoutine;
        Coroutine _codexRoutine;
        int _targetGeneration;

        Text _title;
        Text _status;
        Transform _sidebar;
        readonly List<Button> _tabButtons = new List<Button>();
        readonly List<GameObject> _tabContents = new List<GameObject>();
        readonly HashSet<HubTab> _paintedTabs = new HashSet<HubTab>();
        bool _hubTabsStyled;
        string _loadedHubId;

        Image _profileAvatar;
        Text _profileAvatarLabel;
        Text _profileBody;
        Transform _showcaseGrid;
        Text _showcaseDetail;
        Transform _codexGrid;
        Text _codexDetail;
        Image _albumStage;
        Image _albumSlotL;
        Image _albumSlotR;
        Image _albumSlotLPhoto;
        Image _albumSlotRPhoto;
        Text _albumSlotLBody;
        Text _albumSlotRBody;
        Text _albumPinsPageLabel;
        Transform _achievementsList;
        int _achievementFilter;
        string _achievementCategory = "All";
        Button _editButton;
        Button _retryButton;
        FishCodexEntryDto[] _codexEntries = Array.Empty<FishCodexEntryDto>();
        int _albumPinsPage;
        int _albumSelection;
        int _albumMotionPage = -1;
        int _albumMotionSelection = -1;
        Coroutine _albumPhotoRoutine;
        int _albumPhotoLoadVersion;

        public void Bind(
            IAuthenticatedApiClient api,
            SocialPondSessionController pond,
            Action openEdit,
            Action<string> toast = null,
            Action openWorldMap = null)
        {
            _api = api;
            _pond = pond;
            _openEdit = openEdit;
            _openWorldMap = openWorldMap;
            _toast = toast;
            EnsureUi();
        }

        public void ShowSelf(HubTab tab = HubTab.Profile, bool refresh = true)
        {
            if (!string.IsNullOrEmpty(_viewingOtherPlayerId)) ResetViewedProfile();
            _viewingOtherPlayerId = null;
            _tab = tab;
            UpdateHeaderForTab();
            ApplyTabVisibility();
            if (!refresh || !isActiveAndEnabled)
                return;
            var selfId = _api != null ? _api.PlayerId : null;
            if (_hub != null && _loadedHubId == selfId)
            {
                if (!_paintedTabs.Contains(_tab))
                    RenderCurrentTab();
                return;
            }
            Refresh();
        }

        public void ShowOtherPlayer(string playerId, HubTab tab = HubTab.Profile)
        {
            if (_viewingOtherPlayerId != playerId) ResetViewedProfile();
            _viewingOtherPlayerId = playerId ?? string.Empty;
            _tab = tab;
            UpdateHeaderForTab();
            ApplyTabVisibility();
            if (isActiveAndEnabled)
                Refresh();
        }

        void ResetViewedProfile()
        {
            StopLoad();
            _targetGeneration++;
            _showcaseBusy = false;
            CloseShowcasePicker();
            if (_codexRoutine != null) StopCoroutine(_codexRoutine);
            _codexRoutine = null;
            _hub = null;
            _loadedHubId = null;
            _paintedTabs.Clear();
            _codexEntries = Array.Empty<FishCodexEntryDto>();
            _albumPinsPage = 0;
            if (_editButton != null) _editButton.gameObject.SetActive(false);
        }

        bool _suppressMotion;
        bool _skipRenderMotion;

        public void OnOpened()
        {
            if (!_uiReady)
                return;
            if (_pond != null)
            {
                _pond.CodexUnlocked -= OnCodexUnlocked;
                _pond.AchievementUnlocked -= OnAchievementUnlocked;
                _pond.CodexUnlocked += OnCodexUnlocked;
                _pond.AchievementUnlocked += OnAchievementUnlocked;
            }
            // The shell plays the page entrance. This open's render must not play it again.
            _suppressMotion = true;
            var id = string.IsNullOrEmpty(_viewingOtherPlayerId) ? _api != null ? _api.PlayerId : null : _viewingOtherPlayerId;
            if (_hub != null && _loadedHubId == id)
            {
                ApplyTabVisibility();
                if (!_paintedTabs.Contains(_tab))
                    RenderCurrentTab();
                else
                    _suppressMotion = false;
                return;
            }
            Refresh();
        }

        public void OnClosed()
        {
            StopLoad();
            _targetGeneration++;
            _showcaseBusy = false;
            CloseShowcasePicker();
            if (_codexRoutine != null) StopCoroutine(_codexRoutine);
            _codexRoutine = null;
            _albumPhotoLoadVersion++;
            if (_albumPhotoRoutine != null)
            {
                StopCoroutine(_albumPhotoRoutine);
                _albumPhotoRoutine = null;
            }
            if (_avatarRoutine != null)
            {
                StopCoroutine(_avatarRoutine);
                _avatarRoutine = null;
            }
            if (_pond != null)
            {
                _pond.CodexUnlocked -= OnCodexUnlocked;
                _pond.AchievementUnlocked -= OnAchievementUnlocked;
            }
        }

        void OnDestroy()
        {
            OnClosed();
        }

        void OnCodexUnlocked(CodexUnlockDto _)
        {
            if (_tab == HubTab.Codex || _tab == HubTab.Achievements)
                Refresh();
        }

        void OnAchievementUnlocked(AchievementUnlockDto unlock)
        {
            if (unlock != null && _toast != null)
                _toast("成就解锁：" + unlock.name);
            if (_tab == HubTab.Achievements)
                Refresh();
        }

        /// <summary>
        /// Editor Prefab 填充：写入 Shell 层级。运行时只绑定，不覆盖布局。
        /// </summary>
        public void BuildEditorLayout()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                DestroyImmediate(child);
            }
            _tabButtons.Clear();
            _tabContents.Clear();
            BuildShellHierarchy();
            RefineLayout();
            FixAlbumPagerLayout();
            ApplyCozyStyle();
            ApplyTabVisibility();
#endif
        }

        void EnsureUi()
        {
            _uiReady = false;
            if (!DesktopModalUi.RequireNodes(transform, "PanelProfileHub",
                "Shell/Header/Title", "Shell/Header/Edit", "Shell/Status",
                "Shell/Sidebar", "Shell/Content/Tab_资料/Avatar", "Shell/Content/Tab_资料/Body",
                "Shell/Content/Tab_展示柜/ShowcaseGrid/Viewport/Content", "Shell/Content/Tab_展示柜/Detail",
                "Shell/Content/Tab_图鉴/CodexGrid/Viewport/Content", "Shell/Content/Tab_图鉴/Detail",
                "Shell/Content/Tab_相册/AlbumStage/SlotL/Photo", "Shell/Content/Tab_相册/AlbumStage/SlotL/Body",
                "Shell/Content/Tab_相册/AlbumStage/SlotR/Photo", "Shell/Content/Tab_相册/AlbumStage/SlotR/Body",
                "Shell/Content/Tab_相册/Prev", "Shell/Content/Tab_相册/Next", "Shell/Content/Tab_相册/Page",
                "Shell/Content/Tab_成就/Achievements/Viewport/Content"))
                return;
            foreach (var label in TabLabels)
                if (!DesktopModalUi.RequireNodes(transform, "PanelProfileHub", "Shell/Sidebar/Tab_" + label))
                    return;
            WireFromPrefab();
            if (_title == null || _status == null || _editButton == null ||
                _profileAvatar == null || _profileBody == null || _showcaseDetail == null || _codexDetail == null ||
                _albumStage == null || _albumSlotL == null || _albumSlotR == null ||
                _albumSlotLPhoto == null || _albumSlotRPhoto == null ||
                _albumSlotLBody == null || _albumSlotRBody == null || _albumPinsPageLabel == null)
            {
                Debug.LogError("[DesktopUI] PanelProfileHub nodes require their authored Text/Image/Button components.", this);
                return;
            }
            foreach (var button in _tabButtons)
                if (button == null)
                {
                    Debug.LogError("[DesktopUI] PanelProfileHub sidebar requires five tab Buttons.", this);
                    return;
                }
            _uiReady = true;
            ApplyCozyStyle();
            RefineLayout();
            ApplyTabVisibility();
        }

        void ApplyCozyStyle()
        {
            DesktopModalUi.StyleTree(transform);
            DesktopModalUi.PlacePageSealLeft(transform);
        }

        void RefineLayout()
        {
            if (CozyItemArt.IsAuthored(transform))
                return;
            var shell = transform.Find("Shell");
            if (shell == null)
                return;
            PlaceRect(shell.Find("Header"), Vector2.up, Vector2.one,
                new Vector2(12f, -52f), new Vector2(-12f, -4f));
            PlaceRect(_sidebar, Vector2.zero, Vector2.up,
                new Vector2(12f, 36f), new Vector2(152f, -60f));
            PlaceRect(shell.Find("Content"), Vector2.zero, Vector2.one,
                new Vector2(164f, 36f), new Vector2(-12f, -60f));
            if (_title != null)
                PlaceRect(_title.transform, Vector2.zero, Vector2.one,
                    new Vector2(12f, 0f), new Vector2(-244f, 0f));
            PlaceHeaderButton(_editButton, -220f, 100f);
            PlaceHeaderButton(_retryButton, -150f, 76f);
            if (_retryButton != null)
                _retryButton.gameObject.SetActive(false);
            if (IsAchievementsPage() && _editButton != null)
                _editButton.gameObject.SetActive(false);
            if (_status != null)
                PlaceRect(_status.transform, Vector2.zero, Vector2.right,
                    new Vector2(164f, 6f), new Vector2(-16f, 30f));
            if (_sidebar != null)
            {
                var layout = _sidebar.GetComponent<VerticalLayoutGroup>();
                if (layout == null)
                    layout = _sidebar.gameObject.AddComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.padding = new RectOffset(8, 8, 12, 12);
                    layout.spacing = 8f;
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                }
            }
            foreach (var button in _tabButtons)
            {
                if (button == null)
                    continue;
                var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 44f;
                layout.preferredHeight = 44f;
                foreach (var label in button.GetComponentsInChildren<Text>(true))
                    label.fontSize = 17;
            }
            if (_profileAvatar != null)
            {
                var avatarRect = _profileAvatar.rectTransform;
                avatarRect.anchorMin = Vector2.up;
                avatarRect.anchorMax = Vector2.up;
                avatarRect.pivot = Vector2.up;
                avatarRect.anchoredPosition = new Vector2(16f, -16f);
                avatarRect.sizeDelta = new Vector2(104f, 104f);
                _profileAvatar.preserveAspect = true;
            }
            if (_profileBody != null)
            {
                PlaceRect(_profileBody.transform, Vector2.zero, Vector2.one,
                    new Vector2(136f, 12f), new Vector2(-16f, -16f));
                _profileBody.fontSize = 17;
                _profileBody.lineSpacing = 1.15f;
                _profileBody.verticalOverflow = VerticalWrapMode.Truncate;
            }
            FitGrid(_showcaseGrid, 3, true);
            FitGrid(_codexGrid, 4, true);

            var achievementsRoot = transform.Find("Shell/Content/Tab_成就");
            var achievementsScroll = achievementsRoot != null
                ? achievementsRoot.Find("Achievements") as RectTransform
                : null;
            if (achievementsScroll != null)
            {
                PlaceRect(achievementsScroll, Vector2.zero, Vector2.one,
                    new Vector2(12f, 12f), new Vector2(-12f, -52f));
                var scrollImage = achievementsScroll.GetComponent<Image>();
                if (scrollImage != null)
                    CatTheme.ApplyPaper(scrollImage);
            }
        }

        void OnRectTransformDimensionsChange()
        {

        }

        static void FitGrid(Transform content, int columns, bool fallbackPadding = false)
        {
            if (content == null)
                return;
            var grid = content.GetComponent<GridLayoutGroup>();
            var viewport = content.parent as RectTransform;
            if (grid == null || viewport == null)
                return;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            if (fallbackPadding)
                grid.padding = new RectOffset(12, 28, 18, 16);
            var width = viewport.rect.width;
            if (width < 32f && viewport.parent is RectTransform scroll)
                width = scroll.rect.width;
            if (width < 32f)
                return;
            var contentRect = content as RectTransform;
            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);
            }
            var scrollRect = viewport.GetComponent<ScrollRect>() ?? viewport.GetComponentInParent<ScrollRect>();
            if (scrollRect != null) scrollRect.horizontal = false;
            var availableWidth = width - grid.padding.left - grid.padding.right -
                                 grid.spacing.x * (columns - 1);
            grid.cellSize = new Vector2(Mathf.Max(64f, availableWidth / columns), grid.cellSize.y);
        }

        static void PlaceRect(Transform node, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = node as RectTransform;
            if (rect == null)
                return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void PlaceHeaderButton(Button button, float horizontalPosition, float width)
        {
            if (button == null)
                return;
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(horizontalPosition, 0f);
            rect.sizeDelta = new Vector2(width, 36f);
        }

        /// <summary>
        /// 旧 Prefab 仍有候选栏/单卡相册时，就地升级为左右双卡满铺分页。
        /// </summary>
        void WireFromPrefab()
        {
            _title = FindText("DesignHeader/Title") ?? FindText("Shell/Header/Title");
            _status = FindText("Shell/Status");
            _sidebar = transform.Find("Shell/Sidebar");
            _editButton = FindButton("Shell/Header/Edit");
            _retryButton = FindButton("Shell/Header/Retry");
            if (_editButton != null)
            {
                _editButton.onClick.RemoveAllListeners();
                _editButton.onClick.AddListener(() => _openEdit?.Invoke());
            }
            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveAllListeners();
                _retryButton.onClick.AddListener(Refresh);
            }

            _tabButtons.Clear();
            _tabContents.Clear();
            var content = transform.Find("Shell/Content");
            for (var i = 0; i < TabLabels.Length; i++)
            {
                var tab = (HubTab)i;
                var label = TabLabels[i];
                Button btn = null;
                if (_sidebar != null)
                {
                    var btnTf = _sidebar.Find("Tab_" + label);
                    btn = btnTf != null ? btnTf.GetComponent<Button>() : null;
                }
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    var captured = tab;
                    btn.onClick.AddListener(() => SelectTab(captured));
                    _tabButtons.Add(btn);
                }
                else
                    _tabButtons.Add(null);

                Transform page = null;
                if (content != null)
                    page = content.Find("Tab_" + label);
                _tabContents.Add(page != null ? page.gameObject : null);
            }

            WireProfileRefs();
            WireShowcaseRefs();
            WireCodexRefs();
            WireAlbumRefs();
            WireAchievementRefs();
            RebindAlbumPagers();
        }

        void WireProfileRefs()
        {
            var root = transform.Find("Shell/Content/Tab_资料");
            if (root == null) return;
            var avatar = root.Find("Avatar");
            _profileAvatar = avatar != null ? avatar.GetComponent<Image>() : null;
            _profileAvatarLabel = FindTextUnder(avatar, "Label");
            _profileBody = FindTextUnder(root, "Body");
        }

        void WireShowcaseRefs()
        {
            var root = transform.Find("Shell/Content/Tab_展示柜");
            if (root == null) return;
            _showcaseGrid = FindGridContent(root, "ShowcaseGrid");
            _showcaseDetail = FindTextUnder(root, "Detail");
            BindPagerButton(root.Find("JournalReplace"), OpenShowcasePicker);
            BindPagerButton(root.Find("JournalClear"), () => SaveShowcaseSelection(null));
            BindPagerButton(transform.Find("JournalShowcasePicker/Close"), CloseShowcasePicker);
            BindPagerButton(transform.Find("JournalShowcasePicker/Clear"), () => SaveShowcaseSelection(null));
        }

        void WireCodexRefs()
        {
            var root = transform.Find("Shell/Content/Tab_图鉴");
            if (root == null) return;
            _codexGrid = FindGridContent(root, "CodexGrid");
            _codexDetail = FindTextUnder(root, "Detail");
        }

        void WireAlbumRefs()
        {
            var root = transform.Find("Shell/Content/Tab_相册");
            if (root == null) return;
            var stage = root.Find("AlbumStage");
            _albumStage = stage != null ? stage.GetComponent<Image>() : null;
            var slotL = stage != null ? stage.Find("SlotL") : null;
            var slotR = stage != null ? stage.Find("SlotR") : null;
            _albumSlotL = slotL != null ? slotL.GetComponent<Image>() : null;
            _albumSlotR = slotR != null ? slotR.GetComponent<Image>() : null;
            var photoL = slotL != null ? slotL.Find("Photo") : null;
            var photoR = slotR != null ? slotR.Find("Photo") : null;
            _albumSlotLPhoto = photoL != null ? photoL.GetComponent<Image>() : null;
            _albumSlotRPhoto = photoR != null ? photoR.GetComponent<Image>() : null;
            BindAlbumSelection(slotL, 0);
            BindAlbumSelection(slotR, 1);
            _albumSlotLBody = FindTextUnder(slotL, "Body");
            _albumSlotRBody = FindTextUnder(slotR, "Body");
            _albumPinsPageLabel = FindTextUnder(root, "Page") ?? FindTextUnder(root, "PinsPage");
        }

        void BindAlbumSelection(Transform slot, int index)
        {
            if (slot == null) return;
            var button = slot.GetComponent<Button>() ?? slot.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { _albumSelection = index; RenderAlbum(); });
        }

        void WireAchievementRefs()
        {
            var root = transform.Find("Shell/Content/Tab_成就");
            if (root == null) return;
            _achievementsList = FindGridContent(root, "Achievements");
            if (_achievementsList == null)
            {
                var scroll = root.Find("Achievements");
                _achievementsList = scroll != null ? scroll.Find("Content") : null;
            }
            if (_achievementsList == null)
                _achievementsList = CreateScrollList(root, "Achievements", 0f, 1f);
            foreach (var category in new[] { "All", "Fishing", "Collection", "Progress" })
            {
                var captured = category;
                var button = FindButton("DesignAchievementCategories/" + category);
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    _achievementCategory = captured;
                    if (_hub != null) RenderAchievements();
                });
            }
            foreach (var filterName in new[] { "All", "Completed", "Pending" })
            {
                var filter = filterName == "All" ? 0 : filterName == "Completed" ? 1 : 2;
                var button = root.Find("DesignFilter" + filterName)?.GetComponent<Button>();
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    _achievementFilter = filter;
                    if (_hub != null) RenderAchievements();
                });
            }
        }

        void RebindAlbumPagers()
        {
            var root = transform.Find("Shell/Content/Tab_相册");
            if (root == null) return;
            Action prev = () =>
            {
                _albumSelection = 0;
                _albumPinsPage = Math.Max(0, _albumPinsPage - 1);
                RenderAlbum();
            };
            Action next = () =>
            {
                _albumSelection = 0;
                _albumPinsPage++;
                RenderAlbum();
            };
            BindPagerButton(root.Find("Prev"), prev);
            BindPagerButton(root.Find("Next"), next);
            BindPagerButton(root.Find("Prev_0"), prev);
            BindPagerButton(root.Find("Next_0"), next);
        }

        void FixAlbumPagerLayout()
        {
            var root = transform.Find("Shell/Content/Tab_相册");
            if (root == null) return;
            PlaceAlbumPagerButton(root.Find("Prev") as RectTransform, true);
            PlaceAlbumPagerButton(root.Find("Next") as RectTransform, false);
            PlaceAlbumPagerButton(root.Find("Prev_0") as RectTransform, true);
            PlaceAlbumPagerButton(root.Find("Next_0") as RectTransform, false);
        }

        static void BindPagerButton(Transform node, Action action)
        {
            if (node == null || action == null) return;
            var btn = node.GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => action());
        }

        static Transform FindGridContent(Transform tabRoot, string scrollName)
        {
            if (tabRoot == null) return null;
            var scroll = tabRoot.Find(scrollName);
            if (scroll == null) return null;
            var viewport = scroll.Find("Viewport");
            if (viewport != null)
            {
                var underVp = viewport.Find("Content");
                if (underVp != null)
                    return underVp;
            }
            return scroll.Find("Content");
        }

        static Text FindTextUnder(Transform parent, string name)
        {
            if (parent == null) return null;
            var t = parent.Find(name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        void WireExisting()
        {
            WireFromPrefab();
        }

        void BuildFallbackUi()
        {
            Debug.LogError("[DesktopUI] Prefab is missing required controls. Runtime will not build a second layout.");
            return;
        }

        void BuildShellHierarchy()
        {
            var bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color = CatTheme.Cream;

            var shell = CreatePanel("Shell", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                CatTheme.Cream);

            var header = CreatePanel("Header", shell.transform,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -52), Vector2.zero,
                CatTheme.Cream);
            _title = CreateLabel(header.transform, "Title", "个人中心", 20,
                TextAnchor.MiddleLeft, new Vector2(16, 0), new Vector2(280, 36));
            _editButton = CreateHeaderButton(header.transform, "Edit", "编辑资料",
                new Vector2(-200, 0), new Vector2(100, 32), () => _openEdit?.Invoke());

            _sidebar = CreatePanel("Sidebar", shell.transform,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(128, -52),
                CatTheme.Cream).transform;
            var sideLayout = _sidebar.gameObject.AddComponent<VerticalLayoutGroup>();
            sideLayout.padding = new RectOffset(8, 8, 12, 12);
            sideLayout.spacing = 6;
            sideLayout.childForceExpandWidth = true;
            sideLayout.childForceExpandHeight = false;
            sideLayout.childControlHeight = true;
            sideLayout.childControlWidth = true;

            var contentHost = CreatePanel("Content", shell.transform,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(128, 0), new Vector2(0, -52),
                CatTheme.Cream);

            _status = CreateLabel(shell.transform, "Status", "", 14,
                TextAnchor.LowerLeft, new Vector2(140, 8), new Vector2(600, 24));
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = new Vector2(0, 0);
            statusRt.anchorMax = new Vector2(1, 0);
            statusRt.pivot = new Vector2(0, 0);
            statusRt.anchoredPosition = new Vector2(140, 6);
            statusRt.sizeDelta = new Vector2(-160, 22);

            _tabButtons.Clear();
            _tabContents.Clear();
            for (var i = 0; i < TabLabels.Length; i++)
            {
                var tab = (HubTab)i;
                var btn = CreateNavTab(_sidebar, TabLabels[i], () => SelectTab(tab));
                _tabButtons.Add(btn);

                var page = CreatePanel("Tab_" + TabLabels[i], contentHost.transform,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    CatTheme.Cream);
                page.SetActive(false);
                _tabContents.Add(page);
                BuildTabContent(tab, page.transform);
            }
        }

        void BuildTabContent(HubTab tab, Transform parent)
        {
            switch (tab)
            {
                case HubTab.Profile:
                    BuildProfileTab(parent);
                    break;
                case HubTab.Showcase:
                    BuildSplitDetailTab(parent, "ShowcaseGrid", out _showcaseGrid, out _showcaseDetail,
                        new Vector2(200, 110), 3);
                    break;
                case HubTab.Codex:
                    BuildSplitDetailTab(parent, "CodexGrid", out _codexGrid, out _codexDetail,
                        new Vector2(150, 88), 3);
                    break;
                case HubTab.Album:
                    BuildAlbumTab(parent);
                    break;
                case HubTab.Achievements:
                    _achievementsList = CreateScrollList(parent, "Achievements", 0f, 1f);
                    break;
            }
        }

        void BuildProfileTab(Transform parent)
        {
            var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatarGo.transform.SetParent(parent, false);
            var art = avatarGo.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0, 1);
            art.anchorMax = new Vector2(0, 1);
            art.pivot = new Vector2(0, 1);
            art.anchoredPosition = new Vector2(24, -24);
            art.sizeDelta = new Vector2(120, 120);
            _profileAvatar = avatarGo.GetComponent<Image>();
            _profileAvatar.color = CatTheme.Cream;
            _profileAvatarLabel = CreateLabel(avatarGo.transform, "Label", "钓", 36,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(120, 120));
            Stretch(_profileAvatarLabel.rectTransform);
            _profileAvatarLabel.alignment = TextAnchor.MiddleCenter;

            _profileBody = CreateLabel(parent, "Body", "加载中…", 17,
                TextAnchor.UpperLeft, new Vector2(160, -24), new Vector2(520, 360));
            var brt = _profileBody.rectTransform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(160, 16);
            brt.offsetMax = new Vector2(-16, -24);
            _profileBody.lineSpacing = 1.2f;
        }

        void BuildSplitDetailTab(
            Transform parent, string gridName,
            out Transform grid, out Text detail,
            Vector2 cellSize, int columns)
        {
            grid = CreateScrollGrid(parent, gridName, columns, 0f, 1f, 0f, 0.58f, cellSize);
            detail = CreateLabel(parent, "Detail", "点击格子查看详情", 16,
                TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
            var drt = detail.rectTransform;
            drt.anchorMin = new Vector2(0.58f, 0);
            drt.anchorMax = new Vector2(1, 1);
            drt.offsetMin = new Vector2(12, 12);
            drt.offsetMax = new Vector2(-12, -12);
            detail.lineSpacing = 1.15f;
        }

        void BuildAlbumTab(Transform parent)
        {
            // 满铺展示区：左右各一卡 + 底部分页（禁止竖滑）
            var stageGo = new GameObject("AlbumStage", typeof(RectTransform), typeof(Image));
            stageGo.transform.SetParent(parent, false);
            var stageRt = stageGo.GetComponent<RectTransform>();
            stageRt.anchorMin = Vector2.zero;
            stageRt.anchorMax = Vector2.one;
            stageRt.offsetMin = new Vector2(8, 44);
            stageRt.offsetMax = new Vector2(-8, -8);
            _albumStage = stageGo.GetComponent<Image>();
            _albumStage.color = CatTheme.Cream;

            _albumSlotL = CreateAlbumSlot(stageGo.transform, "SlotL", 0f, 0.5f,
                out _albumSlotLPhoto, out _albumSlotLBody);
            _albumSlotR = CreateAlbumSlot(stageGo.transform, "SlotR", 0.5f, 1f,
                out _albumSlotRPhoto, out _albumSlotRBody);

            _albumPinsPageLabel = CreateLabel(parent, "Page", "1/1", 14,
                TextAnchor.LowerCenter, Vector2.zero, new Vector2(80, 24));
            PlacePager(_albumPinsPageLabel.rectTransform, 0f, 1f);
            CreateAlbumPagerButtons(parent);
        }

        static Image CreateAlbumSlot(
            Transform parent, string name, float xMin, float xMax,
            out Image photo, out Text body)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f);
            var padL = xMin <= 0.01f ? 0f : 4f;
            var padR = xMax >= 0.99f ? 0f : 4f;
            rt.offsetMin = new Vector2(padL, 0f);
            rt.offsetMax = new Vector2(-padR, 0f);
            var image = go.GetComponent<Image>();
            image.color = CatTheme.Cream;

            var photoGo = new GameObject("Photo", typeof(RectTransform), typeof(Image));
            photoGo.transform.SetParent(go.transform, false);
            Stretch(photoGo.GetComponent<RectTransform>());
            photo = photoGo.GetComponent<Image>();
            photo.color = CatTheme.Cream;
            photo.preserveAspect = true;
            photo.raycastTarget = false;

            var scrubGo = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrubGo.transform.SetParent(go.transform, false);
            var scrubRt = scrubGo.GetComponent<RectTransform>();
            scrubRt.anchorMin = new Vector2(0f, 0f);
            scrubRt.anchorMax = new Vector2(1f, 0.42f);
            scrubRt.offsetMin = Vector2.zero;
            scrubRt.offsetMax = Vector2.zero;
            var scrub = scrubGo.GetComponent<Image>();
            scrub.color = new Color(CatTheme.Cream.r, CatTheme.Cream.g, CatTheme.Cream.b, 0.96f);
            scrub.raycastTarget = false;

            body = CreateLabel(go.transform, "Body", "", 16,
                TextAnchor.LowerCenter, Vector2.zero, Vector2.zero);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0.42f);
            brt.offsetMin = new Vector2(12, 10);
            brt.offsetMax = new Vector2(-12, -8);
            body.alignment = TextAnchor.LowerLeft;
            body.lineSpacing = 1.15f;
            body.raycastTarget = false;
            return image;
        }

        void CreateAlbumPagerButtons(Transform parent)
        {
            var prevBtn = CreateHeaderButton(parent, "Prev", "上一页",
                Vector2.zero, new Vector2(72, 28),
                () =>
                {
                    _albumSelection = 0;
                _albumPinsPage = Math.Max(0, _albumPinsPage - 1);
                    RenderAlbum();
                });
            var nextBtn = CreateHeaderButton(parent, "Next", "下一页",
                Vector2.zero, new Vector2(72, 28),
                () =>
                {
                    _albumSelection = 0;
                _albumPinsPage++;
                    RenderAlbum();
                });
            PlaceAlbumPagerButton(prevBtn.GetComponent<RectTransform>(), true);
            PlaceAlbumPagerButton(nextBtn.GetComponent<RectTransform>(), false);
        }

        static void PlaceAlbumPagerButton(RectTransform rt, bool left)
        {
            if (rt == null) return;
            // 角点锚定，避免左右按钮被拉成全宽互相遮挡（上一页点不到）
            rt.anchorMin = left ? Vector2.zero : new Vector2(1f, 0f);
            rt.anchorMax = left ? Vector2.zero : new Vector2(1f, 0f);
            rt.pivot = left ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            rt.anchoredPosition = left ? new Vector2(12f, 4f) : new Vector2(-12f, 4f);
            rt.sizeDelta = new Vector2(72f, 28f);
        }

        static void PlacePager(RectTransform rt, float xMin, float xMax)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(xMin, 0);
            rt.anchorMax = new Vector2(xMax, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 6);
            rt.sizeDelta = new Vector2(0, 24);
        }

        void CreatePagerButtons(Transform parent, float xMin, float xMax, Action prev, Action next)
        {
            var prevBtn = CreateHeaderButton(parent, "Prev_" + xMin, "上一页",
                Vector2.zero, new Vector2(72, 28), prev);
            var nextBtn = CreateHeaderButton(parent, "Next_" + xMin, "下一页",
                Vector2.zero, new Vector2(72, 28), next);
            PlaceCornerButton(prevBtn.GetComponent<RectTransform>(), xMin, xMax, true);
            PlaceCornerButton(nextBtn.GetComponent<RectTransform>(), xMin, xMax, false);
        }

        static void PlaceCornerButton(RectTransform rt, float xMin, float xMax, bool left)
        {
            if (rt == null) return;
            var x = left ? xMin : xMax;
            rt.anchorMin = new Vector2(x, 0f);
            rt.anchorMax = new Vector2(x, 0f);
            rt.pivot = left ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            rt.anchoredPosition = left ? new Vector2(12f, 4f) : new Vector2(-12f, 4f);
            rt.sizeDelta = new Vector2(72f, 28f);
        }

        void SelectTab(HubTab tab)
        {
            if (_showcaseBusy || _tab==tab) return;
            if(!Application.isPlaying) { ApplySelectedTab(tab); return; }
            if(_tabMotionRoutine!=null) StopCoroutine(_tabMotionRoutine);
            for(var i=0;i<_tabButtons.Count;i++) _tabButtons[i]?.GetComponent<CozyButtonVisual>()?.SetSelected(i==(int)tab);
            _tabMotionRoutine=StartCoroutine(ChangeHubTab(tab));
        }
        Coroutine _tabMotionRoutine;
        IEnumerator ChangeHubTab(HubTab tab)
        {
            var generation=_targetGeneration;
            var old=(int)_tab<_tabContents.Count?_tabContents[(int)_tab]:null;
            var motion=old!=null?old.GetComponent<CozyJournalPageMotion>():null;
            if(motion!=null) yield return motion.Exit();
            if(generation!=_targetGeneration) yield break;
            var alreadyPainted = _paintedTabs.Contains(tab);
            if (alreadyPainted)
            {
                _tab = tab;
                UpdateHeaderForTab();
                ApplyTabVisibility();
            }
            else
                ApplySelectedTab(tab);
            var current=(int)_tab<_tabContents.Count?_tabContents[(int)_tab]:null;
            if(current!=null)
            {
                current.GetComponent<CozyJournalPageMotion>()?.PlayEntrance(current.GetComponentsInChildren<CozyStickerMotion>().Length);
                // The fresh render already restarted its cards and polaroid.
                if(alreadyPainted)
                {
                    ReplayCenter(current.transform);
                    CozyJournalStaticPose.RevealAside(current.transform);
                }
            }
            _tabMotionRoutine=null;
        }
        void ApplySelectedTab(HubTab tab)
        {
            if (_showcaseBusy) return;
            CloseShowcasePicker();
            if (tab == HubTab.Album)
            {
                _albumMotionPage = -1;
                _albumMotionSelection = -1;
            }
            _tab = tab;
            UpdateHeaderForTab();
            ApplyTabVisibility();
            if (_hub == null)
                Refresh();
            else if (tab == HubTab.Codex && _hub.isSelf && (_codexEntries == null || _codexEntries.Length == 0))
            {
                if (_codexRoutine != null) StopCoroutine(_codexRoutine);
                _codexRoutine = StartCoroutine(ReloadCodexThenRender());
            }
            else
                RenderCurrentTab();
        }

        IEnumerator ReloadCodexThenRender()
        {
            var generation = _targetGeneration;
            yield return LoadCodexRoutine();
            if (generation == _targetGeneration) RenderCurrentTab();
            _codexRoutine = null;
        }

        void ApplyTabVisibility()
        {
            if (!IsAchievementsPage() && _tab == HubTab.Achievements)
                _tab = HubTab.Profile;
            for (var tabIndex = 0; tabIndex < _tabContents.Count; tabIndex++)
            {
                var show = tabIndex == (int)_tab && (!Application.isPlaying || _hub != null);
                if (_tabContents[tabIndex] != null && _tabContents[tabIndex].activeSelf != show)
                    _tabContents[tabIndex].SetActive(show);
                if (tabIndex < _tabButtons.Count && _tabButtons[tabIndex] != null)
                {
                    var button = _tabButtons[tabIndex];
                    if (!IsAchievementsPage() && tabIndex == (int)HubTab.Achievements)
                    {
                        button.gameObject.SetActive(false);
                        if (_tabContents[tabIndex] != null)
                            _tabContents[tabIndex].SetActive(false);
                        continue;
                    }
                    if (!_hubTabsStyled)
                        DesktopModalUi.StyleButton(button);
                    var visual = button.GetComponent<CozyButtonVisual>();
                    if (visual != null)
                        visual.SetSelected(tabIndex == (int)_tab);
                }
            }
            _hubTabsStyled = true;
            if (_editButton != null)
                _editButton.gameObject.SetActive(_tab == HubTab.Profile &&
                    (_hub != null && _hub.canEdit && _hub.isSelf) && !IsAchievementsPage());
        }

        bool IsAchievementsPage()
        {
            return string.Equals(gameObject.name, "PanelAchievements", StringComparison.Ordinal)
                || transform.name.IndexOf("Achievements", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void UpdateHeaderForTab()
        {
            if (_title == null)
                return;
            if (IsAchievementsPage() || _tab == HubTab.Achievements)
                _title.text = "成就中心";
            else if (_hub == null || _hub.isSelf)
                _title.text = "个人中心";
        }

        void Refresh()
        {
            if (!_uiReady)
                return;
            if (!isActiveAndEnabled)
                return;
            _paintedTabs.Clear();
            StopLoad();
            _loadRoutine = StartCoroutine(LoadRoutine());
        }

        IEnumerator LoadRoutine()
        {
            if (_api == null || !_api.CanUse)
            {
                _suppressMotion = false;
                SetStatus("当前没有有效会话，请重新登录。");
                yield break;
            }

            if (_retryButton != null) _retryButton.gameObject.SetActive(false);
            SetStatus("正在加载个人中心…");
            var targetId = string.IsNullOrEmpty(_viewingOtherPlayerId)
                ? _api.PlayerId
                : _viewingOtherPlayerId;
            var done = false;
            var ok = false;
            string error = null;
            ProfileHubDto hub = null;
            yield return _api.GetProfileHub(targetId, TabApiName(_tab), (success, loaded, message) =>
            {
                ok = success;
                hub = loaded;
                error = message;
                done = true;
            });
            while (!done)
                yield return null;

            if (!ok || hub == null)
            {
                _suppressMotion = false;
                if (_retryButton != null) _retryButton.gameObject.SetActive(true);
                SetStatus(error ?? "个人中心加载失败。");
                _loadRoutine = null;
                yield break;
            }

            _hub = hub;
            _loadedHubId = targetId;
            ApplyReadOnly(hub.canEdit);
            if (_tab == HubTab.Codex)
                yield return LoadCodexRoutine();
            RenderAll();
            SetStatus(hub.isSelf
                ? "已加载本人资料。"
                : "正在查看：" + (hub.profile != null ? hub.profile.nickname : targetId));
            _loadRoutine = null;
        }

        IEnumerator LoadCodexRoutine()
        {
            if (_api == null || !_api.CanUse || _hub == null || !_hub.isSelf)
            {
                _codexEntries = Array.Empty<FishCodexEntryDto>();
                yield break;
            }
            var done = false;
            var generation = _targetGeneration;
            yield return _api.GetCodex((ok, entries, _) =>
            {
                if (generation == _targetGeneration)
                    _codexEntries = ok && entries != null ? entries : Array.Empty<FishCodexEntryDto>();
                done = true;
            });
            while (!done)
                yield return null;
        }

        void ApplyReadOnly(bool canEdit)
        {
            if (_editButton != null)
                _editButton.gameObject.SetActive(canEdit);
        }

        /// <summary>Render a real API snapshot through the same bindings used after a network refresh.</summary>
        public void PresentSnapshot(ProfileHubDto hub, FishCodexEntryDto[] codex, HubTab tab)
        {
            EnsureUi();
            _hub = hub;
            _codexEntries = codex ?? Array.Empty<FishCodexEntryDto>();
            _tab = tab;
            RenderAll();
        }

        void RenderAll()
        {
            ApplyTabVisibility();
            RenderCurrentTab();
        }

        void RenderCurrentTab()
        {
            if (_hub == null)
                return;
            var suppress = _suppressMotion;
            _suppressMotion = false;
            var previous = _skipRenderMotion;
            _skipRenderMotion = suppress;
            try
            {
            switch (_tab)
            {
                case HubTab.Profile:
                    RenderProfile();
                    break;
                case HubTab.Showcase:
                    RenderShowcase();
                    break;
                case HubTab.Codex:
                    RenderCodex();
                    break;
                case HubTab.Album:
                    RenderAlbum();
                    break;
                case HubTab.Achievements:
                    RenderAchievements();
                    break;
            }
            _paintedTabs.Add(_tab);
            }
            finally
            {
                _skipRenderMotion = previous;
            }
        }

        void RenderProfile()
        {
            if (_hub.profile == null)
                return;
            var p = _hub.profile;
            var level = _hub.progress != null ? _hub.progress.level : 0;
            var xp = _hub.progress != null ? _hub.progress.xp : 0;
            var codex = _hub.codexSummary != null
                ? _hub.codexSummary.unlockedCount + "/" + _hub.codexSummary.totalSpecies
                : "-";
            if (_profileBody != null)
            {
                _profileBody.text =
                    "昵称：" + p.nickname + "\n" +
                    "ID：" + p.playerId + "\n" +
                    "简介：" + (string.IsNullOrEmpty(p.bio) ? "（暂无）" : p.bio) + "\n" +
                    "钓鱼等级：" + level + "　XP：" + xp + "\n" +
                    "图鉴进度：" + codex + "\n" +
                    (_hub.isSelf ? "金币：" + p.coins + "\n" : "") +
                    (_hub.isSelf && !string.IsNullOrEmpty(p.shareVisibility)
                        ? "隐私：" + p.shareVisibility + "\n"
                        : "");
            }
            if (_title != null)
                _title.text = _hub.isSelf ? "个人中心" : "钓友资料 · " + p.nickname;

            if (_profileAvatarLabel != null)
            {
                _profileAvatarLabel.text = string.IsNullOrEmpty(p.avatarUrl)
                    ? (string.IsNullOrEmpty(p.nickname) ? "钓" : p.nickname.Substring(0, 1))
                    : DesktopDefaultAvatars.LabelFor(p.avatarUrl);
            }
            var authoredProfile = transform.Find("Shell/Content/Tab_资料");
            if (authoredProfile != null)
            {
                CozyItemArt.Text(authoredProfile, "DesignName", p.nickname);
                CozyItemArt.Text(authoredProfile, "DesignBio", string.IsNullOrEmpty(p.bio) ? "今天也想钓到一条大鱼" : p.bio);
                CozyItemArt.Text(authoredProfile, "DesignId", "ID：" + p.playerId);
                CozyItemArt.Text(authoredProfile, "DesignStats", "钓鱼等级  " + (_hub.progress != null ? _hub.progress.level : 1) +
                    "    XP  " + (_hub.progress != null ? _hub.progress.xp : 0) + "\n图鉴收集  " +
                    (_hub.codexSummary != null ? _hub.codexSummary.unlockedCount + " / " + _hub.codexSummary.totalSpecies : "—"));
                CozyItemArt.Text(authoredProfile, "DesignPrivacy", _hub.isSelf ? "金币  " + p.coins + "\n动态可见范围  " +
                    (p.shareVisibility == "friends" ? "好友" : "公开") : "");
                CozyItemArt.Rows(authoredProfile, "DesignStatCards",
                    "trophy", "钓鱼等级", "Lv." + (_hub.progress != null ? _hub.progress.level : 1),
                    "star", "经验", (_hub.progress != null ? _hub.progress.xp : 0) + " XP",
                    "fish", "图鉴收集", _hub.codexSummary != null
                        ? _hub.codexSummary.unlockedCount + " / " + _hub.codexSummary.totalSpecies : "—");
                var visibility = p.shareVisibility == "private" ? "仅自己" : p.shareVisibility == "friends" ? "好友" : p.shareVisibility == "public" ? "公开" : "由对方隐私设置决定";
                if (_hub.isSelf)
                    CozyItemArt.Rows(authoredProfile, "DesignInfoRows",
                        "coin", "金币", p.coins.ToString(), "social", "动态可见范围", visibility);
                else
                    CozyItemArt.Rows(authoredProfile, "DesignInfoRows", "social", "动态可见范围", visibility);
                FishInventoryItemDto showcased = null;
                foreach (var fish in _hub.showcaseFish ?? Array.Empty<FishInventoryItemDto>())
                    if (fish != null) { showcased = fish; break; }
                CozyItemArt.Set(authoredProfile, "DesignShowcase/Art", showcased != null ? CozyItemArt.Fish(showcased.speciesId) : null);
                CozyItemArt.Text(authoredProfile, "DesignShowcase/Species", showcased != null ? DesktopFishCatalog.SpeciesName(showcased.speciesId) : "暂无展示鱼获");
                CozyItemArt.Text(authoredProfile, "DesignShowcase/Size", showcased != null ? showcased.sizeM.ToString("0.00") + " m" : "");
                RenderJournalProfile(authoredProfile, level, xp);
                var showcaseButton = authoredProfile.Find("DesignShowcase/Open")?.GetComponent<Button>();
                if (showcaseButton != null)
                {
                    showcaseButton.onClick.RemoveAllListeners();
                    showcaseButton.onClick.AddListener(() => SelectTab(HubTab.Showcase));
                }
            }
            if (_avatarRoutine != null)
                StopCoroutine(_avatarRoutine);
            if (Application.isPlaying) _avatarRoutine = StartCoroutine(LoadAvatarRoutine(p.avatarUrl, p.nickname, p.playerId));
            else if (_profileAvatar != null) { _profileAvatar.sprite = DesktopDefaultAvatars.Portrait(p.avatarUrl, p.playerId); _profileAvatar.color = Color.white; _profileAvatar.enabled = true; }
        }

        [Serializable] sealed class JournalLevel { public int level; public int xpToNext; }
        [Serializable] sealed class JournalLevels { public JournalLevel[] rows; }

        void RenderJournalProfile(Transform profile, int level, int xp)
        {
            if (profile.Find("JournalProgress") == null) return;
            var asset = Resources.Load<TextAsset>("Desktop/Theme/Cozy/data/player-levels");
            var levels = asset != null ? JsonUtility.FromJson<JournalLevels>("{\"rows\":" + asset.text + "}") : null;
            var required = 0;
            foreach (var row in levels?.rows ?? Array.Empty<JournalLevel>()) if (row.level == level) required = row.xpToNext;
            CozyItemArt.Text(profile, "JournalProgress/Level", required > 0 ? "Lv." + level + " → Lv." + (level + 1) : "Lv." + level);
            CozyItemArt.Text(profile, "JournalProgress/Caption", required > 0 ? xp + " / " + required + " 经验 · 去收获下一段故事吧。" : xp + " 经验");
            var fill = profile.Find("JournalProgress/Track/Fill")?.GetComponent<Image>();
            if (fill != null)
            {
                var ratio = required > 0 ? Mathf.Clamp01((float)xp / required) : 1;
                var progress = fill.GetComponent<CozyJournalProgressFill>();
                if (progress != null) progress.SetValue(ratio); else fill.fillAmount = ratio;
            }
            BindPagerButton(profile.Find("JournalShowcaseLink"), () => SelectTab(HubTab.Showcase));
            BindPagerButton(profile.Find("JournalAlbumLink"), () => SelectTab(HubTab.Album));
            if (!_skipRenderMotion)
                CozyJournalStaticPose.RevealAside(profile);
        }

        IEnumerator LoadAvatarRoutine(string avatarUrl, string nickname, string playerId)
        {
            if (_profileAvatar == null)
                yield break;
            _profileAvatar.enabled = true;
            if (string.IsNullOrEmpty(avatarUrl) || DesktopDefaultAvatars.IsDefaultPath(avatarUrl))
            {
                _profileAvatar.sprite = DesktopDefaultAvatars.Portrait(avatarUrl, playerId);
                _profileAvatar.color = Color.white;
                _profileAvatar.preserveAspect = true;
                if (_profileAvatarLabel != null) _profileAvatarLabel.gameObject.SetActive(false);
                yield break;
            }
            _profileAvatar.color = CatTheme.Cream;
            _profileAvatar.sprite = null;
            if (_profileAvatarLabel != null)
            {
                _profileAvatarLabel.gameObject.SetActive(true);
                _profileAvatarLabel.text = string.IsNullOrEmpty(avatarUrl)
                    ? (string.IsNullOrEmpty(nickname) ? "钓" : nickname.Substring(0, 1))
                    : DesktopDefaultAvatars.LabelFor(avatarUrl);
            }

            var url = avatarUrl;
            if (string.IsNullOrEmpty(url))
                yield break;

            if (url.StartsWith("data:image/"))
            {
                ApplyDataUrlAvatar(url);
                yield break;
            }

            if (_api == null || string.IsNullOrEmpty(_api.BaseUrl))
                yield break;
            if (url.StartsWith("/"))
                url = _api.BaseUrl + url;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                yield break;

            using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    yield break;
                var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                if (texture == null || _profileAvatar == null)
                    yield break;
                _profileAvatar.sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                _profileAvatar.color = Color.white;
                if (_profileAvatarLabel != null)
                    _profileAvatarLabel.gameObject.SetActive(false);
            }
        }

        void ApplyDataUrlAvatar(string dataUrl)
        {
            var comma = dataUrl.IndexOf(',');
            if (comma < 0 || _profileAvatar == null)
                return;
            try
            {
                var bytes = Convert.FromBase64String(dataUrl.Substring(comma + 1));
                var texture = new Texture2D(2, 2);
                if (!ImageConversion.LoadImage(texture, bytes))
                    return;
                _profileAvatar.sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                _profileAvatar.color = Color.white;
                if (_profileAvatarLabel != null)
                    _profileAvatarLabel.gameObject.SetActive(false);
            }
            catch
            {
                // keep initial label
            }
        }

        void RenderCodex()
        {
            if (_codexGrid == null)
                return;
            FitGrid(_codexGrid, 4);
            ClearChildren(_codexGrid);
            if (_hub == null)
                return;

            if (!_hub.isSelf)
            {
                var summary = _hub.codexSummary;
                CreateCard(_codexGrid, "sum",
                    summary != null
                        ? "图鉴进度\n" + summary.unlockedCount + " / " + summary.totalSpecies
                        : "图鉴摘要不可用",
                    null, null, 15);
                if (_codexDetail != null)
                    _codexDetail.text = "他人仅可见图鉴进度摘要。";
                return;
            }

            var catalog = DesktopGameData.Species;
            if (catalog == null || catalog.Length == 0)
            {
                if (_codexDetail != null)
                    _codexDetail.text = "未加载 fish_species 数值表。";
                return;
            }
            var unlocked = 0;
            for (var i = 0; i < catalog.Length; i++)
            {
                var def = catalog[i];
                if (def == null || string.IsNullOrEmpty(def.speciesId))
                    continue;
                var entry = FindCodexEntry(def.speciesId);
                var got = entry != null && entry.totalCaught > 0;
                if (got) unlocked++;
                var captured = DesktopFishCatalog.GetSpecies(def.speciesId);
                var capturedEntry = entry;
                CreateCard(_codexGrid, def.speciesId,
                    got ? DesktopGameData.SpeciesName(def.speciesId) : "尚未发现",
                    () => ShowCodexDetail(captured, capturedEntry),
                    got ? new Color(1f, 0.92f, 0.78f, 1f) : CatTheme.Cream,
                    15, CozyItemArt.Fish(def.speciesId), !got,
                    got ? DesktopFishCatalog.StarRich(captured != null ? captured.QualityMax : 1) : "捕获后解锁", got);
            }
            CozyItemArt.Text(transform, "Shell/Content/Tab_图鉴/DesignGridHeading", "鱼类图鉴 · 已收录 " + unlocked + " / " + catalog.Length);
            if (_codexGrid.childCount > 0) _codexGrid.GetChild(0).GetComponent<Button>()?.onClick.Invoke();
            ReplayCenter(_codexGrid);
        }

        FishCodexEntryDto FindCodexEntry(string speciesId)
        {
            if (_codexEntries == null) return null;
            for (var i = 0; i < _codexEntries.Length; i++)
            {
                if (_codexEntries[i] != null && _codexEntries[i].speciesId == speciesId)
                    return _codexEntries[i];
            }
            return null;
        }

        void ShowCodexDetail(DesktopFishCatalog.SpeciesInfo species, FishCodexEntryDto entry)
        {
            if (_codexDetail == null || species == null)
                return;
            var got = entry != null && entry.totalCaught > 0;
            var explore = _codexDetail.transform.parent.Find("JournalExplore");
            if (explore != null)
            {
                explore.gameObject.SetActive(!got);
                var button = explore.GetComponent<Button>();
                button.interactable = _openWorldMap != null;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _openWorldMap?.Invoke());
            }
            var root = _codexDetail.transform.parent;
            CozyItemArt.Text(root, "DesignDetailHeading", got ? species.Name + " · 图鉴" : "未发现的鱼种");
            CozyItemArt.Text(root, "JournalPhotoCaption", got ? species.Name : "等待一次相遇");
            CozyItemArt.Set(root, "DesignDetailArt", CozyItemArt.Fish(species.Id));
            var art = root.Find("DesignDetailArt")?.GetComponent<Image>();
            if (art != null) art.color = got ? Color.white : new Color(.3f,.25f,.2f,.45f);
            for (var i=0;i<4;i++) root.Find("JournalRecord"+i)?.gameObject.SetActive(got);
            if (!got)
            {
                _codexDetail.text = "还没有遇见这位朋友。捕获后解锁食性、推荐鱼饵和捕获纪录。";
                _codexDetail.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 88);
                CozyJournalChips.Show(_codexDetail);
                return;
            }
            _codexDetail.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 4);
            _codexDetail.text = string.Empty;
            CozyJournalChips.Show(_codexDetail, species.DietLabel, FirstBait(species));
            var chipRow = root.Find("JournalChips") as RectTransform;
            var firstRecord = root.Find("JournalRecord0") as RectTransform;
            if (chipRow != null && firstRecord != null)
            {
                chipRow.anchorMin = firstRecord.anchorMin;
                chipRow.anchorMax = firstRecord.anchorMax;
                chipRow.pivot = new Vector2(0f, 1f);
                chipRow.anchoredPosition = firstRecord.anchoredPosition + new Vector2(0f, 36f);
                chipRow.SetSiblingIndex(firstRecord.GetSiblingIndex());
            }
            var values = new[] { entry.maxSizeM.ToString("0.00") + " m", entry.totalCaught + " 次", RecordDate(entry.firstCaughtAt), RecordDate(entry.lastCaughtAt) };
            var labels = new[] { "最大体长", "累计捕获", "首次相遇", "最近相遇" };
            for(var i=0;i<4;i++)
            {
                CozyItemArt.Text(root,"JournalRecord"+i+"/Label",labels[i]);
                CozyItemArt.Text(root,"JournalRecord"+i+"/Value",values[i]);
            }
        }

        static string FirstBait(DesktopFishCatalog.SpeciesInfo species)
        {
            var baits = DesktopFishCatalog.TopBaits(species);
            if (string.IsNullOrEmpty(baits) || baits == "—") return null;
            var cut = baits.IndexOf('、');
            return cut > 0 ? baits.Substring(0, cut) : baits;
        }

        static string RecordDate(long timestamp)
        { return timestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime.ToString("yyyy.MM.dd") : "—"; }

        void RenderShowcase()
        {
            if (_showcaseGrid == null)
                return;
            FitGrid(_showcaseGrid, 3);
            ClearChildren(_showcaseGrid);
            var fish = _hub.showcaseFish ?? Array.Empty<FishInventoryItemDto>();
            for (var i = 0; i < 8; i++)
            {
                var item = i < fish.Length ? fish[i] : null;
                var slot = i + 1;
                var index = i;
                if (item == null)
                {
                    CreateCard(_showcaseGrid, "Slot" + i, "留给下一次惊喜", () => SelectShowcase(index), null, 16, null, false, "点击选择一条鱼", false, slot);
                    continue;
                }
                var captured = item;
                var name = DesktopFishCatalog.SpeciesName(item.speciesId);
                if (string.IsNullOrEmpty(name) && item.sizeM <= 0.001f)
                {
                    CreateCard(_showcaseGrid, "Slot" + i, "留给下一次惊喜", () => SelectShowcase(index), null, 16, null, false, "点击选择一条鱼", false, slot);
                    continue;
                }
                name = name ?? item.speciesId ?? "鱼获";
                CreateCard(_showcaseGrid, "Slot" + i,
                    name,
                    () => SelectShowcase(index),
                    CatTheme.Cream, 15, CozyItemArt.Fish(item.speciesId), false, item.sizeM.ToString("0.00") + " m · " + DesktopFishCatalog.StarRich(item.quality), false, slot);
            }
            SelectShowcase(Mathf.Clamp(_showcaseSelection, 0, 7));
            ReplayCenter(_showcaseGrid);
        }

        void SelectShowcase(int index)
        {
            if (_showcaseBusy || _hub == null) return;
            _showcaseSelection = index;
            if(_showcaseGrid!=null) foreach(Transform slot in _showcaseGrid)
                slot.GetComponent<CozyButtonVisual>()?.SetSelected(slot.name=="Slot"+index);
            var fish = _hub.showcaseFish;
            var item = fish != null && index < fish.Length ? fish[index] : null;
            if (item != null) ShowFishDetail(_showcaseDetail, "展示柜格子 " + (index + 1), item);
            else ShowShowcaseNote("格子 " + (index + 1) + " 为空。选择一条背包鱼获摆在这里。");
            var root = transform.Find("Shell/Content/Tab_展示柜");
            foreach (var name in new[] { "JournalReplace", "JournalClear" })
            {
                var button = root?.Find(name)?.GetComponent<Button>();
                if (button == null) continue;
                button.gameObject.SetActive(_hub.canEdit);
                button.interactable = name == "JournalReplace" || item != null;
                if (name == "JournalReplace") DesktopModalUi.SetButtonLabel(button, item != null ? "更换鱼获" : "选择鱼获");
            }
        }

        void OpenShowcasePicker()
        {
            if (_showcaseBusy || _hub == null || !_hub.canEdit || _api == null || !_api.CanUse) return;
            var picker = transform.Find("JournalShowcasePicker");
            if (picker == null) { SetStatus("展示鱼获选择器资源缺失，请重新导入 UI。"); return; }
            picker.gameObject.SetActive(true); picker.SetAsLastSibling();
            var content = picker.Find("Viewport/Content");
            if (content == null) return;
            ClearChildren(content);
            CozyItemArt.Text(picker, "Title", "正在读取背包鱼获…");
            var generation = _targetGeneration;
            var slot = _showcaseSelection;
            StartCoroutine(_api.GetInventoryItems((ok, inventory, error) =>
            {
                if (this == null || generation != _targetGeneration || !picker.gameObject.activeSelf || slot != _showcaseSelection) return;
                CozyItemArt.Text(picker, "Title", !ok ? (error ?? "读取失败，请关闭后重试") : inventory == null || inventory.Length == 0 ? "背包暂时没有可展示的鱼获" : "选择展示位 " + (slot + 1) + " 的鱼获");
                if (!ok) return;
                foreach (var fish in inventory ?? Array.Empty<FishInventoryItemDto>())
                {
                    if (fish == null) continue;
                    var selected = fish;
                    var row = DesktopUiPrefabFactory.Instantiate("ShowcaseSlot", content);
                    if (row == null) continue;
                    CozyItemArt.Text(row.transform, "Label", DesktopProfileCache.FishLabel(fish));
                    CozyItemArt.Set(row.transform, "DesignItemArt", CozyItemArt.Fish(fish.speciesId));
                    var button = row.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => SaveShowcaseSelection(selected.id));
                    }
                }
            }));
        }

        void CloseShowcasePicker()
        {
            if (_showcaseBusy) return;
            transform.Find("JournalShowcasePicker")?.gameObject.SetActive(false);
        }

        void SaveShowcaseSelection(string fishId)
        {
            if (_showcaseBusy || _hub == null || !_hub.canEdit || _api == null || !_api.CanUse) return;
            var slots = new string[PlayerProfileDto.ShowcaseSlotCount];
            var existing = _hub.profile?.showcaseFishIds;
            if (existing != null) Array.Copy(existing, slots, Math.Min(existing.Length, slots.Length));
            slots[Mathf.Clamp(_showcaseSelection, 0, slots.Length - 1)] = fishId;
            _showcaseBusy = true;
            SetStatus("正在保存展示鱼获…");
            var generation = _targetGeneration;
            var picker = transform.Find("JournalShowcasePicker");
            if (picker != null) CozyItemArt.Text(picker, "Title", "正在保存展示鱼获…");
            StartCoroutine(_api.SetShowcase(slots, (ok, profile, error) =>
            {
                if (this == null || generation != _targetGeneration) return;
                _showcaseBusy = false;
                if (!ok)
                {
                    SetStatus(error ?? "保存失败，请重试。");
                    if (picker != null) CozyItemArt.Text(picker, "Title", error ?? "保存失败，请重新选择或关闭");
                    return;
                }
                if (profile != null) DesktopProfileCache.Latest = profile;
                CloseShowcasePicker();
                _toast?.Invoke(string.IsNullOrEmpty(fishId) ? "展示格已清空，鱼获仍在背包里" : "鱼获已经摆进展示柜");
                Refresh();
            }));
        }

        void ShowShowcaseNote(string message)
        {
            if (_showcaseDetail == null)
                return;
            CozyItemArt.Rows(_showcaseDetail.transform.parent, "ShowcaseDetailRows");
            CozyItemArt.Set(_showcaseDetail.transform.parent, "DesignDetailArt", null);
            CozyItemArt.Text(_showcaseDetail.transform.parent, "JournalPhotoCaption", "还没有摆上鱼获");
            CozyItemArt.Text(_showcaseDetail.transform.parent, "DesignDetailHeading", "展示位 " + (_showcaseSelection+1));
            _showcaseDetail.text = "从背包里选一条最喜欢的鱼，贴进这一个展示格。";
            CozyJournalChips.Show(_showcaseDetail);
        }

        void ShowFishDetail(Text target, string title, FishInventoryItemDto item)
        {
            if (target == null || item == null)
                return;
            var species = DesktopFishCatalog.GetSpecies(item.speciesId);
            if (target.transform.parent.Find("JournalReplace") != null)
            {
                var root = target.transform.parent;
                CozyItemArt.Set(root,"DesignDetailArt",CozyItemArt.Fish(item.speciesId));
                CozyItemArt.Text(root,"JournalPhotoCaption",DesktopFishCatalog.SpeciesName(item.speciesId));
                CozyItemArt.Text(root,"DesignDetailHeading","展示位 " + (_showcaseSelection+1));
                var rows = root.Find("ShowcaseDetailRows");
                if (rows != null) rows.gameObject.SetActive(false);
                target.supportRichText = true;
                target.text = item.sizeM.ToString("0.00") + " m · " + DesktopFishCatalog.StarRich(item.quality);
                CozyJournalChips.Show(target, PondLabel(item.pondId), "已珍藏在展示柜");
                if (!_skipRenderMotion)
                    CozyJournalStaticPose.RevealAside(root);
                return;
            }
            CozyItemArt.Set(target.transform.parent, "DesignDetailArt", CozyItemArt.Fish(item.speciesId));
            CozyItemArt.Text(target.transform.parent, "JournalPhotoCaption", DesktopFishCatalog.SpeciesName(item.speciesId));
            var name = species != null ? species.Name : item.speciesId;
            var caught = item.caughtAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(item.caughtAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "—";
            var cells = new List<string>
            {
                "star", "品质", DesktopFishCatalog.QualityName(item.quality),
                "hook", "体长", item.sizeM.ToString("0.00") + " m",
                "fish", "重量", DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(item.sizeM)),
                "clock", "捕获时间", caught,
            };
            if (!string.IsNullOrEmpty(item.pondId))
            {
                cells.Add("pond");
                cells.Add("来源塘");
                cells.Add(item.pondId);
            }
            if (species != null)
            {
                cells.Add("leaf");
                cells.Add("食性");
                cells.Add(species.DietLabel);
                cells.Add("medal");
                cells.Add("脱钩率");
                cells.Add((species.BaseEscapeRate * 100f).ToString("0.0") + "%");
                cells.Add("rod");
                cells.Add("推荐鱼饵");
                cells.Add(DesktopFishCatalog.TopBaits(species));
            }
            if (CozyItemArt.Rows(target.transform.parent, "ShowcaseDetailRows", cells.ToArray()))
            {
                target.text = name;
                return;
            }
            target.text =
                title + "\n\n" +
                name + "\n" +
                "品质：" + DesktopFishCatalog.QualityName(item.quality) + "\n" +
                "体长：" + item.sizeM.ToString("0.00") + "m\n" +
                "重量：" + DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(item.sizeM)) + "\n" +
                "捕获时间：" + caught + "\n" +
                (string.IsNullOrEmpty(item.pondId) ? "" : "来源塘：" + item.pondId + "\n") +
                (species != null
                    ? "食性：" + species.DietLabel + "\n脱钩率：" +
                      (species.BaseEscapeRate * 100f).ToString("0.0") + "%\n推荐鱼饵：" +
                      DesktopFishCatalog.TopBaits(species)
                    : "");
        }

        void RenderAlbum()
        {
            if (_albumSlotLBody == null || _albumSlotRBody == null || _hub == null)
                return;

            var pins = _hub.albumPins ?? Array.Empty<AlbumCardDto>();
            var albumRoot = transform.Find("Shell/Content/Tab_相册");
            var pageCountForButtons = Math.Max(1, (pins.Length + AlbumPinsPerPage - 1) / AlbumPinsPerPage);
            _albumPinsPage = Mathf.Clamp(_albumPinsPage, 0, pageCountForButtons - 1);
            var prevButton = albumRoot?.Find("Prev")?.GetComponent<Button>();
            var nextButton = albumRoot?.Find("Next")?.GetComponent<Button>();
            if (prevButton != null) prevButton.interactable = _albumPinsPage > 0;
            if (nextButton != null) nextButton.interactable = _albumPinsPage + 1 < pageCountForButtons;
            var leftButton = _albumSlotL != null ? _albumSlotL.GetComponent<Button>() : null;
            var rightButton = _albumSlotR != null ? _albumSlotR.GetComponent<Button>() : null;
            if (leftButton != null) leftButton.interactable = pins.Length > _albumPinsPage * 2;
            if (rightButton != null) rightButton.interactable = pins.Length > _albumPinsPage * 2 + 1;
            var aside = albumRoot?.Find("JournalAlbumAside");
            if (aside != null)
            {
                var card = pins.Length > 0 ? pins[Mathf.Clamp(_albumPinsPage * AlbumPinsPerPage + _albumSelection, 0, pins.Length - 1)] : null;
                CozyItemArt.Set(aside, "Art", card != null ? CozyItemArt.Fish(card.speciesId) : null);
                CozyItemArt.Text(aside, "Caption", card != null ? DesktopFishCatalog.SpeciesName(card.speciesId) : "下一页，等你来写");
                CozyItemArt.Text(aside, "Detail", card != null ? FormatAlbumAside(card) : "每次相遇都值得留念。新的鱼获故事，会写进湖畔相册。");
                var detail = aside.Find("Detail")?.GetComponent<Text>();
                if (card != null)
                    CozyJournalChips.Show(detail, AlbumDate(card), AlbumPond(card));
                else
                    CozyJournalChips.Show(detail);
            }

            if (pins.Length == 0)
            {
                _albumPinsPage = 0;
                ClearAlbumPhoto(_albumSlotLPhoto);
                ClearAlbumPhoto(_albumSlotRPhoto);
                ApplyAlbumFace(_albumSlotLBody, null, "相册为空\n稀有 / 大鱼 / 回鱼 / 新图鉴会自动记入");
                ApplyAlbumFace(_albumSlotRBody, null, string.Empty);
                if (_albumPinsPageLabel != null)
                    _albumPinsPageLabel.text = "0/0";
                if (_albumMotionPage != 0 || _albumMotionSelection != _albumSelection)
                {
                    _albumMotionPage = 0;
                    _albumMotionSelection = _albumSelection;
                    if (!_skipRenderMotion)
                        PlayAlbumTurn(albumRoot);
                }
                return;
            }

            var pageCount = Math.Max(1, (pins.Length + AlbumPinsPerPage - 1) / AlbumPinsPerPage);
            _albumPinsPage = Mathf.Clamp(_albumPinsPage, 0, pageCount - 1);
            var start = _albumPinsPage * AlbumPinsPerPage;
            var left = start < pins.Length ? pins[start] : null;
            var right = start + 1 < pins.Length ? pins[start + 1] : null;

            ApplyAlbumFace(_albumSlotLBody, left, string.Empty);
            ApplyAlbumFace(_albumSlotRBody, right, string.Empty);
            if (_albumPinsPage != _albumMotionPage || _albumSelection != _albumMotionSelection)
            {
                _albumMotionPage = _albumPinsPage;
                _albumMotionSelection = _albumSelection;
                if (!_skipRenderMotion)
                    PlayAlbumTurn(albumRoot);
            }

            if (_albumPinsPageLabel != null)
                _albumPinsPageLabel.text = (_albumPinsPage + 1) + "/" + pageCount;

            if (_albumPhotoRoutine != null)
                StopCoroutine(_albumPhotoRoutine);
            _albumPhotoLoadVersion++;
            _albumPhotoRoutine = StartCoroutine(LoadAlbumPhotosRoutine(
                left, right, _albumPhotoLoadVersion));
        }

        IEnumerator LoadAlbumPhotosRoutine(AlbumCardDto left, AlbumCardDto right, int version)
        {
            yield return LoadAlbumSlotPhoto(_albumSlotLPhoto, ResolveAlbumPhotoUrl(left), version);
            if (version != _albumPhotoLoadVersion)
                yield break;
            yield return LoadAlbumSlotPhoto(_albumSlotRPhoto, ResolveAlbumPhotoUrl(right), version);
            _albumPhotoRoutine = null;
        }

        string ResolveAlbumPhotoUrl(AlbumCardDto card)
        {
            if (card == null)
                return null;
            if (!string.IsNullOrEmpty(card.photoUrl))
                return card.photoUrl;
            var avatar = _hub != null && _hub.profile != null ? _hub.profile.avatarUrl : null;
            var playerId = _hub != null && _hub.profile != null ? _hub.profile.playerId : null;
            return DesktopDefaultAvatars.ResolveFishingPhotoPath(avatar, playerId);
        }

        IEnumerator LoadAlbumSlotPhoto(Image target, string url, int version)
        {
            if (target == null)
                yield break;
            if (string.IsNullOrEmpty(url))
            {
                ClearAlbumPhoto(target);
                yield break;
            }

            if (url.StartsWith("/"))
                url = (_api != null ? _api.BaseUrl : string.Empty) + url;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                ClearAlbumPhoto(target);
                yield break;
            }

            target.color = target.sprite != null ? Color.white : CatTheme.Cream;
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (version != _albumPhotoLoadVersion || target == null)
                    yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ClearAlbumPhoto(target);
                    yield break;
                }
                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    ClearAlbumPhoto(target);
                    yield break;
                }
                target.sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                target.preserveAspect = true;
                target.color = Color.white;
            }
        }

        static void ClearAlbumPhoto(Image target)
        {
            if (target == null)
                return;
            target.sprite = null;
            target.color = CatTheme.Cream;
        }

        static void ApplyAlbumFace(Text body, AlbumCardDto pin, string empty)
        {
            if (body == null)
                return;
            var date = EnsureAlbumDate(body);
            if (pin == null)
            {
                body.text = empty ?? string.Empty;
                date.gameObject.SetActive(false);
                return;
            }
            var rare = pin.quality == "blue" || pin.quality == "purple" || pin.quality == "red" ||
                       pin.quality == "orange" || pin.quality == "gold";
            body.supportRichText = false;
            body.text = DesktopFishCatalog.SpeciesName(pin.speciesId) + " · " + pin.sizeM.ToString("0.00") + " m\n" +
                        (rare ? "难忘的稀有鱼获" : "值得记下的一次收获");
            date.gameObject.SetActive(true);
            date.text = AlbumDate(pin) + " · " + AlbumPond(pin);
        }

        static Text EnsureAlbumDate(Text body)
        {
            var existing = body.transform.parent.Find("Date")?.GetComponent<Text>();
            if (existing != null)
                return existing;
            var go = new GameObject("Date", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(body.transform.parent, false);
            var date = go.GetComponent<Text>();
            date.font = body.font;
            date.fontSize = 13;
            date.alignment = TextAnchor.MiddleCenter;
            date.color = new Color(178f / 255f, 141f / 255f, 104f / 255f, 1f);
            date.raycastTarget = false;
            date.horizontalOverflow = HorizontalWrapMode.Wrap;
            date.verticalOverflow = VerticalWrapMode.Truncate;
            var rect = date.rectTransform;
            var source = body.rectTransform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = new Vector2(source.sizeDelta.x, 22f);
            rect.anchoredPosition = source.anchoredPosition + new Vector2(0f, -source.sizeDelta.y + 22f);
            var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(go.transform, false);
            var ruleRect = (RectTransform)rule.transform;
            ruleRect.anchorMin = new Vector2(0.08f, 1f);
            ruleRect.anchorMax = new Vector2(0.92f, 1f);
            ruleRect.pivot = new Vector2(0.5f, 1f);
            ruleRect.sizeDelta = new Vector2(0f, 1f);
            ruleRect.anchoredPosition = new Vector2(0f, 2f);
            var line = rule.GetComponent<Image>();
            line.color = new Color(230f / 255f, 213f / 255f, 184f / 255f, 1f);
            line.raycastTarget = false;
            return date;
        }

        static string FormatAlbumCard(AlbumCardDto pin)
        {
            if (pin == null)
                return string.Empty;
            var rare = pin.quality == "blue" || pin.quality == "purple" || pin.quality == "red" ||
                       pin.quality == "orange" || pin.quality == "gold";
            return DesktopFishCatalog.SpeciesName(pin.speciesId) + " · " + pin.sizeM.ToString("0.00") + " m\n" +
                   (rare ? "难忘的稀有鱼获" : "值得记下的一次收获") + "\n" +
                   AlbumDate(pin) + " · " + AlbumPond(pin);
        }

        static string FormatAlbumAside(AlbumCardDto pin)
        {
            if (pin == null)
                return "每次相遇都值得留念。新的鱼获故事，会写进湖畔相册。";
            return "在" + AlbumPond(pin) + "，遇见 " + pin.sizeM.ToString("0.00") + " m 的" +
                   DesktopFishCatalog.SpeciesName(pin.speciesId) + "。\n这一天的风和水光，也一起留在了照片里。";
        }

        static string AlbumDate(AlbumCardDto pin)
        {
            return pin != null && pin.eventAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(pin.eventAt).LocalDateTime.ToString("M月d日")
                : "—";
        }

        static string AlbumPond(AlbumCardDto pin)
        {
            if (pin == null)
                return "—";
            if (!string.IsNullOrEmpty(pin.pondName))
                return pin.pondName;
            return PondLabel(pin.pondId);
        }

        static string PondLabel(string pondId)
        {
            if (string.IsNullOrEmpty(pondId))
                return "—";
            var pond = DesktopGameData.GetPond(pondId);
            return pond != null && !string.IsNullOrEmpty(pond.name) ? pond.name : pondId;
        }

        static void PlayAlbumTurn(Transform albumRoot)
        {
            RevealAlbumSlot(albumRoot != null ? albumRoot.Find("AlbumStage/SlotL") : null, new Vector2(-28f, 0f), -1.3f);
            RevealAlbumSlot(albumRoot != null ? albumRoot.Find("AlbumStage/SlotR") : null, new Vector2(28f, 0f), 1.3f);
            CozyJournalStaticPose.RevealAside(albumRoot);
        }

        static void RevealAlbumSlot(Transform slot, Vector2 offset, float angle)
        {
            if (slot == null || !Application.isPlaying)
                return;
            var pose = slot.GetComponent<CozyJournalStaticPose>();
            if (pose == null)
            {
                pose = slot.gameObject.AddComponent<CozyJournalStaticPose>();
                pose.Configure(angle, slot.GetComponentsInChildren<Graphic>(true));
            }
            pose.PlayReveal(offset, .28f, .25f);
        }

        void ReplayCenter(Transform root)
        {
            if (_skipRenderMotion || root == null || !Application.isPlaying)
                return;
            foreach (var sticker in root.GetComponentsInChildren<CozyStickerMotion>(true))
                sticker.RestartEntrance();
        }

        void RenderAchievements()
        {
            if (_achievementsList == null)
                return;
            if (_title != null)
                _title.text = IsAchievementsPage() ? "成就" : "个人中心";
            ClearChildren(_achievementsList);
            var list = _hub.achievements ?? Array.Empty<AchievementViewDto>();
            var visible = 0;
            foreach (var achievement in list)
            {
                if (achievement == null || !MatchesAchievementCategory(achievement.category)) continue;
                if ((_achievementFilter == 1 && !achievement.unlocked) || (_achievementFilter == 2 && achievement.unlocked))
                    continue;
                visible++;
                var title = achievement.name;
                var desc = achievement.unlocked || !achievement.isHidden ? achievement.desc : "隐藏成就";
                var color = achievement.unlocked ? new Color(1f, 0.91f, 0.72f, 1f) : CatTheme.Cream;
                CreateAchievementRow(_achievementsList, achievement.achievementId, title + "\n" + desc, color, achievement.unlocked, achievement.iconKey);
            }
            var completed = 0;
            foreach (var achievement in list)
                if (achievement != null && achievement.unlocked) completed++;
            CozyItemArt.Text(transform, "DesignAchievementSummary/Value", completed + " / " + list.Length);
            CozyItemArt.Text(transform, "Shell/Content/Tab_成就/JournalAchievementSummary/Value", completed + " / " + list.Length);
            foreach (var filterName in new[] { "All", "Completed", "Pending" })
            {
                var filter = filterName == "All" ? 0 : filterName == "Completed" ? 1 : 2;
                transform.Find("Shell/Content/Tab_成就/DesignFilter" + filterName)?.GetComponent<CozyButtonVisual>()
                    ?.SetSelected(filter == _achievementFilter);
            }
            foreach (var category in new[] { "All", "Fishing", "Collection", "Progress" })
                transform.Find("DesignAchievementCategories/" + category)?.GetComponent<CozyButtonVisual>()
                    ?.SetSelected(category == _achievementCategory);
            if (visible == 0)
                CreateAchievementRow(_achievementsList, "empty", "当前分类暂无成就",
                    CatTheme.Cream);
            ReplayCenter(_achievementsList);
            if (!_skipRenderMotion)
                CozyJournalStaticPose.RevealAside(transform.Find("Shell/Content/Tab_成就") ?? transform.Find("DesignAchievementSummary"));
        }

        bool MatchesAchievementCategory(string category)
        {
            switch (_achievementCategory)
            {
                case "Fishing": return category == "catch" || category == "fishing";
                case "Collection": return category == "album" || category == "codex" || category == "collection";
                case "Progress": return category == "progress";
                default: return true;
            }
        }

        IEnumerator PinRoutine(string candidateId)
        {
            var done = false;
            var ok = false;
            string err = null;
            yield return _api.ChangeAlbumPin("pin", candidateId, (success, _, message) =>
            {
                ok = success;
                err = message;
                done = true;
            });
            while (!done) yield return null;
            if (!ok)
                SetStatus(err ?? "钉选失败");
            else
                Refresh();
        }

        IEnumerator UnpinRoutine(string pinId)
        {
            var done = false;
            var ok = false;
            string err = null;
            yield return _api.ChangeAlbumPin("unpin", pinId, (success, _, message) =>
            {
                ok = success;
                err = message;
                done = true;
            });
            while (!done) yield return null;
            if (!ok)
                SetStatus(err ?? "取消钉选失败");
            else
                Refresh();
        }

        static string FormatCard(AlbumCardDto c)
        {
            if (c == null) return "-";
            var name = DesktopFishCatalog.SpeciesName(c.speciesId);
            var pond = string.IsNullOrEmpty(c.pondName) ? (c.pondId ?? "") : c.pondName;
            return name + "\n" + DesktopFishCatalog.QualityName(c.quality) + " · " +
                   c.sizeM.ToString("0.00") + "m\n" +
                   pond + " · " + SourceLabel(c.source);
        }

        static string SourceLabel(string source)
        {
            switch (source)
            {
                case "return": return "回鱼";
                case "first_codex": return "首次图鉴";
                default: return "捕获";
            }
        }

        static string TabApiName(HubTab tab)
        {
            switch (tab)
            {
                case HubTab.Showcase: return "showcase";
                case HubTab.Codex: return "codex";
                case HubTab.Album: return "album";
                case HubTab.Achievements: return "achievements";
                default: return "profile";
            }
        }

        void SetStatus(string msg)
        {
            if (_status != null)
                _status.text = msg ?? string.Empty;
        }

        void StopLoad()
        {
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }
        }

        Text FindText(string path)
        {
            var t = transform.Find(path);
            return t != null ? t.GetComponent<Text>() : null;
        }

        Button FindButton(string path)
        {
            var t = transform.Find(path);
            return t != null ? t.GetComponent<Button>() : null;
        }

        static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        static GameObject CreatePanel(
            string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            CatTheme.ApplyPaper(go.GetComponent<Image>());
            return go;
        }

        static Text CreateLabel(
            Transform parent, string name, string text, int size,
            TextAnchor anchor, Vector2 anchored, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = CatTheme.Font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.alignment = anchor;
            t.color = CatTheme.Ink;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            return t;
        }

        static Button CreateHeaderButton(
            Transform parent, string name, string label,
            Vector2 anchored, Vector2 size, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = CatTheme.Cream;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var text = CreateLabel(go.transform, "Label", label, 16, TextAnchor.MiddleCenter,
                Vector2.zero, size);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            CatTheme.StyleButton(btn);
            return btn;
        }

        static Button CreateNavTab(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("Tab_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 40;
            go.GetComponent<Image>().color = CatTheme.Cream;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var text = CreateLabel(go.transform, "Label", label, 15, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(100, 36));
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            CatTheme.StyleButton(btn);
            return btn;
        }

        static Transform CreateScrollGrid(
            Transform parent, string name, int columns,
            float anchorMinY, float anchorMaxY,
            float anchorMinX, float anchorMaxX,
            Vector2 cellSize)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(anchorMinX, anchorMinY);
            srt.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
            srt.offsetMin = new Vector2(8, 8);
            srt.offsetMax = new Vector2(-8, -36);
            scrollGo.GetComponent<Image>().color = CatTheme.Cream;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());

            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            Stretch(crt);
            crt.pivot = new Vector2(0, 1);
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.padding = new RectOffset(8, 8, 8, 8);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            return content.transform;
        }

        static Transform CreateScrollList(
            Transform parent, string name,
            float anchorMinY, float anchorMaxY)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, anchorMinY);
            srt.anchorMax = new Vector2(1, anchorMaxY);
            srt.offsetMin = new Vector2(12, 12);
            srt.offsetMax = new Vector2(-12, -12);
            scrollGo.GetComponent<Image>().color = CatTheme.Cream;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            Stretch(crt);
            crt.pivot = new Vector2(0, 1);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            return content.transform;
        }

        static void CreateCard(
            Transform parent, string name, string label, Action onClick,
            Color? tint = null, int fontSize = 12, Sprite art = null, bool silhouette = false, string metadata = null, bool collected = false, int slot = 0)
        {
            var scroll = parent != null ? parent.GetComponentInParent<ScrollRect>() : null;
            var template = scroll != null ? scroll.transform.parent.Find("JournalCardTemplate") : null;
            var go = template != null ? Instantiate(template.gameObject, parent, false) : DesktopUiPrefabFactory.Instantiate("ShowcaseSlot", parent);
            if (go != null) go.SetActive(true);
            if (go == null)
                return;
            go.name = name;
            var text = DesktopModalUi.FindComponent<Text>(go.transform, "Label");
            var button = go.GetComponent<Button>();
            if (text == null || button == null)
            {
                Debug.LogError("[DesktopUI] ShowcaseSlot requires root Button and Label Text.", go);
                go.SetActive(false);
                UnityEngine.Object.Destroy(go);
                return;
            }
            text.text = label;
            var collection = go.GetComponent<CozyProfileCollectionCard>();
            if (collection != null) collection.Bind(label, metadata, art, silhouette, collected, slot);
            var artImage = DesktopModalUi.FindComponent<Image>(go.transform, "DesignItemArt");
            if (artImage != null)
            {
                CozyItemArt.Set(go.transform, "DesignItemArt", art);
                if (art != null && silhouette)
                    artImage.color = new Color(CatTheme.Ink.r, CatTheme.Ink.g, CatTheme.Ink.b, 0.22f);
                if (template == null)
                {
                    var anchorMax = text.rectTransform.anchorMax;
                    anchorMax.y = art != null ? 0.42f : 1f;
                    text.rectTransform.anchorMax = anchorMax;
                }
                var stamp = go.transform.Find("JournalStamp");
                if (stamp != null) stamp.gameObject.SetActive(collected || (!silhouette && label != null && label.StartsWith("✓")));
            }
            button.onClick.RemoveAllListeners();
            button.interactable = onClick != null;
            if (onClick != null)
                button.onClick.AddListener(() =>
                {
                    foreach(Transform sibling in parent) sibling.GetComponent<CozyButtonVisual>()?.SetSelected(sibling==go.transform);
                    onClick();
                    var tab=parent;
                    while(tab.parent!=null && !tab.name.StartsWith("Tab_")) tab=tab.parent;
                    tab.Find("JournalCollectionPhoto")?.GetComponent<CozyJournalStaticPose>()?.PlayReveal(new Vector2(24f, 0f), .26f, .3f);
                });
            DesktopModalUi.StyleTree(go.transform);
            var scope = parent.parent != null ? parent.parent.name : parent.name;
            var motion = CozyStickerMotion.Bind(go, "profile:" + scope + ":" + name,
                Mathf.Max(0, parent.childCount - 1), scroll != null ? scroll.viewport : null, art != null ? art.name : name);
            if (!Application.isPlaying && motion != null) motion.ApplyFinalPose();
        }

        static void CreateAchievementRow(Transform parent, string name, string label, Color tint, bool unlocked = false, string iconKey = null)
        {
            var go = DesktopUiPrefabFactory.Instantiate("AchievementRow", parent);
            if (go == null)
                return;
            go.name = name;
            var text = DesktopModalUi.FindComponent<Text>(go.transform, "Label");
            if (text == null)
            {
                Debug.LogError("[DesktopUI] AchievementRow requires Label Text.", go);
                go.SetActive(false);
                UnityEngine.Object.Destroy(go);
                return;
            }
            var description = go.transform.Find("JournalDescription")?.GetComponent<Text>();
            if (description != null)
            {
                var splitAt = label.IndexOf('\n');
                text.supportRichText = false;
                text.text = splitAt >= 0 ? label.Substring(0, splitAt) : label;
                description.text = splitAt >= 0 ? label.Substring(splitAt + 1) : "";
                CozyItemArt.Text(go.transform, "DesignState", unlocked ? "已解锁" : "");
                CozyItemArt.Text(go.transform, "DesignStateBadge/Label", "已解锁");
                var stamp = go.transform.Find("DesignStateBadge");
                if (stamp != null) stamp.gameObject.SetActive(unlocked);
                var medal = go.transform.Find("Reflow_AchievementSeal")?.GetComponent<Image>();
                if (medal != null)
                {
                    var icon = string.IsNullOrEmpty(iconKey) ? "medal" : iconKey;
                    switch(icon)
                    {
                        case "ach_first_catch": icon="hook"; break;
                        case "ach_codex_5": case "ach_codex_20": icon="map"; break;
                        case "ach_return_1": icon="fish"; break;
                        case "ach_big_08": icon="star"; break;
                        case "ach_album_3": icon="heart"; break;
                    }
                    var sprite = CatTheme.LoadCozy("icons/" + icon + ".png", Vector4.zero) ?? CatTheme.LoadCozy("icons/medal.png", Vector4.zero);
                    medal.sprite = sprite; medal.color = new Color(1,1,1,unlocked?1:.5f);
                    medal.material = unlocked ? null : Resources.Load<Material>(CozyJournalTheme.ResourceRoot + "materials/Grayscale");
                }
                var medallion = go.transform.Find("JournalMedal")?.GetComponent<CozyJournalMedalGraphic>();
                if(medallion!=null)
                {
                    medallion.color = new Color(1,1,1,unlocked?1:.5f);
                    medallion.material = unlocked ? null : Resources.Load<Material>(CozyJournalTheme.ResourceRoot + "materials/Grayscale");
                }
                DesktopModalUi.StyleTree(go.transform);
                var scroll = parent.GetComponentInParent<ScrollRect>();
                var motion = CozyStickerMotion.Bind(go, "achievement:" + name, Mathf.Max(0, parent.childCount - 1),
                    scroll != null ? scroll.viewport : null, name, null, false, true);
                if (!Application.isPlaying) motion.ApplyFinalPose();
                return;
            }
            text.text = label;
            var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            CozyItemArt.Text(go.transform, "DesignState", name == "empty" ? "" : unlocked ? "已完成" : "未完成");
            var state = DesktopModalUi.FindComponent<Text>(go.transform, "DesignState");
            if (state != null)
                state.color = unlocked ? CozyItemArt.CompletedText : CatTheme.Ink;
            var badge = DesktopModalUi.FindComponent<Image>(go.transform, "DesignStateBadge");
            if (badge != null)
            {
                badge.gameObject.SetActive(name != "empty");
                badge.color = unlocked ? CozyItemArt.CompletedBadge : CozyItemArt.PendingBadge;
            }
            var seal = DesktopModalUi.FindComponent<Image>(go.transform, "Reflow_AchievementSeal");
            if (seal != null)
                seal.color = new Color(1f, 1f, 1f, unlocked || name == "empty" ? 1f : 0.45f);
            var split = label.IndexOf('\n');
            if (split > 0 && badge != null)
                text.text = "<b><size=23>" + label.Substring(0, split) + "</size></b>" + label.Substring(split);
            layout.minHeight = 100f;
            layout.preferredHeight = 100f;
            layout.flexibleHeight = 0f;
            text.fontSize = 20;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            DesktopModalUi.StyleTree(go.transform);
            var listScroll = parent.GetComponentInParent<ScrollRect>();
            var rowMotion = CozyStickerMotion.Bind(go, "achievement:" + name, Mathf.Max(0, parent.childCount - 1),
                listScroll != null ? listScroll.viewport : null, name, null, false, true);
            if (!Application.isPlaying && rowMotion != null) rowMotion.ApplyFinalPose();
        }

        static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
