using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Data binding and interactions for one SocialPostCard prefab instance.
    /// The component never creates layout controls; all controls must exist in
    /// SocialPostCard.prefab or PostCommentRow.prefab.
    /// </summary>
    public sealed class DesktopSocialPostCard : MonoBehaviour
    {
        [SerializeField] Text _authorText;
        [SerializeField] Image _photo;
        [SerializeField] Text _bodyText;
        [SerializeField] Text _fishInfoText;
        [SerializeField] Button _likeButton;
        [SerializeField] Button _commentsButton;
        [SerializeField] GameObject _commentsPanel;
        [SerializeField] RectTransform _commentsContent;
        [SerializeField] InputField _commentInput;
        [SerializeField] Button _sendCommentButton;

        static int _activeImageLoads;
        const int MaxConcurrentImageLoads = 4;
        IAuthenticatedApiClient _api;
        SocialPostDto _post;
        int _commentsRequestVersion;

        public void Bind(SocialPostDto post, IAuthenticatedApiClient api)
        {
            _post = post;
            _api = api;
            ResolveBindings();
            RenderPost();
            RestyleUi();
            BindActions();
        }

        void ResolveBindings()
        {
            _authorText = _authorText ?? Find<Text>("Header/AuthorText");
            _photo = _photo ?? Find<Image>("Photo");
            _bodyText = _bodyText ?? Find<Text>("BodyText");
            _fishInfoText = _fishInfoText ?? Find<Text>("FishInfoText");
            _likeButton = _likeButton ?? Find<Button>("Actions/LikeButton");
            _commentsButton = _commentsButton ?? Find<Button>("Actions/CommentsButton");
            _commentsPanel = _commentsPanel ?? Find<Transform>("CommentsPanel")?.gameObject;
            _commentsContent = _commentsContent ??
                Find<RectTransform>("CommentsPanel/CommentsContent");
            _commentInput = _commentInput ?? Find<InputField>("CommentsPanel/CommentInput");
            _sendCommentButton = _sendCommentButton ??
                Find<Button>("CommentsPanel/SendButton");
        }

        void RenderPost()
        {
            if (_post == null)
                return;

            if (_authorText != null)
                _authorText.text = (_post.nickname ?? "钓友") + " · " + FormatTime(_post.createdAt);
            if (_bodyText != null)
                _bodyText.text = _post.text ?? "分享了一条鱼获";
            if (_fishInfoText != null)
            {
                var fish = _post.fish;
                _fishInfoText.text = "鱼获：" + (fish != null ? fish.speciesId : "未知") +
                    "  品质：" + (fish != null ? fish.quality : "未知") +
                    "  体长：" + (fish != null ? fish.sizeM.ToString("0.00") : "-") +
                    "m  重量：" + (fish != null
                        ? DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(fish.sizeM))
                        : "-") +
                    "  可见范围：" +
                    (_post.visibility == "friends" ? "仅好友" : "所有人");
            }
            if (_likeButton != null)
                SetButtonText(_likeButton,
                    (_post.likedByMe ? "已赞 " : "点赞 ") + _post.likeCount);
            if (_commentsButton != null)
                SetButtonText(_commentsButton, "评论 " + _post.commentCount);
            if (_commentsPanel != null)
                _commentsPanel.SetActive(false);
            if (_photo != null)
            {
                _photo.gameObject.SetActive(!string.IsNullOrEmpty(_post.photoUrl));
                if (!string.IsNullOrEmpty(_post.photoUrl))
                {
                    _photo.color = _photo.sprite != null ? Color.white : CatTheme.Cream;
                    StartCoroutine(LoadPhoto(_post.photoUrl));
                }
            }
        }

        void BindActions()
        {
            if (_likeButton != null)
            {
                _likeButton.onClick.RemoveAllListeners();
                _likeButton.onClick.AddListener(ToggleLike);
            }
            if (_commentsButton != null)
            {
                _commentsButton.onClick.RemoveAllListeners();
                _commentsButton.onClick.AddListener(ToggleComments);
            }
            if (_sendCommentButton != null)
            {
                _sendCommentButton.onClick.RemoveAllListeners();
                _sendCommentButton.onClick.AddListener(SendComment);
            }
        }

        void RestyleUi()
        {
            CatTheme.ApplyPaper(GetComponent<Image>());
            var layout = transform.GetComponent<VerticalLayoutGroup>();
            if (layout == null && GetComponent<LayoutGroup>() == null)
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(18, 18, 14, 14);
                layout.spacing = 8f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
            var element = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            element.minHeight = 188f;
            element.preferredHeight = -1f;
            element.flexibleHeight = 0f;
            element.ignoreLayout = false;
            StyleText(_authorText, 18, 28f);
            if (_authorText != null)
                _authorText.fontStyle = FontStyle.Bold;
            StyleText(_bodyText, 18, 48f);
            StyleText(_fishInfoText, 16, 40f);
            var header = transform.Find("Header");
            if (header != null)
            {
                CatTheme.ApplyPaper(header.GetComponent<Image>());
                var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
                if (headerLayout != null)
                {
                    headerLayout.spacing = 10f;
                    headerLayout.childControlWidth = true;
                    headerLayout.childControlHeight = true;
                    headerLayout.childForceExpandWidth = false;
                    headerLayout.childForceExpandHeight = false;
                }
            }
            if (_photo != null)
            {
                var photoLayout = _photo.GetComponent<LayoutElement>() ?? _photo.gameObject.AddComponent<LayoutElement>();
                photoLayout.minHeight = 148f;
                photoLayout.preferredHeight = 148f;
                photoLayout.flexibleHeight = 0f;
                _photo.preserveAspect = true;
                _photo.color = _photo.sprite != null ? Color.white : CatTheme.Cream;
            }
            var actions = transform.Find("Actions");
            if (actions != null)
            {
                CatTheme.ApplyPaper(actions.GetComponent<Image>());
                var actionLayout = actions.GetComponent<HorizontalLayoutGroup>();
                if (actionLayout != null)
                {
                    actionLayout.spacing = 12f;
                    actionLayout.childControlWidth = true;
                    actionLayout.childControlHeight = true;
                    actionLayout.childForceExpandWidth = false;
                    actionLayout.childForceExpandHeight = false;
                }
            }
            StyleAction(_likeButton, "heart");
            StyleAction(_commentsButton, "social");
            SetButtonText(_sendCommentButton, "发送评论");
            StyleAction(_sendCommentButton, "check", true);
            if (_commentsPanel != null)
            {
                CatTheme.ApplyPaper(_commentsPanel.GetComponent<Image>());
                var commentsLayout = _commentsPanel.GetComponent<VerticalLayoutGroup>();
                if (commentsLayout != null)
                {
                    commentsLayout.padding = new RectOffset(12, 12, 10, 10);
                    commentsLayout.spacing = 8f;
                    commentsLayout.childControlWidth = true;
                    commentsLayout.childControlHeight = true;
                    commentsLayout.childForceExpandWidth = true;
                    commentsLayout.childForceExpandHeight = false;
                }
            }
            if (_commentsContent != null)
            {
                var commentsLayout = _commentsContent.GetComponent<VerticalLayoutGroup>();
                if (commentsLayout != null)
                {
                    commentsLayout.spacing = 8f;
                    commentsLayout.childControlWidth = true;
                    commentsLayout.childControlHeight = true;
                    commentsLayout.childForceExpandWidth = true;
                    commentsLayout.childForceExpandHeight = false;
                }
            }
            StyleInput(_commentInput);
        }

        static void StyleText(Text label, int size, float minHeight)
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
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            var element = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0f;
            element.preferredWidth = -1f;
            element.flexibleWidth = 1f;
            element.minHeight = minHeight;
            element.preferredHeight = -1f;
            element.flexibleHeight = 0f;
        }

        static void StyleAction(Button button, string icon, bool primary = false)
        {
            if (button == null)
                return;
            var element = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 120f;
            element.preferredWidth = 156f;
            element.minHeight = 40f;
            element.preferredHeight = 40f;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
            var label = button.GetComponentInChildren<Text>(true);
            StyleText(label, 16, 0f);
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleCenter;
                label.GetComponent<LayoutElement>().ignoreLayout = true;
                Inset(label.rectTransform, icon == null ? 12f : 36f, 4f);
            }
            CatTheme.StyleButton(button, icon, primary);
        }

        static void StyleInput(InputField input)
        {
            if (input == null)
                return;
            var image = input.GetComponent<Image>();
            if (image != null)
            {
                image.overrideSprite = null;
                image.sprite = null;
                image.color = new Color(1f, 0.99f, 0.96f, 1f);
            }
            var element = input.GetComponent<LayoutElement>() ?? input.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 44f;
            element.preferredHeight = 44f;
            element.flexibleHeight = 0f;
            StyleText(input.textComponent, 16, 0f);
            if (input.textComponent != null)
            {
                input.textComponent.alignment = TextAnchor.MiddleLeft;
                input.textComponent.horizontalOverflow = input.lineType == InputField.LineType.SingleLine
                    ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
                Inset(input.textComponent.rectTransform, 12f, 6f);
            }
            if (input.placeholder is Text placeholder)
            {
                StyleText(placeholder, 16, 0f);
                placeholder.alignment = TextAnchor.MiddleLeft;
                var color = CatTheme.Ink;
                color.a = 0.6f;
                placeholder.color = color;
                Inset(placeholder.rectTransform, 12f, 6f);
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

        static void Inset(RectTransform rect, float left, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, vertical);
            rect.offsetMax = new Vector2(-12f, -vertical);
        }

        void ToggleLike()
        {
            if (_post == null || _api == null || _likeButton == null)
                return;
            _likeButton.interactable = false;
            StartCoroutine(_api.TogglePostLike(_post.id, (ok, liked, count, message) =>
            {
                _likeButton.interactable = true;
                if (!ok)
                    return;
                _post.likedByMe = liked;
                _post.likeCount = count;
                SetButtonText(_likeButton, (liked ? "已赞 " : "点赞 ") + count);
            }));
        }

        void ToggleComments()
        {
            if (_post == null || _api == null || _commentsPanel == null)
                return;
            if (_commentsPanel.activeSelf)
            {
                _commentsPanel.SetActive(false);
                return;
            }

            _commentsRequestVersion++;
            var requestVersion = _commentsRequestVersion;
            ClearComments();
            _commentsPanel.SetActive(true);
            StartCoroutine(_api.GetPostComments(_post.id,
                (ok, comments, count, message) =>
                {
                    if (requestVersion != _commentsRequestVersion ||
                        !_commentsPanel.activeSelf)
                        return;
                    if (!ok)
                        return;
                    _post.commentCount = count;
                    SetButtonText(_commentsButton, "评论 " + count);
                    for (var i = 0; i < comments.Length; i++)
                        AddCommentRow(comments[i]);
                }));
        }

        void AddCommentRow(PostCommentDto comment)
        {
            if (comment == null || _commentsContent == null)
                return;
            var row = DesktopUiPrefabFactory.Instantiate("PostCommentRow", _commentsContent);
            if (row == null)
                return;
            var label = FindIn<Text>(row, "Text") ?? FindIn<Text>(row, "CommentText");
            if (label != null)
                label.text = (comment.nickname ?? "钓友") + "：" + comment.text;
            var delete = FindIn<Button>(row, "Delete");
            RestyleCommentRow(row, label, delete);
            if (delete != null &&
                _api.PlayerId != comment.playerId && _api.PlayerId != _post.playerId)
                delete.gameObject.SetActive(false);
            if (delete != null)
            {
                delete.onClick.RemoveAllListeners();
                delete.onClick.AddListener(() => DeleteComment(comment, row, delete));
            }
        }

        static void RestyleCommentRow(GameObject row, Text label, Button delete)
        {
            CatTheme.ApplyPaper(row.GetComponent<Image>());
            StyleText(label, 16, 40f);
            var element = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
            element.minHeight = 40f;
            element.preferredHeight = -1f;
            element.flexibleHeight = 0f;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(8, 8, 6, 6);
                layout.spacing = 12f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
            SetButtonText(delete, "删除");
            StyleAction(delete, null);
            if (delete != null)
            {
                var deleteLayout = delete.GetComponent<LayoutElement>();
                deleteLayout.minWidth = 76f;
                deleteLayout.preferredWidth = 84f;
            }
        }

        void DeleteComment(PostCommentDto comment, GameObject row, Button delete)
        {
            if (_api == null || comment == null)
                return;
            delete.interactable = false;
            StartCoroutine(_api.DeletePostComment(_post.id, comment.id,
                (ok, count, message) =>
                {
                    if (!ok)
                    {
                        delete.interactable = true;
                        return;
                    }
                    _post.commentCount = count;
                    SetButtonText(_commentsButton, "评论 " + count);
                    if (row != null)
                        Destroy(row);
                }));
        }

        void SendComment()
        {
            if (_api == null || _post == null || _commentInput == null ||
                _sendCommentButton == null)
                return;
            var value = (_commentInput.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value) || value.Length > 200)
                return;
            _sendCommentButton.interactable = false;
            StartCoroutine(_api.AddPostComment(_post.id, value,
                (ok, comment, count, message) =>
                {
                    _sendCommentButton.interactable = true;
                    if (!ok)
                        return;
                    _post.commentCount = count;
                    SetButtonText(_commentsButton, "评论 " + count);
                    _commentInput.text = string.Empty;
                    AddCommentRow(comment);
                }));
        }

        void ClearComments()
        {
            if (_commentsContent == null)
                return;
            for (var i = _commentsContent.childCount - 1; i >= 0; i--)
                Destroy(_commentsContent.GetChild(i).gameObject);
        }

        IEnumerator LoadPhoto(string url)
        {
            if (_photo == null || string.IsNullOrEmpty(url))
                yield break;
            if (url.StartsWith("/"))
                url = (_api != null ? _api.BaseUrl : string.Empty) + url;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                yield break;
            while (_activeImageLoads >= MaxConcurrentImageLoads)
                yield return null;
            _activeImageLoads++;
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                try
                {
                    request.timeout = 10;
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                        yield break;
                    var texture = DownloadHandlerTexture.GetContent(request);
                    if (texture == null || _photo == null)
                        yield break;
                    _photo.sprite = Sprite.Create(texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                    _photo.preserveAspect = true;
                    _photo.color = Color.white;
                }
                finally
                {
                    _activeImageLoads--;
                }
            }
        }

        T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }

        static T FindIn<T>(GameObject root, string path) where T : Component
        {
            var node = root != null ? root.transform.Find(path) : null;
            return node != null ? node.GetComponent<T>() : null;
        }

        static void SetButtonText(Button button, string value)
        {
            var text = button != null ? button.GetComponentInChildren<Text>() : null;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        static string FormatTime(long timestamp)
        {
            return timestamp <= 0 ? "刚刚" :
                DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime
                    .ToString("yyyy-MM-dd HH:mm");
        }
    }
}
