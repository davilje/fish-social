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
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        RectTransform _content;
        ScrollRect _scroll;
        Text _status;
        Button _publicTab;
        Button _friendsTab;
        bool _friendsOnly;
        Coroutine _loadRoutine;
        bool _isOpen;
        bool _hasLoaded;
        bool _loadedFriendsOnly;
        bool _loadingMore;
        bool _canLoadMore;
        int _nextOffset;
        const int InitialPageSize = 50;
        const int MorePageSize = 10;

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond)
        {
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
            if (_isOpen)
                return;
            _isOpen = true;
            if (!_hasLoaded || _loadedFriendsOnly != _friendsOnly)
                Refresh();
        }

        public void OnClosed()
        {
            _isOpen = false;
            if (_loadRoutine != null)
                StopCoroutine(_loadRoutine);
            _loadRoutine = null;
            _loadingMore = false;
            _hasLoaded = false;
            _canLoadMore = false;
            _nextOffset = 0;
            ClearContent();
        }

        public void BuildEditorLayout()
        {
            if (Find<Button>("Header/Public") == null ||
                Find<Button>("Header/Friends") == null ||
                Find<RectTransform>("Scroll/Viewport/Content") == null)
            {
                ClearUiHierarchy();
                BuildFallbackUi();
            }
        }

        void OnDestroy()
        {
            if (_pond != null)
            {
                _pond.PostLikedReceived -= OnPostLiked;
                _pond.PostCommentedReceived -= OnPostCommented;
                _pond.PostCommentDeletedReceived -= OnPostCommentDeleted;
            }
        }

        void EnsureUi()
        {
            var panelImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.10f, 0.14f, 1f);
            _publicTab = Find<Button>("Header/Public");
            _friendsTab = Find<Button>("Header/Friends");
            _status = Find<Text>("Status");
            _content = Find<RectTransform>("Scroll/Viewport/Content");
            _scroll = Find<ScrollRect>("Scroll");
            if (_publicTab == null || _friendsTab == null || _status == null ||
                _content == null || _scroll == null)
            {
                Debug.LogError(
                    "[DesktopUI] PanelSocialFeed Prefab 缺少必需控件。" +
                    "请在 Unity Prefab Manager 中执行“初始化”，" +
                    "运行时不会再重建或覆盖手动布局。");
                return;
            }

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
            if (_scroll != null)
            {
                _scroll.onValueChanged.RemoveListener(OnScrollChanged);
                _scroll.onValueChanged.AddListener(OnScrollChanged);
            }
        }

        void SelectFeed(bool friendsOnly)
        {
            if (_friendsOnly == friendsOnly && _hasLoaded)
                return;
            _friendsOnly = friendsOnly;
            Refresh();
        }

        void Refresh()
        {
            if (_loadRoutine != null)
                return;
            _loadingMore = false;
            _nextOffset = 0;
            _canLoadMore = false;
            _loadRoutine = StartCoroutine(LoadRoutine(false, InitialPageSize, 0));
        }

        void OnScrollChanged(Vector2 normalizedPosition)
        {
            if (normalizedPosition.y <= 0.05f)
                LoadMore();
        }

        void LoadMore()
        {
            if (!_isOpen || !_hasLoaded || !_canLoadMore ||
                _loadingMore || _loadRoutine != null)
                return;
            _loadingMore = true;
            _loadRoutine = StartCoroutine(
                LoadRoutine(true, MorePageSize, _nextOffset));
        }

        IEnumerator LoadRoutine(bool append, int limit, int offset)
        {
            var hasContent = _content != null && _content.childCount > 0;
            if (!append)
                SetStatus(hasContent ? "正在更新动态…" : "正在加载动态…");
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
            yield return _api.GetSocialFeed(_friendsOnly, limit, offset,
                (success, items, message) =>
            {
                ok = success;
                posts = items;
                error = message;
                done = true;
            });
            while (!done)
                yield return null;

            if (!ok)
            {
                SetStatus(error ?? "动态加载失败，请点击重试。");
                CreateRetry();
                _loadRoutine = null;
                _loadingMore = false;
                yield break;
            }
            if (posts == null || posts.Length == 0)
            {
                if (!append)
                {
                    ClearContent();
                    SetStatus(_friendsOnly ? "暂无好友动态。" : "暂无公开动态。");
                }
                _canLoadMore = false;
                _loadRoutine = null;
                _loadingMore = false;
                yield break;
            }

            if (!append)
            {
                ClearContent();
                SetStatus(string.Empty);
            }
            for (var i = 0; i < posts.Length; i++)
            {
                CreatePostCard(posts[i]);
                if ((i + 1) % MorePageSize == 0)
                    yield return null;
            }
            _nextOffset = offset + posts.Length;
            _canLoadMore = posts.Length >= limit;
            _hasLoaded = true;
            _loadedFriendsOnly = _friendsOnly;
            _loadRoutine = null;
            _loadingMore = false;
        }

        void CreatePostCard(SocialPostDto post)
        {
            var card = DesktopUiPrefabFactory.Instantiate("SocialPostCard", _content);
            if (card == null)
                return;
            var binder = card.GetComponent<DesktopSocialPostCard>();
            if (binder == null)
            {
                Debug.LogError("[DesktopUI] SocialPostCard 缺少 DesktopSocialPostCard 组件。");
                Destroy(card);
                return;
            }
            binder.Bind(post, _api);
        }

        void OnPostLiked(PostLikedDto message)
        {
            if (message != null && isActiveAndEnabled)
                Refresh();
        }

        void OnPostCommented(PostCommentedDto message)
        {
            if (message != null && isActiveAndEnabled)
                Refresh();
        }

        void OnPostCommentDeleted(PostCommentDeletedDto message)
        {
            if (message != null && isActiveAndEnabled)
                Refresh();
        }

        void ClearContent()
        {
            if (_content == null)
                return;
            for (var i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
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
            var retry = AddButton(transform, "重试", 100);
            retry.onClick.AddListener(Refresh);
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
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.color = Color.white;
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
            element.minHeight = 32;
            var button = go.GetComponent<Button>();
            go.GetComponent<Image>().color = new Color(0.18f, 0.27f, 0.34f, 1f);
            var text = AddLabel(go.transform, label, 14);
            text.alignment = TextAnchor.MiddleCenter;
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return button;
        }

        static InputField AddInput(Transform parent, string placeholder)
        {
            var go = new GameObject("CommentInput", typeof(RectTransform), typeof(Image),
                typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = 34;
            element.preferredHeight = 34;
            var input = go.GetComponent<InputField>();
            go.GetComponent<Image>().color = new Color(0.16f, 0.21f, 0.26f, 1f);
            var text = AddLabel(go.transform, string.Empty, 14);
            text.color = Color.white;
            input.textComponent = text;
            input.placeholder = AddLabel(go.transform, placeholder, 14);
            return input;
        }

        void BuildFallbackUi()
        {
            Debug.LogError("[DesktopUI] Prefab is missing required controls. Runtime will not build a second layout.");
            return;
        }
    }
}
