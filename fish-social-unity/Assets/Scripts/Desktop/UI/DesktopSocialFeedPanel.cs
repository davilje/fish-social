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
            if (!_uiReady)
                return;
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
            if (_pond != null)
            {
                _pond.PostLikedReceived -= OnPostLiked;
                _pond.PostCommentedReceived -= OnPostCommented;
                _pond.PostCommentDeletedReceived -= OnPostCommentDeleted;
            }
        }

        void EnsureUi()
        {
            _uiReady = false;
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
            RestyleTabs();
            Refresh();
        }

        void RestyleUi()
        {
            DesktopModalUi.StyleTree(transform);
            RestyleTabs();
        }

        void RestyleTabs()
        {
            var tabs = new[] { _publicTab, _friendsTab };
            var names = new[] { "公开动态", "好友动态" };
            var icons = new[] { "social", "user" };
            for (var index = 0; index < tabs.Length; index++)
            {
                var button = tabs[index];
                if (button == null)
                    continue;
                DesktopModalUi.SetButtonLabel(button, names[index]);
                DesktopModalUi.StyleButton(button, icons[index]);
                var visual = button.GetComponent<CozyButtonVisual>();
                if (visual != null)
                    visual.SetSelected(_friendsOnly == (index == 1));
            }
        }

        void Refresh()
        {
            if (!_uiReady)
                return;
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
            DesktopModalUi.StyleTree(card.transform);
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
            var retry = Find<Button>("Retry");
            if (retry != null)
                retry.gameObject.SetActive(false);
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
