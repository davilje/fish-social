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
                    _photo.color = new Color(0.08f, 0.11f, 0.14f, 1f);
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
            if (delete != null &&
                _api.PlayerId != comment.playerId && _api.PlayerId != _post.playerId)
                delete.gameObject.SetActive(false);
            if (delete != null)
            {
                delete.onClick.RemoveAllListeners();
                delete.onClick.AddListener(() => DeleteComment(comment, row, delete));
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
