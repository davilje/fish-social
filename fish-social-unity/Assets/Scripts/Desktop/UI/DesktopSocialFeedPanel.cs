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
            EnsureUi();
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
            CatTheme.ApplyPaper(panelImage);
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

            RestyleUi();

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
            Paper(Find<Image>("Header"));
            Paper(Find<Image>("Scroll"));
            Paper(Find<Image>("Scroll/Viewport"));
            Paper(Find<Image>("Scroll/Viewport/Content"));
            foreach (var label in GetComponentsInChildren<Text>(true))
                StyleText(label, 16);
            foreach (var input in GetComponentsInChildren<InputField>(true))
                StyleInput(input);
            Place(Find<RectTransform>("Header"), new Vector2(0f, 1f), Vector2.one,
                new Vector2(12f, -50f), new Vector2(-12f, -10f));
            Place(_publicTab.transform as RectTransform, Vector2.zero, new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(-6f, 0f));
            Place(_friendsTab.transform as RectTransform, new Vector2(0.5f, 0f), Vector2.one,
                new Vector2(6f, 0f), Vector2.zero);
            Place(_status.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(16f, -86f), new Vector2(-132f, -54f));
            Place(_scroll.transform as RectTransform, Vector2.zero, Vector2.one,
                new Vector2(12f, 12f), new Vector2(-12f, -96f));
            var viewport = Find<RectTransform>("Scroll/Viewport");
            Place(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = new Vector2(0f, _content.offsetMin.y);
            _content.offsetMax = Vector2.zero;
            var layout = _content.GetComponent<VerticalLayoutGroup>();
            if (layout == null && _content.GetComponent<LayoutGroup>() == null)
                layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(6, 6, 6, 6);
                layout.spacing = 14f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
            var fitter = _content.GetComponent<ContentSizeFitter>() ?? _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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
                var label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = names[index];
                    StyleText(label, 18);
                    label.alignment = TextAnchor.MiddleCenter;
                    Place(label.rectTransform, Vector2.zero, Vector2.one,
                        new Vector2(36f, 4f), new Vector2(-12f, -4f));
                }
                CatTheme.StyleButton(button, icons[index]);
                var visual = button.GetComponent<CozyButtonVisual>();
                if (visual != null)
                    visual.SetSelected(_friendsOnly == (index == 1));
            }
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
            Place(retry.transform as RectTransform, Vector2.one, Vector2.one,
                new Vector2(-120f, -88f), new Vector2(-12f, -54f));
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
            CatTheme.StyleButton(button);
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
