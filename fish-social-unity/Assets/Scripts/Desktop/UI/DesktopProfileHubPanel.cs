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
        Action<string> _toast;

        HubTab _tab = HubTab.Profile;
        string _viewingOtherPlayerId;
        ProfileHubDto _hub;
        Coroutine _loadRoutine;
        Coroutine _avatarRoutine;

        Text _title;
        Text _status;
        Transform _sidebar;
        readonly List<Button> _tabButtons = new List<Button>();
        readonly List<GameObject> _tabContents = new List<GameObject>();

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
        Button _editButton;
        Button _retryButton;
        FishCodexEntryDto[] _codexEntries = Array.Empty<FishCodexEntryDto>();
        int _albumPinsPage;
        Coroutine _albumPhotoRoutine;
        int _albumPhotoLoadVersion;

        public void Bind(
            IAuthenticatedApiClient api,
            SocialPondSessionController pond,
            Action openEdit,
            Action<string> toast = null)
        {
            _api = api;
            _pond = pond;
            _openEdit = openEdit;
            _toast = toast;
            EnsureUi();
        }

        public void ShowSelf(HubTab tab = HubTab.Profile)
        {
            _viewingOtherPlayerId = null;
            _tab = tab;
            if (isActiveAndEnabled)
                Refresh();
        }

        public void ShowOtherPlayer(string playerId, HubTab tab = HubTab.Profile)
        {
            _viewingOtherPlayerId = playerId ?? string.Empty;
            _tab = tab;
            if (isActiveAndEnabled)
                Refresh();
        }

        public void OnOpened()
        {
            if (_pond != null)
            {
                _pond.CodexUnlocked += OnCodexUnlocked;
                _pond.AchievementUnlocked += OnAchievementUnlocked;
            }
            Refresh();
        }

        public void OnClosed()
        {
            StopLoad();
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
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
#endif
                    Destroy(child);
            }
            _tabButtons.Clear();
            _tabContents.Clear();
            BuildShellHierarchy();
        }

        void EnsureUi()
        {
            if (transform.Find("Shell") != null)
            {
                UpgradeAlbumTabIfNeeded();
                EnsureMaskedScrolls();
                WireFromPrefab();
                return;
            }

            Debug.LogWarning(
                "[DesktopUI] PanelProfileHub Prefab 缺少 Shell 层级，使用运行时 fallback。" +
                "请执行 Fish Social → Bake PanelProfileHub。");
            BuildShellHierarchy();
        }

        /// <summary>
        /// 旧 Prefab 仍有候选栏/单卡相册时，就地升级为左右双卡满铺分页。
        /// </summary>
        void UpgradeAlbumTabIfNeeded()
        {
            var root = transform.Find("Shell/Content/Tab_相册");
            if (root == null)
                return;

            var stage = root.Find("AlbumStage");
            var needsRebuild = stage == null ||
                               stage.Find("SlotL") == null ||
                               stage.Find("SlotR") == null ||
                               stage.Find("SlotL/Photo") == null ||
                               stage.Find("SlotR/Photo") == null;
            if (needsRebuild)
            {
                for (var i = root.childCount - 1; i >= 0; i--)
                    Destroy(root.GetChild(i).gameObject);
                BuildAlbumTab(root);
                return;
            }

            DestroyNamedChild(root, "Candidates");
            DestroyNamedChild(root, "CandTitle");
            DestroyNamedChild(root, "CandPage");
            DestroyNamedChild(root, "Pins");
            DestroyNamedChild(root, "PinsTitle");
            DestroyNamedChild(root, "PinsPage");
            DestroyNamedChild(root, "Prev_0.5");
            DestroyNamedChild(root, "Next_0.5");
        }

        static void DestroyNamedChild(Transform parent, string name)
        {
            if (parent == null) return;
            var child = parent.Find(name);
            if (child != null)
                Destroy(child.gameObject);
        }

        void EnsureMaskedScrolls()
        {
            EnsureMaskedScroll(transform.Find("Shell/Content/Tab_展示柜/ShowcaseGrid"));
            EnsureMaskedScroll(transform.Find("Shell/Content/Tab_图鉴/CodexGrid"));
            EnsureMaskedScroll(transform.Find("Shell/Content/Tab_成就/Achievements"));
        }

        static void EnsureMaskedScroll(Transform scrollRoot)
        {
            if (scrollRoot == null)
                return;
            var scroll = scrollRoot.GetComponent<ScrollRect>();
            if (scroll == null)
                return;

            var viewportTf = scrollRoot.Find("Viewport");
            if (viewportTf == null)
            {
                var content = scrollRoot.Find("Content");
                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                viewportGo.transform.SetParent(scrollRoot, false);
                Stretch(viewportGo.GetComponent<RectTransform>());
                if (content != null)
                    content.SetParent(viewportGo.transform, false);
                scroll.viewport = viewportGo.GetComponent<RectTransform>();
                if (content != null)
                    scroll.content = content as RectTransform;
            }
            else
            {
                if (viewportTf.GetComponent<RectMask2D>() == null)
                    viewportTf.gameObject.AddComponent<RectMask2D>();
                scroll.viewport = viewportTf as RectTransform;
                var content = viewportTf.Find("Content") ?? scrollRoot.Find("Content");
                if (content != null)
                    scroll.content = content as RectTransform;
            }

            scroll.horizontal = false;
            scroll.vertical = true;
        }

        void WireFromPrefab()
        {
            _title = FindText("Shell/Header/Title");
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
            FixAlbumPagerLayout();
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
            _albumSlotLBody = FindTextUnder(slotL, "Body");
            _albumSlotRBody = FindTextUnder(slotR, "Body");
            _albumPinsPageLabel = FindTextUnder(root, "Page") ?? FindTextUnder(root, "PinsPage");
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
        }

        void RebindAlbumPagers()
        {
            var root = transform.Find("Shell/Content/Tab_相册");
            if (root == null) return;
            Action prev = () =>
            {
                _albumPinsPage = Math.Max(0, _albumPinsPage - 1);
                RenderAlbum();
            };
            Action next = () =>
            {
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
            BuildShellHierarchy();
        }

        void BuildShellHierarchy()
        {
            var bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color = new Color(0.09f, 0.12f, 0.16f, 1f);

            var shell = CreatePanel("Shell", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.09f, 0.12f, 0.16f, 1f));

            var header = CreatePanel("Header", shell.transform,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -52), Vector2.zero,
                new Color(0.12f, 0.18f, 0.24f, 1f));
            _title = CreateLabel(header.transform, "Title", "个人中心", 20,
                TextAnchor.MiddleLeft, new Vector2(16, 0), new Vector2(280, 36));
            _editButton = CreateHeaderButton(header.transform, "Edit", "编辑资料",
                new Vector2(-200, 0), new Vector2(100, 32), () => _openEdit?.Invoke());
            _retryButton = CreateHeaderButton(header.transform, "Retry", "重试",
                new Vector2(-90, 0), new Vector2(72, 32), Refresh);

            _sidebar = CreatePanel("Sidebar", shell.transform,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(128, -52),
                new Color(0.11f, 0.15f, 0.2f, 1f)).transform;
            var sideLayout = _sidebar.gameObject.AddComponent<VerticalLayoutGroup>();
            sideLayout.padding = new RectOffset(8, 8, 12, 12);
            sideLayout.spacing = 6;
            sideLayout.childForceExpandWidth = true;
            sideLayout.childForceExpandHeight = false;
            sideLayout.childControlHeight = true;
            sideLayout.childControlWidth = true;

            var contentHost = CreatePanel("Content", shell.transform,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(128, 0), new Vector2(0, -52),
                new Color(0.1f, 0.13f, 0.17f, 1f));

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
                    new Color(0.1f, 0.13f, 0.17f, 1f));
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
            _profileAvatar.color = new Color(0.22f, 0.36f, 0.42f, 1f);
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
            _albumStage.color = new Color(0.06f, 0.08f, 0.1f, 1f);

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
            image.color = new Color(0.1f, 0.14f, 0.16f, 1f);

            var photoGo = new GameObject("Photo", typeof(RectTransform), typeof(Image));
            photoGo.transform.SetParent(go.transform, false);
            Stretch(photoGo.GetComponent<RectTransform>());
            photo = photoGo.GetComponent<Image>();
            photo.color = new Color(0.08f, 0.11f, 0.14f, 1f);
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
            scrub.color = new Color(0.04f, 0.06f, 0.08f, 0.72f);
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
                    _albumPinsPage = Math.Max(0, _albumPinsPage - 1);
                    RenderAlbum();
                });
            var nextBtn = CreateHeaderButton(parent, "Next", "下一页",
                Vector2.zero, new Vector2(72, 28),
                () =>
                {
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
            _tab = tab;
            ApplyTabVisibility();
            if (_hub == null)
                Refresh();
            else if (tab == HubTab.Codex && _hub.isSelf && (_codexEntries == null || _codexEntries.Length == 0))
                StartCoroutine(ReloadCodexThenRender());
            else
                RenderCurrentTab();
        }

        IEnumerator ReloadCodexThenRender()
        {
            yield return LoadCodexRoutine();
            RenderCurrentTab();
        }

        void ApplyTabVisibility()
        {
            for (var i = 0; i < _tabContents.Count; i++)
            {
                if (_tabContents[i] != null)
                    _tabContents[i].SetActive(i == (int)_tab);
                if (i < _tabButtons.Count && _tabButtons[i] != null)
                {
                    var img = _tabButtons[i].GetComponent<Image>();
                    if (img != null)
                        img.color = i == (int)_tab
                            ? new Color(0.2f, 0.45f, 0.55f, 1f)
                            : new Color(0.14f, 0.2f, 0.26f, 1f);
                }
            }
        }

        void Refresh()
        {
            if (!isActiveAndEnabled)
                return;
            StopLoad();
            _loadRoutine = StartCoroutine(LoadRoutine());
        }

        IEnumerator LoadRoutine()
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前没有有效会话，请重新登录。");
                yield break;
            }

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
                SetStatus(error ?? "个人中心加载失败。");
                _loadRoutine = null;
                yield break;
            }

            _hub = hub;
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
            yield return _api.GetCodex((ok, entries, _) =>
            {
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

        void RenderAll()
        {
            ApplyTabVisibility();
            RenderCurrentTab();
        }

        void RenderCurrentTab()
        {
            if (_hub == null)
                return;
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
            if (_avatarRoutine != null)
                StopCoroutine(_avatarRoutine);
            _avatarRoutine = StartCoroutine(LoadAvatarRoutine(p.avatarUrl, p.nickname));
        }

        IEnumerator LoadAvatarRoutine(string avatarUrl, string nickname)
        {
            if (_profileAvatar == null)
                yield break;
            _profileAvatar.color = new Color(0.22f, 0.36f, 0.42f, 1f);
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

            var unlocked = 0;
            for (var i = 0; i < DesktopFishCatalog.Species.Length; i++)
            {
                var species = DesktopFishCatalog.Species[i];
                var entry = FindCodexEntry(species.Id);
                var got = entry != null && entry.totalCaught > 0;
                if (got) unlocked++;
                var captured = species;
                var capturedEntry = entry;
                CreateCard(_codexGrid, species.Id,
                    got ? "✓ " + species.Name + "\n×" + entry.totalCaught : "？？？",
                    () => ShowCodexDetail(captured, capturedEntry),
                    got ? new Color(0.18f, 0.35f, 0.28f, 1f) : new Color(0.2f, 0.22f, 0.26f, 1f),
                    15);
            }
            if (_codexDetail != null)
                _codexDetail.text = "已解锁 " + unlocked + " / " + DesktopFishCatalog.Species.Length +
                                   "\n\n点击左侧格子查看物种资料与捕获记录。";
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
            if (!got)
            {
                _codexDetail.text = "未解锁\n捕获后显示食性、咬钩、脱钩、推荐鱼饵与捕获记录。";
                return;
            }
            var first = entry.firstCaughtAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(entry.firstCaughtAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "—";
            var last = entry.lastCaughtAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(entry.lastCaughtAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "—";
            _codexDetail.text =
                species.Name + "（" + species.Id + "）\n" +
                "食性：" + species.DietLabel + "\n" +
                "基础咬钩：" + DesktopFishCatalog.FormatBiteRate(species) + "\n" +
                "脱钩率：" + (species.BaseEscapeRate * 100f).ToString("0.0") + "%\n" +
                "推荐鱼饵：" + DesktopFishCatalog.TopBaits(species) + "\n\n" +
                "—— 捕获记录 ——\n" +
                "累计捕获：" + entry.totalCaught + "\n" +
                "最大体型：" + entry.maxSizeM.ToString("0.00") + "m\n" +
                "首次捕获：" + first + "\n" +
                "最近捕获：" + last;
        }

        void RenderShowcase()
        {
            if (_showcaseGrid == null)
                return;
            ClearChildren(_showcaseGrid);
            var fish = _hub.showcaseFish ?? Array.Empty<FishInventoryItemDto>();
            for (var i = 0; i < 8; i++)
            {
                var item = i < fish.Length ? fish[i] : null;
                var slot = i + 1;
                if (item == null)
                {
                    CreateCard(_showcaseGrid, "Slot" + i, "空位 " + slot, () =>
                    {
                        if (_showcaseDetail != null)
                            _showcaseDetail.text = "格子 " + slot + " 为空。\n可在「编辑资料」中装备背包鱼。";
                    }, null, 16);
                    continue;
                }
                var captured = item;
                var name = DesktopFishCatalog.SpeciesName(item.speciesId);
                CreateCard(_showcaseGrid, "Slot" + i,
                    name + "\n" + DesktopFishCatalog.QualityName(item.quality) +
                    "\n" + item.sizeM.ToString("0.00") + "m",
                    () => ShowFishDetail(_showcaseDetail, "展示柜格子 " + slot, captured),
                    new Color(0.18f, 0.3f, 0.38f, 1f), 15);
            }
            if (_showcaseDetail != null)
                _showcaseDetail.text = "点击左侧格子查看鱼获资料。";
        }

        void ShowFishDetail(Text target, string title, FishInventoryItemDto item)
        {
            if (target == null || item == null)
                return;
            var species = DesktopFishCatalog.GetSpecies(item.speciesId);
            var caught = item.caughtAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(item.caughtAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "—";
            target.text =
                title + "\n\n" +
                (species != null ? species.Name : item.speciesId) + "\n" +
                "品质：" + DesktopFishCatalog.QualityName(item.quality) + "\n" +
                "体长：" + item.sizeM.ToString("0.00") + "m\n" +
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
            var emptyTint = new Color(0.1f, 0.14f, 0.16f, 1f);
            var filledTint = new Color(0.12f, 0.16f, 0.18f, 1f);

            if (pins.Length == 0)
            {
                _albumPinsPage = 0;
                if (_albumStage != null)
                    _albumStage.color = new Color(0.06f, 0.08f, 0.1f, 1f);
                if (_albumSlotL != null) _albumSlotL.color = emptyTint;
                if (_albumSlotR != null) _albumSlotR.color = emptyTint;
                ClearAlbumPhoto(_albumSlotLPhoto);
                ClearAlbumPhoto(_albumSlotRPhoto);
                _albumSlotLBody.text = "相册为空\n稀有 / 大鱼 / 回鱼 / 新图鉴会自动记入";
                _albumSlotRBody.text = string.Empty;
                if (_albumPinsPageLabel != null)
                    _albumPinsPageLabel.text = "0/0";
                return;
            }

            var pageCount = Math.Max(1, (pins.Length + AlbumPinsPerPage - 1) / AlbumPinsPerPage);
            _albumPinsPage = Mathf.Clamp(_albumPinsPage, 0, pageCount - 1);
            var start = _albumPinsPage * AlbumPinsPerPage;
            var left = start < pins.Length ? pins[start] : null;
            var right = start + 1 < pins.Length ? pins[start + 1] : null;

            if (_albumSlotL != null)
                _albumSlotL.color = left != null ? filledTint : emptyTint;
            if (_albumSlotR != null)
                _albumSlotR.color = right != null ? filledTint : emptyTint;
            _albumSlotLBody.text = left != null ? FormatAlbumPage(left) : string.Empty;
            _albumSlotRBody.text = right != null ? FormatAlbumPage(right) : string.Empty;

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

            target.color = new Color(0.08f, 0.11f, 0.14f, 1f);
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
            target.color = new Color(0.08f, 0.11f, 0.14f, 1f);
        }

        static string FormatAlbumPage(AlbumCardDto pin)
        {
            if (pin == null)
                return "—";
            var name = DesktopFishCatalog.SpeciesName(pin.speciesId);
            var quality = DesktopFishCatalog.QualityName(pin.quality);
            var when = pin.eventAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(pin.eventAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "—";
            var pond = string.IsNullOrEmpty(pin.pondName)
                ? (string.IsNullOrEmpty(pin.pondId) ? "—" : pin.pondId)
                : pin.pondName;
            return name + "\n" +
                   quality + "  ·  " + pin.sizeM.ToString("0.00") + "m\n\n" +
                   "来源塘：" + pond + "\n" +
                   "记录：" + when + "\n" +
                   SourceLabel(pin.source);
        }

        void RenderAchievements()
        {
            if (_achievementsList == null)
                return;
            ClearChildren(_achievementsList);
            var list = _hub.achievements ?? Array.Empty<AchievementViewDto>();
            foreach (var a in list)
            {
                var title = (a.unlocked ? "★ " : "○ ") + a.name;
                var desc = a.unlocked || !a.isHidden ? a.desc : "隐藏成就";
                var color = a.unlocked
                    ? new Color(0.85f, 0.75f, 0.35f, 1f)
                    : new Color(0.45f, 0.48f, 0.52f, 1f);
                CreateAchievementRow(_achievementsList, a.achievementId, title + "\n" + desc, color);
            }
            if (list.Length == 0)
                CreateAchievementRow(_achievementsList, "empty", "暂无成就数据",
                    new Color(0.3f, 0.32f, 0.36f, 1f));
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
                Destroy(parent.GetChild(i).gameObject);
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
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.alignment = anchor;
            t.color = new Color(0.9f, 0.93f, 0.96f, 1f);
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
            go.GetComponent<Image>().color = new Color(0.18f, 0.32f, 0.4f, 1f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var text = CreateLabel(go.transform, "Label", label, 13, TextAnchor.MiddleCenter,
                Vector2.zero, size);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        static Button CreateNavTab(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("Tab_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 40;
            go.GetComponent<Image>().color = new Color(0.14f, 0.2f, 0.26f, 1f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var text = CreateLabel(go.transform, "Label", label, 15, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(100, 36));
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
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
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.6f);

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
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.45f);

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
            Color? tint = null, int fontSize = 12)
        {
            GameObject go = null;
            var slot = DesktopUiPrefabFactory.Instantiate("ShowcaseSlot", parent);
            if (slot != null)
            {
                go = slot;
                go.name = name;
                var layout = go.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.preferredWidth = 200f;
                    layout.preferredHeight = 110f;
                    layout.minWidth = 160f;
                    layout.minHeight = 88f;
                }
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
            }

            var image = go.GetComponent<Image>();
            if (image != null)
                image.color = tint ?? new Color(0.16f, 0.22f, 0.28f, 1f);
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                if (onClick != null)
                {
                    btn.interactable = true;
                    btn.onClick.AddListener(() => onClick());
                }
                else
                    btn.interactable = false;
            }

            var labelTf = go.transform.Find("Label");
            Text text = labelTf != null ? labelTf.GetComponent<Text>() : null;
            if (text == null)
            {
                text = CreateLabel(go.transform, "Label", label, fontSize, TextAnchor.UpperLeft,
                    new Vector2(6, -4), new Vector2(148, 64));
                Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(10, 8);
                text.rectTransform.offsetMax = new Vector2(-10, -8);
            }
            else
            {
                text.text = label;
                text.fontSize = fontSize;
                text.alignment = TextAnchor.UpperLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        static void CreateAchievementRow(Transform parent, string name, string label, Color tint)
        {
            GameObject go = null;
            var row = DesktopUiPrefabFactory.Instantiate("AchievementRow", parent);
            if (row != null)
            {
                go = row;
                go.name = name;
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                var le = go.GetComponent<LayoutElement>();
                le.preferredHeight = 72;
                le.minHeight = 72;
                CreateLabel(go.transform, "Label", label, 18, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.zero);
            }

            var image = go.GetComponent<Image>();
            if (image != null)
                image.color = tint;
            var textTf = go.transform.Find("Label");
            var text = textTf != null ? textTf.GetComponent<Text>() : null;
            if (text != null)
            {
                text.text = label;
                text.fontSize = 18;
                text.alignment = TextAnchor.MiddleLeft;
                Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(16, 8);
                text.rectTransform.offsetMax = new Vector2(-16, -8);
            }
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
