using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Server-authoritative public/friends feed. The page owns no pond
    /// lifecycle and can be opened while the player remains in the pond.
    /// </summary>
    public sealed class DesktopSocialFeedPanel : MonoBehaviour
    {
        bool _uiReady;
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        RectTransform _content;
        ScrollRect _scroll;
        Text _status;
        Button _publicTab;
        Button _friendsTab;
        Button _myTab;
        bool _mineOnly;
        bool _loadedMineOnly;
        bool _friendsOnly;
        Coroutine _loadRoutine;
        int _loadGeneration;
        RectTransform _commentsHost;
        DesktopSocialPostCard _selectedComments;
        Text _commentsTitle;
        DesktopSocialComposeModal _composer;
        bool _isOpen;
        bool _hasLoaded;
        bool _loadedFriendsOnly;
        bool _feedTabsStyled;
        bool _loadingMore;
        FeedSlot _publicFeed;
        FeedSlot _friendFeed;
        FeedSlot _mineFeed;
        bool _canLoadMore;
        bool _shellEntrance;
        bool _seenListTop;
        bool _suppressScrollLoad;
        int _nextOffset;
        const int InitialPageSize = 50;
        const int MorePageSize = 10;

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond)
        {
            if (_pond != null)
            {
                _pond.PostLikedReceived -= OnPostLiked;
                _pond.PostCommentedReceived -= OnPostCommented;
                _pond.PostCommentDeletedReceived -= OnPostCommentDeleted;
            }
            _api = api;
            _pond = pond;
            EnsureUi();
            if (_pond != null)
            {
                _pond.PostLikedReceived += OnPostLiked;
                _pond.PostCommentedReceived += OnPostCommented;
                _pond.PostCommentDeletedReceived += OnPostCommentDeleted;
            }
        }

        public void OnOpened()
        {
            if (!_uiReady)
                return;
            if (_isOpen)
                return;
            _isOpen = true;
            _shellEntrance = true;
            _seenListTop = false;
            var share = Find<Button>("DesignFeedInfo/Share");
            if (share != null)
            {
                share.onClick.RemoveAllListeners();
                share.onClick.AddListener(OpenComposer);
                DesktopModalUi.SetButtonLabel(share, "分享鱼获");
            }
            EnsureFeedSlots();
            var slot = CurrentFeed;
            if (slot != null && slot.loaded)
            {
                ShowFeed(slot);
                return;
            }
            Refresh();
        }

        public void OnClosed()
        {
            _isOpen = false;
            if (_categoryMotion != null) StopCoroutine(_categoryMotion);
            _categoryMotion = null;
            GetComponent<CozyJournalPageMotion>()?.Finish();
            if (_composer != null) _composer.Close(true);
            _loadGeneration++;
            if (_loadRoutine != null)
                StopCoroutine(_loadRoutine);
            _loadRoutine = null;
            _loadingMore = false;
        }

        public void BuildEditorLayout()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            if (Find<Button>("Header/Public") == null ||
                Find<Button>("Header/Friends") == null ||
                Find<RectTransform>("Scroll/Viewport/Content") == null)
            {
                ClearUiHierarchy();
                BuildFallbackUi();
            }
            EnsureUi();
#endif
        }

        void OnDestroy()
        {
            if (_composer != null) Destroy(_composer.gameObject);
            if (_pond != null)
            {
                _pond.PostLikedReceived -= OnPostLiked;
                _pond.PostCommentedReceived -= OnPostCommented;
                _pond.PostCommentDeletedReceived -= OnPostCommentDeleted;
            }
        }

        public void RefreshMyFeed()
        {
            EnsureFeedSlots();
            if (_mineFeed != null)
            {
                _mineFeed.loaded = false;
                ClearRoot(_mineFeed.root);
            }
            SelectFeed(false, true);
        }

        public void OpenComposer()
        {
            if (_composer == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null) return;
                _composer = DesktopSocialComposeModal.Create(canvas.rootCanvas.transform);
            }
            if (_composer == null) { SetStatus("发布界面资源缺失，请重新导入手账 UI。"); return; }
            _composer.Open(_api, _pond != null ? _pond.Nickname : "钓友", () =>
            {
                EnsureFeedSlots();
                if (_mineFeed != null)
                {
                    _mineFeed.loaded = false;
                    ClearRoot(_mineFeed.root);
                }
                SelectFeed(false, true);
                SetStatus("鱼获故事已发布，正在刷新我的动态…");
            }, DesktopProfileCache.Latest?.avatarUrl);
        }

        void EnsureUi()
        {
            _uiReady = false;
            _publicTab = Find<Button>("Header/Public");
            _friendsTab = Find<Button>("Header/Friends");
            _myTab = Find<Button>("Header/My");
            _status = Find<Text>("Status");
            _content = Find<RectTransform>("Scroll/Viewport/Content");
            _scroll = Find<ScrollRect>("Scroll");
            _commentsHost = Find<RectTransform>("DesignFeedInfo/CommentsHost");
            _commentsTitle = Find<Text>("DesignFeedInfo/DetailTitle");
            if (_publicTab == null || _friendsTab == null || _status == null ||
                _content == null || _scroll == null)
            {
                Debug.LogError(
                    "[DesktopUI] PanelSocialFeed Prefab 缺少必需控件。" +
                    "请在 Unity Prefab Manager 中执行“初始化”，" +
                    "运行时不会再重建或覆盖手动布局。");
                return;
            }

            _uiReady = true;
            RestyleUi();
            DesktopModalUi.BindButton(transform, "Retry", Refresh);
            var retry = Find<Button>("Retry");
            if (retry != null)
                retry.gameObject.SetActive(false);

            if (_publicTab != null)
            {
                _publicTab.onClick.RemoveAllListeners();
                _publicTab.onClick.AddListener(() => SelectFeed(false));
            }
            if (_friendsTab != null)
            {
                _friendsTab.onClick.RemoveAllListeners();
                _friendsTab.onClick.AddListener(() => SelectFeed(true));
            }
            if (_myTab != null)
            {
                _myTab.onClick.RemoveAllListeners();
                _myTab.onClick.AddListener(() => SelectFeed(false, true));
            }
            if (_scroll != null)
            {
                _scroll.onValueChanged.RemoveListener(OnScrollChanged);
                _scroll.onValueChanged.AddListener(OnScrollChanged);
            }
            if (_content != null)
            {
                var rows = _content.GetComponent<VerticalLayoutGroup>();
                if (rows != null && rows.padding.top < 18)
                    rows.padding = new RectOffset(rows.padding.left, rows.padding.right, 18, Mathf.Max(rows.padding.bottom, 12));
            }
        }

        void SelectFeed(bool friendsOnly, bool mineOnly = false)
        {
            var same = _friendsOnly == friendsOnly && _mineOnly == mineOnly;
            EnsureFeedSlots();
            if (!same)
            {
                _loadGeneration++;
                if (_loadRoutine != null)
                    StopCoroutine(_loadRoutine);
                _loadRoutine = null;
                if (_categoryMotion != null) StopCoroutine(_categoryMotion);
                _categoryMotion = null;
                _friendsOnly = friendsOnly;
                _mineOnly = mineOnly;
                _seenListTop = false;
                _suppressScrollLoad = false;
                _shellEntrance = false;
            }
            RestyleTabs();
            var slot = CurrentFeed;
            if (slot != null && slot.loaded && !same && Application.isPlaying)
            {
                _categoryMotion = StartCoroutine(RevealFeed(slot));
                return;
            }
            if (slot != null)
                ShowFeed(slot);
            if (slot != null && slot.loaded)
                return;
            if (_loadRoutine != null)
                return;
            Refresh();
        }

        IEnumerator RevealFeed(FeedSlot slot)
        {
            var motion = GetComponent<CozyJournalPageMotion>();
            if (motion != null) yield return motion.Exit();
            ShowFeed(slot);
            if (slot != null && slot.root != null)
            {
                var stickers = slot.root.GetComponentsInChildren<CozyStickerMotion>();
                for (var i = 0; i < stickers.Length; i++)
                    stickers[i].RestartEntrance();
            }
            motion?.PlayEntrance(slot != null && slot.root != null ? slot.root.childCount : 0);
            _categoryMotion = null;
        }
        Coroutine _categoryMotion;

        void RestyleUi()
        {
            DesktopModalUi.StyleTree(transform);
            RestyleTabs();
            StyleCommentColumn();
        }

        void StyleCommentColumn()
        {
            var aside = transform.Find("DesignFeedInfo");
            if (aside == null) return;
            CozyCssGraphic.EnsureWash(aside, "AsideWash",
                new Color(1f, 253f / 255f, 247f / 255f, 0.92f), 20f, 2f,
                new Color(239f / 255f, 223f / 255f, 196f / 255f, 1f));
            var summary = aside.Find("JournalSummary") as RectTransform;
            if (summary == null) return;
            var framed = summary.Find("JournalPaper");
            if (framed != null) framed.gameObject.SetActive(false);
            summary.anchorMin = summary.anchorMax = new Vector2(0f, 1f);
            summary.pivot = new Vector2(0f, 1f);
            summary.anchoredPosition = new Vector2(18f, -58f);
            summary.sizeDelta = new Vector2(326f, 82f);
            CozyCssGraphic.EnsureWash(summary, "SummaryWash",
                new Color(251f / 255f, 243f / 255f, 228f / 255f, 1f), 12f, 0f, Color.clear);
        }

        void RestyleTabs()
        {
            CozyItemArt.Text(transform, "JournalFeedHeading", _mineOnly ? "我的动态" : _friendsOnly ? "好友动态" : "公开动态");
            var tabs = new[] { _publicTab, _friendsTab, _myTab };
            var names = new[] { "公开动态", "好友动态", "我的动态" };
            var icons = new[] { "social", "user", "fish" };
            for (var index = 0; index < tabs.Length; index++)
            {
                var button = tabs[index];
                if (button == null)
                    continue;
                if (!_feedTabsStyled)
                {
                    DesktopModalUi.SetButtonLabel(button, names[index]);
                    DesktopModalUi.StyleButton(button, icons[index]);
                }
                var visual = button.GetComponent<CozyButtonVisual>();
                if (visual != null)
                    visual.SetSelected(_mineOnly ? index == 2 : index == (_friendsOnly ? 1 : 0));
            }
            _feedTabsStyled = true;
        }

        void Refresh()
        {
            if (!_uiReady)
                return;
            if (_loadRoutine != null)
                return;
            EnsureFeedSlots();
            var slot = CurrentFeed;
            if (slot != null)
            {
                slot.loaded = false;
                slot.canMore = false;
                slot.nextOffset = 0;
                ClearRoot(slot.root);
                SyncFeed(slot);
            }
            _loadingMore = false;
            var retry = Find<Button>("Retry");
            if (retry != null) retry.gameObject.SetActive(false);
            _loadRoutine = StartCoroutine(LoadRoutine(false, InitialPageSize, 0, ++_loadGeneration, _friendsOnly, _mineOnly));
        }

        void OnScrollChanged(Vector2 normalizedPosition)
        {
            if (_suppressScrollLoad)
                return;
            if (normalizedPosition.y > 0.2f)
                _seenListTop = true;
            if (!_seenListTop || normalizedPosition.y > 0.05f)
                return;
            LoadMore();
        }

        void LoadMore()
        {
            var slot = CurrentFeed;
            if (!_isOpen || slot == null || !slot.loaded || !slot.canMore ||
                _loadingMore || _loadRoutine != null)
                return;
            _loadingMore = true;
            _loadRoutine = StartCoroutine(
                LoadRoutine(true, MorePageSize, slot.nextOffset, ++_loadGeneration, _friendsOnly, _mineOnly));
        }

        IEnumerator LoadRoutine(bool append, int limit, int offset, int generation, bool friendsOnly, bool mineOnly)
        {
            var slot = FeedFor(friendsOnly, mineOnly);
            var root = slot != null ? slot.root : _content;
            var hasContent = root != null && root.childCount > 0;
            if (!append && !hasContent)
                SetStatus("正在加载动态…");
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前没有有效的 Steam 会话。");
                _loadRoutine = null;
                _loadingMore = false;
                yield break;
            }

            var done = false;
            var ok = false;
            SocialPostDto[] posts = null;
            string error = null;
            if (mineOnly)
                yield return _api.GetMySocialFeed(limit, offset, (success, items, message) =>
                {
                    ok = success;
                    posts = items;
                    error = message;
                    done = true;
                });
            else
                yield return _api.GetSocialFeed(friendsOnly, limit, offset, (success, items, message) =>
                {
                    ok = success;
                    posts = items;
                    error = message;
                    done = true;
                });
            while (!done)
                yield return null;

            if (generation != _loadGeneration)
                yield break;

            if (!ok)
            {
                GetComponent<CozyJournalPageMotion>()?.Finish();
                SetStatus(error ?? "动态加载失败，请稍后重新打开页面。");
                CreateRetry();
                _loadRoutine = null;
                _loadingMore = false;
                yield break;
            }
            if (posts == null || posts.Length == 0)
            {
                if (!append)
                {
                    ClearRoot(root);
                    GetComponent<CozyJournalPageMotion>()?.Finish();
                    SetStatus(mineOnly ? "还没有分享鱼获。" : friendsOnly ? "暂无好友动态。" : "暂无公开动态。");
                }
                RememberFeed(slot, true, false, offset);
                _loadRoutine = null;
                _loadingMore = false;
                _suppressScrollLoad = false;
                yield break;
            }

            if (!append)
            {
                ClearRoot(root);
                SetStatus(string.Empty);
            }
            for (var i = 0; i < posts.Length; i++)
            {
                if (generation != _loadGeneration) yield break;
                CreatePostCard(posts[i], root);
                if ((i + 1) % MorePageSize == 0)
                    yield return null;
            }
            RememberFeed(slot, true, posts.Length >= limit, offset + posts.Length);
            if (!append && slot == CurrentFeed)
                GetComponent<CozyJournalPageMotion>()?.PlayEntrance(posts.Length);
            _shellEntrance = false;
            if (!append && _scroll != null)
            {
                _suppressScrollLoad = true;
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 1f;
                _seenListTop = false;
            }

            _loadRoutine = null;
            _loadingMore = false;
            if (!append)
            {
                yield return null;
                if (generation == _loadGeneration)
                {
                    _suppressScrollLoad = false;
                    _seenListTop = false;
                }
            }
        }

        void CreatePostCard(SocialPostDto post, Transform root)
        {
            var card = DesktopUiPrefabFactory.Instantiate("SocialPostCard", root != null ? root : _content);
            if (card == null)
                return;
            var binder = card.GetComponent<DesktopSocialPostCard>();
            if (binder == null)
            {
                Debug.LogError("[DesktopUI] SocialPostCard 缺少 DesktopSocialPostCard 组件。");
                Destroy(card);
                return;
            }
            if (!Application.isPlaying)
                DesktopModalUi.StyleTree(card.transform);
            else
            {
                var skin = card.GetComponent<CozyPanelSkin>() ?? card.AddComponent<CozyPanelSkin>();
                skin.MarkSkinned();
                skin.RememberChildren();
            }
            binder.Bind(post, _api);
            if (_commentsHost != null) binder.ConfigureCommentsHost(_commentsHost, SelectCommentCard);
            binder.CommentCountChanged += count => { if (_selectedComments == binder) CozyItemArt.Text(transform,"DesignFeedInfo/CommentCount",count+" 条评论"); };
            if (_commentsHost != null && _selectedComments == null) binder.SelectComments();
        }

        void SelectCommentCard(DesktopSocialPostCard selectedCard)
        {
            if(selectedCard==null)return;
            var post=selectedCard.Post;
            if (_selectedComments != null && _selectedComments != selectedCard)
                _selectedComments.CloseComments();
            _selectedComments = selectedCard;
            foreach(var postCard in _content.GetComponentsInChildren<DesktopSocialPostCard>())
                postCard.GetComponent<CozyStickerMotion>()?.SetSelected(postCard==selectedCard);
            selectedCard.GetComponent<CozyStickerMotion>()?.ReplayTape();
            var empty = transform.Find("DesignFeedInfo/CommentEmpty"); if(empty != null) empty.gameObject.SetActive(false);
            if (_commentsTitle != null)
                _commentsTitle.text = transform.Find("JournalDetailParity") != null
                    ? (post.fish != null ? DesktopFishCatalog.SpeciesName(post.fish.speciesId) : "鱼获故事")
                    : (post.nickname ?? "钓友") + "的动态\n评论与留言";
            if (transform.Find("JournalDetailParity") != null)
            {
                CozyItemArt.Text(transform, "DesignFeedInfo/Heading", "给 " + (post.nickname ?? "钓友") + " 的留言");
                CozyItemArt.Set(transform, "DesignFeedInfo/JournalSummaryArt", post.fish != null ? CozyItemArt.Fish(post.fish.speciesId) : null);
            }
            var pondName = post.fish != null ? DesktopGameData.GetPond(post.fish.pondId) : null;
            var pondLabel = pondName != null && !string.IsNullOrEmpty(pondName.name) ? pondName.name : "";
            CozyItemArt.Text(transform, "DesignFeedInfo/CommentCount",
                (string.IsNullOrEmpty(pondLabel) ? "" : pondLabel + " · ") + selectedCard.CommentCount + " 条评论");
            StyleCommentColumn();
            RevealCommentAside();
            RaiseSelectedTape(selectedCard);
            var body = transform.Find("DesignFeedInfo/Body");
            if (body != null) body.gameObject.SetActive(false);
            var note = transform.Find("DesignFeedInfo/Note");
            if (note != null) note.gameObject.SetActive(false);
        }

        void RevealCommentAside()
        {
            var info = transform.Find("DesignFeedInfo");
            if (info == null) return;
            foreach (var name in new[] { "Heading", "JournalSummary", "JournalSummaryArt", "DetailTitle", "CommentCount" })
            {
                var node = info.Find(name);
                if (node == null) continue;
                var pose = node.GetComponent<CozyJournalStaticPose>() ?? node.gameObject.AddComponent<CozyJournalStaticPose>();
                pose.PlayReveal(new Vector2(24f, 0f), .26f, .3f);
            }
            var host = info.Find("CommentsHost");
            if (host == null) return;
            for (var i = 0; i < host.childCount; i++)
            {
                var child = host.GetChild(i);
                var pose = child.GetComponent<CozyJournalStaticPose>() ?? child.gameObject.AddComponent<CozyJournalStaticPose>();
                pose.PlayReveal(new Vector2(24f, 0f), .26f, .3f);
            }
        }

        static void RaiseSelectedTape(DesktopSocialPostCard selectedCard)
        {
            if (selectedCard == null) return;
            var content = selectedCard.transform.parent;
            if (content == null) return;
            for (var i = 0; i < content.childCount; i++)
            {
                var tape = content.GetChild(i).Find("JournalTape");
                if (tape == null) continue;
                // A nested canvas draws above the scroll mask, so the corner tape
                // stays on screen after the post has scrolled out of the list.
                var raycaster = tape.GetComponent<GraphicRaycaster>();
                if (raycaster != null) Destroy(raycaster);
                var overlay = tape.GetComponent<Canvas>();
                if (overlay != null) Destroy(overlay);
                if (content.GetChild(i) == selectedCard.transform)
                    tape.SetAsLastSibling();
            }
        }

        /// <summary>Use the same selection and comment renderer for a supplied snapshot without a network request.</summary>
        public void PresentSelectedComments(DesktopSocialPostCard card,PostCommentDto[] comments,int totalCount)
        {
            if(card==null)return;
            EnsureUi();
            card.ConfigureCommentsHost(_commentsHost,SelectCommentCard);
            card.PresentComments(comments,totalCount);
            CozyItemArt.Text(transform,"DesignFeedInfo/CommentCount",card.CommentCount+" 条评论");
        }

        void OnPostLiked(PostLikedDto message)
        {
            if (message == null || !_isOpen || _content == null) return;
            foreach (var card in FeedCards())
                card.ApplyLiked(message);
        }

        void OnPostCommented(PostCommentedDto message)
        {
            if (message == null || !_isOpen || _content == null) return;
            foreach (var card in FeedCards())
                card.ApplyCommented(message);
        }

        void OnPostCommentDeleted(PostCommentDeletedDto message)
        {
            if (message == null || !_isOpen || _content == null) return;
            foreach (var card in FeedCards())
                card.ApplyCommentDeleted(message);
        }

        void ClearContent()
        {
            ClearRoot(_content);
        }

        sealed class FeedSlot
        {
            public RectTransform root;
            public bool loaded;
            public bool canMore;
            public int nextOffset;
        }

        FeedSlot CurrentFeed => _mineOnly ? _mineFeed : _friendsOnly ? _friendFeed : _publicFeed;

        FeedSlot FeedFor(bool friendsOnly, bool mineOnly)
        {
            EnsureFeedSlots();
            return mineOnly ? _mineFeed : friendsOnly ? _friendFeed : _publicFeed;
        }

        void EnsureFeedSlots()
        {
            if (_content == null || _publicFeed != null)
                return;
            _publicFeed = new FeedSlot { root = _content };
            _friendFeed = new FeedSlot { root = CozyKeptPage.DuplicateList(_content, "FriendsFeed") };
            _mineFeed = new FeedSlot { root = CozyKeptPage.DuplicateList(_content, "MineFeed") };
        }

        void ShowFeed(FeedSlot slot)
        {
            SetFeedRoot(_publicFeed, slot == _publicFeed);
            SetFeedRoot(_friendFeed, slot == _friendFeed);
            SetFeedRoot(_mineFeed, slot == _mineFeed);
            SyncFeed(slot);
            if (_selectedComments != null && !_selectedComments.gameObject.activeInHierarchy)
            {
                _selectedComments.CloseComments();
                _selectedComments = null;
                var empty = transform.Find("DesignFeedInfo/CommentEmpty");
                if (empty != null) empty.gameObject.SetActive(true);
                CozyItemArt.Text(transform, "DesignFeedInfo/CommentCount", "");
                if (_commentsTitle != null) _commentsTitle.text = "选择一条动态\n在这里查看和发送评论";
            }
        }

        static void SetFeedRoot(FeedSlot slot, bool active)
        {
            if (slot == null || slot.root == null || slot.root.gameObject.activeSelf == active)
                return;
            slot.root.gameObject.SetActive(active);
        }

        void SyncFeed(FeedSlot slot)
        {
            if (slot == null)
                return;
            _content = slot.root;
            _hasLoaded = slot.loaded;
            _canLoadMore = slot.canMore;
            _nextOffset = slot.nextOffset;
            _loadedFriendsOnly = slot == _friendFeed;
            _loadedMineOnly = slot == _mineFeed;
            if (_scroll != null && slot.root != null)
                _scroll.content = slot.root;
        }

        void RememberFeed(FeedSlot slot, bool loaded, bool canMore, int nextOffset)
        {
            if (slot != null)
            {
                slot.loaded = loaded;
                slot.canMore = canMore;
                slot.nextOffset = nextOffset;
            }
            if (slot == CurrentFeed)
                SyncFeed(slot);
        }

        IEnumerable<DesktopSocialPostCard> FeedCards()
        {
            if (_publicFeed != null && _publicFeed.root != null)
                foreach (var card in _publicFeed.root.GetComponentsInChildren<DesktopSocialPostCard>(true))
                    yield return card;
            if (_friendFeed != null && _friendFeed.root != null)
                foreach (var card in _friendFeed.root.GetComponentsInChildren<DesktopSocialPostCard>(true))
                    yield return card;
            if (_mineFeed != null && _mineFeed.root != null)
                foreach (var card in _mineFeed.root.GetComponentsInChildren<DesktopSocialPostCard>(true))
                    yield return card;
        }

        void ClearRoot(Transform root)
        {
            if (root == _content)
            {
                if (_selectedComments != null) _selectedComments.CloseComments();
                _selectedComments = null;
                var empty = transform.Find("DesignFeedInfo/CommentEmpty");
                if (empty != null) empty.gameObject.SetActive(true);
                CozyItemArt.Text(transform, "DesignFeedInfo/CommentCount", "");
                if (_commentsTitle != null) _commentsTitle.text = "选择一条动态\n在这里查看和发送评论";
            }
            if (root == null)
                return;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        void ClearUiHierarchy()
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

        void CreateRetry()
        {
            var retry = Find<Button>("Retry");
            if (retry != null)
                retry.gameObject.SetActive(true);
        }

        void SetStatus(string value)
        {
            if (_status != null)
                _status.text = value ?? string.Empty;
        }

        T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }

        static string FormatTime(long timestamp)
        {
            return timestamp <= 0 ? "刚刚" :
                DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }

        static Text AddLabel(Transform parent, string value, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value ?? string.Empty;
            text.font = CatTheme.Font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.color = CatTheme.Ink;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static Button AddButton(Transform parent, string label, float width)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = 40;
            element.preferredHeight = 40;
            var button = go.GetComponent<Button>();
            go.GetComponent<Image>().color = CatTheme.Cream;
            var text = AddLabel(go.transform, label, 16);
            text.alignment = TextAnchor.MiddleCenter;
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 4f);
            rt.offsetMax = new Vector2(-12f, -4f);
            DesktopModalUi.StyleButton(button);
            return button;
        }

        static InputField AddInput(Transform parent, string placeholder)
        {
            var go = new GameObject("CommentInput", typeof(RectTransform), typeof(Image),
                typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = 40;
            element.preferredHeight = 40;
            var input = go.GetComponent<InputField>();
            var text = AddLabel(go.transform, string.Empty, 16);
            input.textComponent = text;
            input.placeholder = AddLabel(go.transform, placeholder, 14);
            StyleInput(input);
            return input;
        }

        static void StyleInput(InputField input)
        {
            var image = input.GetComponent<Image>();
            if (image != null)
            {
                image.overrideSprite = null;
                image.sprite = null;
                image.color = new Color(1f, 0.99f, 0.96f, 1f);
            }
            StyleText(input.textComponent, 16);
            if (input.textComponent != null)
            {
                input.textComponent.horizontalOverflow = input.lineType == InputField.LineType.SingleLine
                    ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
                Place(input.textComponent.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(12f, 6f), new Vector2(-12f, -6f));
            }
            if (input.placeholder is Text placeholder)
            {
                StyleText(placeholder, 16);
                var color = CatTheme.Ink;
                color.a = 0.6f;
                placeholder.color = color;
                Place(placeholder.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(12f, 6f), new Vector2(-12f, -6f));
            }
            var colors = input.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = CatTheme.Cream;
            colors.disabledColor = CatTheme.Cream;
            input.colors = colors;
            input.caretColor = CatTheme.Ink;
        }

        static void StyleText(Text label, int size)
        {
            if (label == null)
                return;
            if (CatTheme.Font != null)
                label.font = CatTheme.Font;
            label.fontSize = size;
            label.color = CatTheme.Ink;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.raycastTarget = false;
        }

        static void Paper(Image image)
        {
            if (image == null || image.type == Image.Type.Filled)
                return;
            var color = CatTheme.Cream;
            color.a = image.color.a;
            image.color = color;
            CatTheme.ApplyPaper(image);
            if (image.GetComponent<ScrollRect>() != null && image.GetComponent<Mask>() == null &&
                image.GetComponent<RectMask2D>() == null)
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

        void BuildFallbackUi()
        {
            Debug.LogError("[DesktopUI] Prefab is missing required controls. Runtime will not build a second layout.");
            return;
        }
    }
}
