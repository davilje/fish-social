using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Data binding and interactions for one SocialPostCard prefab instance.
    /// The component never creates layout controls; all controls must exist in
    /// SocialPostCard.prefab or PostCommentRow.prefab.
    /// </summary>
    public sealed class DesktopSocialPostCard : MonoBehaviour, IPointerClickHandler
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
        bool _commentsLoadFailed;
        RectTransform _commentsHost;
        Transform _commentsHome;
        Action<DesktopSocialPostCard> _selectComments;
        Sprite _downloadedPhoto;
        Texture2D _downloadedTexture;
        readonly Dictionary<string, GameObject> _commentRows = new Dictionary<string, GameObject>();
        readonly HashSet<string> _socketComments = new HashSet<string>();

        public void Bind(SocialPostDto post, IAuthenticatedApiClient api)
        {
            _post = post;
            _api = api;
            ResolveBindings();
            if (_authorText == null || _photo == null || _bodyText == null || _fishInfoText == null ||
                _likeButton == null || _commentsButton == null || _commentsPanel == null ||
                _commentsContent == null || _commentInput == null || _sendCommentButton == null)
            {
                Debug.LogError("[DesktopUI] SocialPostCard requires Header/AuthorText, Photo, BodyText, FishInfoText, Actions/{LikeButton,CommentsButton} and CommentsPanel/{CommentsContent,CommentInput,SendButton} with serialized components.", this);
                return;
            }
            RenderPost();
            RestyleUi();
            RefreshCardLayout();
            BindActions();
            if (transform.Find("JournalCatch") != null)
            {
                var scroll = GetComponentInParent<ScrollRect>();
                var motion = CozyStickerMotion.Bind(gameObject, "post:" + (_post?.id ?? name), transform.GetSiblingIndex(),
                    scroll != null ? scroll.viewport : null, _post?.id, 0);
                if (!Application.isPlaying) motion.ApplyFinalPose();
            }
        }

        public string PostId => _post?.id;
        public SocialPostDto Post => _post;
        public int CommentCount => _post?.commentCount ?? 0;
        public event Action<int> CommentCountChanged;

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
                Find<RectTransform>("CommentsPanel/CommentsViewport/CommentsContent") ??
                Find<RectTransform>("CommentsPanel/CommentsContent");
            _commentInput = _commentInput ?? Find<InputField>("CommentsPanel/CommentInput");
            _sendCommentButton = _sendCommentButton ??
                Find<Button>("CommentsPanel/SendButton");
            if (_commentInput != null)
            {
                _commentInput.characterLimit = 200;
                _commentInput.lineType = InputField.LineType.MultiLineNewline;
            }
        }

        void RenderPost()
        {
            if (_post == null)
                return;

            var journal = transform.Find("JournalCatch") != null;
            if (_authorText != null)
                _authorText.text = (_post.nickname ?? "钓友") + (journal ? "" : " · " + FormatTime(_post.createdAt));
            if (journal)
            {
                CozyItemArt.Set(transform, "Header/JournalAvatar", DesktopDefaultAvatars.Portrait(_post.authorAvatarUrl, _post.playerId));
                CozyItemArt.Text(transform, "Header/JournalTime", FormatTime(_post.createdAt));
                CozyItemArt.Text(transform, "Header/JournalVisibility", _post.visibility == "private" ? "仅自己" : _post.visibility == "friends" ? "仅好友" : "公开");
                CozyItemArt.Set(transform, "JournalCatch/Art", _post.fish != null ? CozyItemArt.Fish(_post.fish.speciesId) : null);
                CozyItemArt.Text(transform, "JournalCatch/Name", _post.fish != null ? DesktopFishCatalog.SpeciesName(_post.fish.speciesId) : "鱼获记录");
                CozyItemArt.Text(transform, "JournalCatch/Pond", _post.fish?.pondId ?? "");
            }
            if (_bodyText != null)
            {
                _bodyText.supportRichText = false;
                _bodyText.text = _post.text ?? "分享了一条鱼获";
            }
            if (_fishInfoText != null)
            {
                var fish = _post.fish;
                _fishInfoText.text = "鱼获：" + (fish != null ? DesktopFishCatalog.SpeciesName(fish.speciesId) : "未知") +
                    "  品质：" + (fish != null ? DesktopFishCatalog.QualityName(fish.quality) : "未知") +
                    "  体长：" + (fish != null ? fish.sizeM.ToString("0.00") : "-") +
                    "m  重量：" + (fish != null
                        ? DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(fish.sizeM))
                        : "-") +
                    "  可见范围：" +
                    (_post.visibility == "friends" ? "仅好友" : "所有人");
            }
            if (journal && _fishInfoText != null)
                _fishInfoText.text = _post.fish != null ? _post.fish.sizeM.ToString("0.00") + " m · " + DesktopFishCatalog.QualityName(_post.fish.quality) : "";
            if (_likeButton != null)
                SetButtonText(_likeButton,
                    (_post.likedByMe ? "♥ 已赞 " : "♡ 点赞 ") + _post.likeCount);
            if (_commentsButton != null)
                SetButtonText(_commentsButton, "评论 " + _post.commentCount);
            CommentCountChanged?.Invoke(_post.commentCount);
            if (_commentsPanel != null)
                _commentsPanel.SetActive(false);
            if (_photo != null)
            {
                _photo.gameObject.SetActive(!journal && !string.IsNullOrEmpty(_post.photoUrl));
                if (!journal && !string.IsNullOrEmpty(_post.photoUrl))
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
            DesktopModalUi.StyleTree(transform);
            DesktopModalUi.AlignImageLeft(_photo);
            DesktopModalUi.StyleButton(_likeButton, "heart");
            DesktopModalUi.StyleButton(_commentsButton, "social");
            DesktopModalUi.StyleButton(_sendCommentButton, "check", true);
            SetButtonText(_sendCommentButton, transform.Find("JournalCatch") != null ? "留下鼓励" : "发送评论");
            RefreshLikeColor();
        }









        void RefreshCardLayout()
        {
            if (transform.Find("DesignCardVersion") == null || _bodyText == null || _photo == null)
                return;
            var journalCatch = transform.Find("JournalCatch") as RectTransform;
            if (journalCatch != null)
            {
                var storyHeight = CozySocialText.Apply(_bodyText, 17, 28.9f, _bodyText.rectTransform.rect.width > 1 ? _bodyText.rectTransform.rect.width : 640);
                _bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, storyHeight);
                var catchTop = 76 + storyHeight + 12;
                journalCatch.anchoredPosition = new Vector2(18, -catchTop);
                _fishInfoText.rectTransform.anchoredPosition = new Vector2(167, -(catchTop + 46));
                var footer = transform.Find("Actions") as RectTransform;
                if (footer != null) footer.anchoredPosition = new Vector2(18, -(catchTop + 102));
                var cardHeight = catchTop + 154;
                _bodyText.alignment = TextAnchor.UpperLeft;
                var inlineComments = _commentsPanel.transform as RectTransform;
                if (_commentsHost == null || inlineComments.parent != _commentsHost)
                {
                    inlineComments.anchoredPosition = new Vector2(18, -cardHeight);
                    if (_commentsPanel.activeSelf) cardHeight += inlineComments.sizeDelta.y + 14;
                }
                var cardLayout = GetComponent<LayoutElement>();
                if (cardLayout != null) cardLayout.minHeight = cardLayout.preferredHeight = cardHeight;
                return;
            }
            var bodyHeight = Mathf.Max(_bodyText.preferredHeight + 8f, 40f);
            _bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
            var next = 70f + bodyHeight + 12f;
            var picture = _photo.rectTransform;
            picture.anchoredPosition = new Vector2(18f, -next);
            if (_photo.gameObject.activeSelf) next += picture.sizeDelta.y + 12f;
            _fishInfoText.rectTransform.anchoredPosition = new Vector2(18f, -next);
            var fishHeight = Mathf.Max(38f, _fishInfoText.preferredHeight + 8f);
            _fishInfoText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fishHeight);
            next += fishHeight;
            var actions = transform.Find("Actions") as RectTransform;
            if (actions != null) actions.anchoredPosition = new Vector2(18f, -next);
            next += 46f;
            var comments = _commentsPanel.transform as RectTransform;
            if (_commentsHost == null || comments.parent != _commentsHost)
            {
                comments.anchoredPosition = new Vector2(18f, -next);
                if (_commentsPanel.activeSelf) next += comments.sizeDelta.y + 14f;
            }
            var element = GetComponent<LayoutElement>();
            if (element != null) element.minHeight = element.preferredHeight = next;
        }

        void ToggleLike()
        {
            if (_post == null || _api == null || _likeButton == null)
                return;
            _likeButton.interactable = false;
            StartCoroutine(_api.TogglePostLike(_post.id, (ok, liked, count, message) =>
            {
                if (this == null || _likeButton == null) return;
                ApplyLikeResult(ok, liked, count);
            }));
        }

        void ApplyLikeResult(bool ok, bool liked, int count)
        {
            if (_likeButton == null || _post == null) return;
            _likeButton.interactable = true;
            if (!ok) { CozyActionFeedback.Play(_likeButton.transform,false,"点赞失败"); return; }
            _post.likedByMe = liked;
            _post.likeCount = count;
            SetButtonText(_likeButton, (liked ? "♥ 已赞 " : "♡ 点赞 ") + count);
            RefreshLikeColor();
            var rect = _likeButton.transform as RectTransform;
            CozyMotion.Cancel(rect);
            var visual = _likeButton.GetComponent<CozyButtonVisual>();
            if (visual == null) return;
            visual.SetMotionScale(CozyMotion.Reduced ? Vector2.one : Vector2.one * 1.1f);
            CozyMotion.Animate(rect, .22f,
                t => { if (visual != null) visual.SetMotionScale(Vector2.one * Mathf.LerpUnclamped(1.1f, 1, t)); },
                () => { if (visual != null) visual.SetMotionScale(Vector2.one); },
                CozyWebMotion.Ease.OutBack,
                cleanup: () => { if (visual != null) visual.SetMotionScale(Vector2.one); });
        }

        void RefreshLikeColor()
        {
            if (_likeButton == null || _post == null) return;
            var label = _likeButton.transform.Find("Label")?.GetComponent<Text>();
            var visual = _likeButton.GetComponent<CozyButtonVisual>();
            if(visual != null) visual.SetTint(_post.likedByMe ? (Color)new Color32(255,240,237,255) : Color.white);
            if(label != null) label.color = _post.likedByMe ? (Color)new Color32(218,109,108,255) : CatTheme.Ink;
        }

        void CommentStatus(string value)
        {
            if(_commentsPanel == null) return;
            var text = _commentsPanel.transform.Find("CommentStatus")?.GetComponent<Text>();
            if(text != null) { text.text = value ?? ""; text.gameObject.SetActive(!string.IsNullOrEmpty(value)); }
        }

        void OnDisable()
        {
            if (_likeButton != null) CozyMotion.Cancel(_likeButton.transform as RectTransform);
        }

        public void ApplyLiked(PostLikedDto message)
        {
            if (_post == null || message == null || message.postId != _post.id) return;
            _post.likeCount = message.likeCount;
            if (_api != null && message.playerId == _api.PlayerId) _post.likedByMe = message.liked;
            SetButtonText(_likeButton, (_post.likedByMe ? "♥ 已赞 " : "♡ 点赞 ") + _post.likeCount);
        }

        public void ApplyCommented(PostCommentedDto message)
        {
            if (_post == null || message == null || message.postId != _post.id || message.comment == null) return;
            if (!_socketComments.Add(message.comment.id ?? string.Empty)) return;
            if (!_commentRows.ContainsKey(message.comment.id ?? string.Empty))
                _post.commentCount++;
            SetButtonText(_commentsButton, "评论 " + _post.commentCount);
            CommentCountChanged?.Invoke(_post.commentCount);
            if (_commentsPanel != null && _commentsPanel.activeSelf) AddCommentRow(message.comment);
        }

        public void ApplyCommentDeleted(PostCommentDeletedDto message)
        {
            if (_post == null || message == null || message.postId != _post.id) return;
            _post.commentCount = message.commentCount;
            SetButtonText(_commentsButton, "评论 " + _post.commentCount);
            CommentCountChanged?.Invoke(_post.commentCount);
            if (message.commentId != null && _commentRows.TryGetValue(message.commentId, out var row))
            {
                _commentRows.Remove(message.commentId);
                if (row != null) { row.SetActive(false); Destroy(row); }
            }
        }

        public void ConfigureCommentsHost(RectTransform host, Action<DesktopSocialPostCard> select)
        {
            _commentsHost = host;
            _selectComments = select;
            if (_commentsHome == null && _commentsPanel != null) _commentsHome = _commentsPanel.transform.parent == host ? transform : _commentsPanel.transform.parent;
        }

        public void CloseComments()
        {
            _commentsRequestVersion++;
            if (_commentsPanel == null) return;
            _commentsPanel.SetActive(false);
            if (_commentsHome != null && _commentsPanel.transform.parent != _commentsHome)
                _commentsPanel.transform.SetParent(_commentsHome, false);
            RefreshCardLayout();
        }

        void OnDestroy()
        {
            _commentsRequestVersion++;
            if (_commentsPanel != null && _commentsPanel.transform.parent == _commentsHost)
                Destroy(_commentsPanel);
            if (_downloadedPhoto != null) Destroy(_downloadedPhoto);
            if (_downloadedTexture != null) Destroy(_downloadedTexture);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging && _commentsHost != null)
                SelectComments();
        }

        public void SelectComments()
        {
            if (_commentsPanel == null) return;
            if (_commentsPanel.activeSelf && !_commentsLoadFailed) { _selectComments?.Invoke(this); return; }
            ToggleComments();
        }

        void ToggleComments()
        {
            if (_post == null || _api == null || _commentsPanel == null)
                return;
            if (_commentsPanel.activeSelf && !_commentsLoadFailed)
            {
                if (_commentsHost != null)
                {
                    _commentInput?.ActivateInputField();
                    return;
                }
                CloseComments();
                return;
            }

            _selectComments?.Invoke(this);
            if (_commentsHost != null)
            {
                var rect = _commentsPanel.transform as RectTransform;
                rect.SetParent(_commentsHost, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            _commentsRequestVersion++;
            var requestVersion = _commentsRequestVersion;
            _commentsLoadFailed = false;
            ClearComments();
            _commentsPanel.SetActive(true);
            CommentStatus("正在加载留言…");
            RefreshCardLayout();
            StartCoroutine(_api.GetPostComments(_post.id,
                (ok, comments, count, message) =>
                {
                    if (this == null || _commentsPanel == null || requestVersion != _commentsRequestVersion ||
                        !_commentsPanel.activeSelf)
                        return;
                    if (!ok)
                    {
                        _commentsLoadFailed = true;
                        CommentStatus("留言加载失败，点击卡片重试。");
                        SetButtonText(_commentsButton, "评论加载失败 · 重试");
                        return;
                    }
                    PresentComments(comments, count, false);
                }));
        }

        /// <summary>Render a completed server snapshot, also used by deterministic editor fixtures.</summary>
        public void PresentComments(PostCommentDto[] comments, int totalCount, bool select = true)
        {
            if (_post == null || _commentsPanel == null) return;
            if(select)_selectComments?.Invoke(this);
            if (_commentsHost != null)
            {
                var rect=(RectTransform)_commentsPanel.transform;
                rect.SetParent(_commentsHost,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;
            }
            _commentsLoadFailed=false;ClearComments();_commentsPanel.SetActive(true);
            _post.commentCount=totalCount;CommentCountChanged?.Invoke(totalCount);
            SetButtonText(_commentsButton,"评论 "+totalCount);
            CommentStatus(comments==null||comments.Length==0?"还没有留言\n给钓友留一句鼓励吧":"");
            for(var i=0;comments!=null&&i<comments.Length;i++)AddCommentRow(comments[i]);
            RefreshCardLayout();
        }

        void AddCommentRow(PostCommentDto comment)
        {
            if (comment == null || _commentsContent == null)
                return;
            if (!string.IsNullOrEmpty(comment.id) && _commentRows.ContainsKey(comment.id)) return;
            var row = DesktopUiPrefabFactory.Instantiate("PostCommentRow", _commentsContent);
            if (row == null)
                return;
            var label = FindIn<Text>(row, "Text") ?? FindIn<Text>(row, "CommentText");
            if (label != null)
            {
                label.supportRichText = false;
                var author = row.transform.Find("Author")?.GetComponent<Text>();
                if(author != null) { author.supportRichText=false;author.text=comment.nickname??"钓友";label.text=comment.text??""; }
                else label.text = (comment.nickname ?? "钓友") + "：" + comment.text;
                var height=CozySocialText.Apply(label,15,24,label.rectTransform.rect.width>1?label.rectTransform.rect.width:280);
                label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
                var layout=row.GetComponent<LayoutElement>();if(layout!=null)layout.preferredHeight=Mathf.Max(84,height+50);
                CommentStatus("");
            }
            var delete = FindIn<Button>(row, "Delete");
            if (label == null || delete == null)
            {
                Debug.LogError("[DesktopUI] PostCommentRow requires Text (or CommentText) and Delete Button; runtime layout generation is disabled.", row);
                row.SetActive(false);
                Destroy(row);
                return;
            }
            if (!string.IsNullOrEmpty(comment.id)) _commentRows[comment.id] = row;
            RestyleCommentRow(row, label, delete);
            if (delete != null &&
                (_api == null || (_api.PlayerId != comment.playerId && _api.PlayerId != _post.playerId)))
                delete.gameObject.SetActive(false);
            if (delete != null)
            {
                delete.onClick.RemoveAllListeners();
                delete.onClick.AddListener(() => DeleteComment(comment, row, delete));
            }
        }

        static void RestyleCommentRow(GameObject row, Text label, Button delete)
        {
            DesktopModalUi.StyleTree(row.transform);
            SetButtonText(delete, "删除");
        }

        void DeleteComment(PostCommentDto comment, GameObject row, Button delete)
        {
            if (_api == null || comment == null)
                return;
            delete.interactable = false;
            StartCoroutine(_api.DeletePostComment(_post.id, comment.id,
                (ok, count, message) =>
                {
                    if (this == null || delete == null) return;
                    if (!ok)
                    {
                        delete.interactable = true;
                        return;
                    }
                    _post.commentCount = count;
                    CommentCountChanged?.Invoke(count);
                    SetButtonText(_commentsButton, "评论 " + count);
                    if (!string.IsNullOrEmpty(comment.id)) _commentRows.Remove(comment.id);
                    if (row != null)
                    {
                        row.SetActive(false);
                        Destroy(row);
                    }
                }));
        }

        void SendComment()
        {
            if (_api == null || _post == null || _commentInput == null ||
                _sendCommentButton == null)
                return;
            var value = (_commentInput.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value) || value.Length > 200) { CommentStatus("请输入 1–200 字的留言。"); return; }
            _sendCommentButton.interactable = false;
            StartCoroutine(_api.AddPostComment(_post.id, value,
                (ok, comment, count, message) =>
                {
                    if (this == null || _sendCommentButton == null) return;
                    _sendCommentButton.interactable = true;
                    SetButtonText(_sendCommentButton, ok ? "留下鼓励" : "发送失败 · 重试");
                    CommentStatus(ok ? "" : "发送失败，留言文字已保留。");
                    if (!ok)
                        return;
                    _post.commentCount = count;
                    CommentCountChanged?.Invoke(count);
                    SetButtonText(_commentsButton, "评论 " + count);
                    if(_commentInput.text.Trim()==value) _commentInput.text = string.Empty;
                    AddCommentRow(comment);
                }));
        }

        void ClearComments()
        {
            _commentRows.Clear();
            if (_commentsContent == null)
                return;
            for (var i = _commentsContent.childCount - 1; i >= 0; i--)
            {
                var child=_commentsContent.GetChild(i);child.gameObject.SetActive(false);child.SetParent(null,false);
                if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
            }
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
                    if (_downloadedPhoto != null) Destroy(_downloadedPhoto);
                    if (_downloadedTexture != null) Destroy(_downloadedTexture);
                    _downloadedTexture = texture;
                    _downloadedPhoto = Sprite.Create(texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                    _photo.sprite = _downloadedPhoto;
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
