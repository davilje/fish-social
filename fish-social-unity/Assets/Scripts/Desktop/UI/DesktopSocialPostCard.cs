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
                StylePostPaper();
                StylePostTape(_api != null && _post.playerId == _api.PlayerId);
                var scroll = GetComponentInParent<ScrollRect>();
                var motion = CozyStickerMotion.Bind(gameObject, "post:" + (_post?.id ?? name), transform.GetSiblingIndex(),
                    scroll != null ? scroll.viewport : null, _post?.id, 0);
                motion?.RememberTape();
                if (!Application.isPlaying) motion.ApplyFinalPose();
            }
            var hint = transform.Find("Actions/JournalHint")?.GetComponent<Text>();
            if (hint != null)
            {
                var mine = _api != null && _post.playerId == _api.PlayerId;
                hint.text = mine ? DesktopLocalization.Text("ui.social.post_card.title.my_story", "我的鱼获故事") : DesktopLocalization.Text("ui.social.post_card.hint.open_comments", "点击卡片，看看钓友来信");
                hint.fontSize = 12;
                hint.alignment = TextAnchor.MiddleRight;
                hint.horizontalOverflow = HorizontalWrapMode.Overflow;
                hint.color = new Color(176f / 255f, 146f / 255f, 114f / 255f);
            }
        }

        void StylePostTape(bool mine)
        {
            var tape = transform.Find("JournalTape") as RectTransform;
            if (tape == null) return;
            var image = tape.GetComponent<Image>();
            if (image != null)
            {
                var sprite = CozyJournalTheme.Sprite(mine ? "decor/tape-gold-dots" : "decor/tape-pink-stripe");
                if (sprite != null) image.sprite = sprite;
            }
            tape.anchorMin = tape.anchorMax = new Vector2(0f, 1f);
            tape.pivot = new Vector2(0.5f, 0.5f);
            tape.sizeDelta = new Vector2(104f, 26f);
            tape.anchoredPosition = new Vector2(34f, -5f);
            tape.localEulerAngles = new Vector3(0f, 0f, 28f);
            tape.gameObject.SetActive(false);
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

        static string PondLabel(string pondId)
        {
            if (string.IsNullOrEmpty(pondId))
                return "";
            var pond = DesktopGameData.GetPond(pondId);
            return pond != null && !string.IsNullOrEmpty(pond.name) ? pond.name : pondId;
        }

        void StyleVisibility(string label)
        {
            var text = transform.Find("Header/JournalVisibility")?.GetComponent<Text>();
            if (text == null)
                return;
            text.text = label;
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(127f / 255f, 156f / 255f, 94f / 255f);
            var paper = text.transform.parent.Find("VisibilityPaper");
            if (paper != null) paper.gameObject.SetActive(false);
            var width = label != null && label.Length > 2 ? 72f : 52f;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            text.rectTransform.pivot = new Vector2(1f, 0.5f);
            text.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
            text.rectTransform.sizeDelta = new Vector2(width, 22f);
            var header = text.transform.parent as RectTransform;
            if (header != null && header.rect.width > 1f)
            {
                var limit = header.rect.width - width - 24f;
                foreach (var lineName in new[] { "AuthorText", "JournalTime" })
                {
                    var line = header.Find(lineName) as RectTransform;
                    if (line == null) continue;
                    line.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(80f, limit - line.anchoredPosition.x));
                }
            }
            var plate = text.transform.parent.Find("JournalVisibilityPlate");
            if (plate == null)
            {
                var go = new GameObject("JournalVisibilityPlate", typeof(RectTransform), typeof(CozyCssGraphic));
                go.transform.SetParent(text.transform.parent, false);
                plate = go.transform;
                var graphic = go.GetComponent<CozyCssGraphic>();
                graphic.shape = CozyCssGraphic.Shape.Paper;
                graphic.radius = 11f;
                graphic.border = 0f;
                graphic.color = new Color(234f / 255f, 241f / 255f, 223f / 255f);
                graphic.raycastTarget = false;
            }
            var rect = (RectTransform)plate;
            rect.anchorMin = text.rectTransform.anchorMin;
            rect.anchorMax = text.rectTransform.anchorMax;
            rect.pivot = text.rectTransform.pivot;
            rect.anchoredPosition = text.rectTransform.anchoredPosition;
            rect.sizeDelta = text.rectTransform.sizeDelta;
            plate.SetSiblingIndex(text.transform.GetSiblingIndex());
        }

        void StylePostPaper()
        {
            var image = GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(1f, 1f, 1f, 0f);
            }
            var paper = transform.Find("PostPaper");
            if (paper == null)
            {
                var go = new GameObject("PostPaper", typeof(RectTransform), typeof(CozyCssGraphic));
                go.transform.SetParent(transform, false);
                go.transform.SetAsFirstSibling();
                paper = go.transform;
                var graphic = go.GetComponent<CozyCssGraphic>();
                graphic.shape = CozyCssGraphic.Shape.Paper;
                graphic.radius = 18f;
                graphic.border = 0f;
                graphic.color = Color.white;
                graphic.raycastTarget = false;
            }
            var rect = (RectTransform)paper;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            paper.SetAsFirstSibling();
        }

        void StyleCatchPlate()
        {
            var plate = transform.Find("JournalCatch");
            if (plate == null)
                return;
            var framed = plate.Find("JournalPaper");
            if (framed != null) framed.gameObject.SetActive(false);
            // The catch row has no Image of its own. The sliced input frame was the only plate, so the fish sat on white.
            CozyCssGraphic.EnsureWash(plate, "CatchWash", new Color(251f / 255f, 243f / 255f, 228f / 255f, 1f), 12f, 0f, Color.clear);
            FitCatchWidth(plate as RectTransform, ((RectTransform)plate).anchoredPosition.y);
        }

        static void FitCatchWidth(RectTransform rect, float top)
        {
            if (rect == null) return;
            var height = rect.rect.height > 1f ? rect.rect.height : rect.sizeDelta.y > 1f ? rect.sizeDelta.y : 88f;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(18f, top - height);
            rect.offsetMax = new Vector2(-18f, top);
        }

        static void StyleQuiet(Button button, bool selected)
        {
            button?.GetComponent<CozyButtonVisual>()?.ConfigureSurface(
                "surfaces/button-quiet-default",
                "surfaces/button-quiet-hover",
                "surfaces/button-quiet-pressed",
                selected ? "surfaces/button-primary-default" : "surfaces/button-quiet-default");
        }

        void RevealComments()
        {
            if (_commentsPanel == null)
                return;
            var image = _commentsPanel.GetComponent<Image>();
            if (image != null)
            {
                image.overrideSprite = null;
                image.sprite = null;
                image.color = new Color(1f, 1f, 1f, 0f);
            }
            var scroll = _commentsPanel.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.verticalScrollbar == null && scroll.transform.Find("CommentScrollbar") == null)
            {
                var barGo = new GameObject("CommentScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
                barGo.transform.SetParent(scroll.transform, false);
                var bar = (RectTransform)barGo.transform;
                bar.anchorMin = new Vector2(1f, 0f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(1f, 0.5f);
                bar.sizeDelta = new Vector2(8f, -8f);
                bar.anchoredPosition = new Vector2(-2f, 0f);
                barGo.GetComponent<Image>().color = new Color(0.886f, 0.812f, 0.686f, 0.95f);
                var sliding = new GameObject("Sliding Area", typeof(RectTransform));
                sliding.transform.SetParent(barGo.transform, false);
                var slide = (RectTransform)sliding.transform;
                slide.anchorMin = Vector2.zero;
                slide.anchorMax = Vector2.one;
                slide.offsetMin = new Vector2(1f, 1f);
                slide.offsetMax = new Vector2(-1f, -1f);
                var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                handleGo.transform.SetParent(sliding.transform, false);
                var handle = (RectTransform)handleGo.transform;
                handle.anchorMin = Vector2.zero;
                handle.anchorMax = Vector2.one;
                handle.offsetMin = Vector2.zero;
                handle.offsetMax = Vector2.zero;
                var handleImage = handleGo.GetComponent<Image>();
                handleImage.color = new Color(0.886f, 0.812f, 0.686f, 1f);
                var scrollbar = barGo.GetComponent<Scrollbar>();
                scrollbar.handleRect = handle;
                scrollbar.targetGraphic = handleImage;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scroll.vertical = true;
                scroll.verticalScrollbar = scrollbar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            var pose = _commentsPanel.GetComponent<CozyJournalStaticPose>() ?? _commentsPanel.AddComponent<CozyJournalStaticPose>();
            pose.Configure(0f, _commentsPanel.GetComponentsInChildren<Graphic>(true));
            pose.PlayReveal(Vector2.zero, CozyMotion.Snap, .3f);
        }

        void RenderPost()
        {
            if (_post == null)
                return;

            var journal = transform.Find("JournalCatch") != null;
            if (_authorText != null)
                _authorText.text = (_post.nickname ?? DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友")) + (journal ? "" : " · " + FormatTime(_post.createdAt));
            if (journal)
            {
                CozyItemArt.Set(transform, "Header/JournalAvatar", DesktopDefaultAvatars.Portrait(_post.authorAvatarUrl, _post.playerId));
                CozyItemArt.Text(transform, "Header/JournalTime", FormatTime(_post.createdAt));
                StyleVisibility(_post.visibility == "private" ? DesktopLocalization.Text("ui.social.post_card.visibility.private", "仅自己") : _post.visibility == "friends" ? DesktopLocalization.Text("ui.social.post_card.visibility.friends", "仅好友") : DesktopLocalization.Text("ui.social.post_card.visibility.public", "公开"));
                StyleCatchPlate();
                CozyItemArt.Set(transform, "JournalCatch/Art", _post.fish != null ? CozyItemArt.Fish(_post.fish.speciesId) : null);
                CozyItemArt.Text(transform, "JournalCatch/Name", _post.fish != null ? DesktopFishCatalog.SpeciesName(_post.fish.speciesId) : DesktopLocalization.Text("ui.social.post_card.label.catch_record", "鱼获记录"));
                var catchName = transform.Find("JournalCatch/Name")?.GetComponent<Text>();
                if (catchName != null) catchName.fontSize = 20;
                CozyItemArt.Text(transform, "JournalCatch/Pond", PondLabel(_post.fish != null ? _post.fish.pondId : null));
                var pondLabel = transform.Find("JournalCatch/Pond")?.GetComponent<Text>();
                if (pondLabel != null)
                {
                    pondLabel.fontSize = 14;
                    pondLabel.alignment = TextAnchor.MiddleRight;
                    pondLabel.color = new Color(174f / 255f, 142f / 255f, 106f / 255f);
                    var pondRect = pondLabel.rectTransform;
                    pondRect.anchorMin = pondRect.anchorMax = new Vector2(1f, 0.5f);
                    pondRect.pivot = new Vector2(1f, 0.5f);
                    pondRect.anchoredPosition = new Vector2(-16f, 0f);
                    pondRect.sizeDelta = new Vector2(140f, 28f);
                }
            }
            if (_bodyText != null)
            {
                _bodyText.supportRichText = false;
                _bodyText.text = _post.text ?? DesktopLocalization.Text("ui.social.post_card.fallback.story", "分享了一条鱼获");
            }
            if (_fishInfoText != null)
            {
                var fish = _post.fish;
                _fishInfoText.text = DesktopLocalization.Format("ui.social.post_card.label.catch_summary", (object)((fish != null ? DesktopFishCatalog.SpeciesName(fish.speciesId) : DesktopLocalization.Text("ui.social.post_card.fallback.unknown", "未知"))), (object)((fish != null ? DesktopFishCatalog.QualityName(fish.quality) : DesktopLocalization.Text("ui.social.post_card.fallback.unknown", "未知"))), (object)((fish != null ? fish.sizeM.ToString("0.00") : "-")), (object)((fish != null
                        ? DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(fish.sizeM))
                        : "-")), (object)((_post.visibility == "friends" ? DesktopLocalization.Text("ui.social.post_card.visibility.friends", "仅好友") : DesktopLocalization.Text("ui.social.post_card.visibility.everyone", "所有人"))));
            }
            if (journal && _fishInfoText != null)
            {
                _fishInfoText.supportRichText = true;
                _fishInfoText.fontSize = 15;
                _fishInfoText.color = new Color(0.55f, 0.45f, 0.35f, 1f);
                _fishInfoText.text = _post.fish != null
                    ? _post.fish.sizeM.ToString("0.00") + " m · " + DesktopFishCatalog.StarRich(_post.fish.quality)
                    : "";
            }
            StyleQuiet(_likeButton, false);
            StyleQuiet(_commentsButton, false);
            if (_likeButton != null)
                SetButtonText(_likeButton,
                    (_post.likedByMe ? DesktopLocalization.Text("ui.social.post_card.button.liked", "♥ 已赞 ") : DesktopLocalization.Text("ui.social.post_card.button.like", "♡ 点赞 ")) + _post.likeCount);
            if (_commentsButton != null)
                SetButtonText(_commentsButton, DesktopLocalization.Format("ui.social.post_card.button.comment_count", (object)(_post.commentCount)));
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
            SetButtonText(_sendCommentButton, transform.Find("JournalCatch") != null ? DesktopLocalization.Text("ui.social.post_card.placeholder.encouragement", "留下鼓励") : DesktopLocalization.Text("ui.social.post_card.button.send_comment", "发送评论"));
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
                FitCatchWidth(journalCatch, -catchTop);
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
            SetButtonText(_likeButton, (liked ? DesktopLocalization.Text("ui.social.post_card.button.liked", "♥ 已赞 ") : DesktopLocalization.Text("ui.social.post_card.button.like", "♡ 点赞 ")) + count);
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
            var visual = _likeButton.GetComponent<CozyButtonVisual>();
            if (visual != null) visual.SetFeedLike(_post.likedByMe);
        }

        void CommentStatus(string value)
        {
            if(_commentsPanel == null) return;
            var text = _commentsPanel.transform.Find("CommentStatus")?.GetComponent<Text>();
            if (text != null)
            {
                text.text = value ?? "";
                text.fontSize = 15;
                text.alignment = TextAnchor.MiddleCenter;
                var status = text.rectTransform;
                status.anchorMin = new Vector2(0f, 1f);
                status.anchorMax = new Vector2(1f, 1f);
                status.pivot = new Vector2(0.5f, 1f);
                status.anchoredPosition = new Vector2(0f, -72f);
                status.sizeDelta = new Vector2(-24f, 48f);
                text.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }
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
            SetButtonText(_likeButton, (_post.likedByMe ? DesktopLocalization.Text("ui.social.post_card.button.liked", "♥ 已赞 ") : DesktopLocalization.Text("ui.social.post_card.button.like", "♡ 点赞 ")) + _post.likeCount);
        }

        public void ApplyCommented(PostCommentedDto message)
        {
            if (_post == null || message == null || message.postId != _post.id || message.comment == null) return;
            if (!_socketComments.Add(message.comment.id ?? string.Empty)) return;
            if (!_commentRows.ContainsKey(message.comment.id ?? string.Empty))
                _post.commentCount++;
            SetButtonText(_commentsButton, DesktopLocalization.Format("ui.social.post_card.button.comment_count", (object)(_post.commentCount)));
            CommentCountChanged?.Invoke(_post.commentCount);
            if (_commentsPanel != null && _commentsPanel.activeSelf) AddCommentRow(message.comment);
        }

        public void ApplyCommentDeleted(PostCommentDeletedDto message)
        {
            if (_post == null || message == null || message.postId != _post.id) return;
            _post.commentCount = message.commentCount;
            SetButtonText(_commentsButton, DesktopLocalization.Format("ui.social.post_card.button.comment_count", (object)(_post.commentCount)));
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
            FitCommentChrome();
            _commentsRequestVersion++;
            var requestVersion = _commentsRequestVersion;
            _commentsLoadFailed = false;
            ClearComments();
            _commentsPanel.SetActive(true);
            RevealComments();
            CommentStatus(DesktopLocalization.Text("ui.social.post_card.status.loading_comments", "正在加载留言…"));
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
                        CommentStatus(DesktopLocalization.Text("ui.social.post_card.error.comment_load_failed", "留言加载失败，点击卡片重试。"));
                        SetButtonText(_commentsButton, DesktopLocalization.Text("ui.social.post_card.button.retry_comments", "评论加载失败 · 重试"));
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
            FitCommentChrome();
            _commentsLoadFailed=false;ClearComments();_commentsPanel.SetActive(true);
            RevealComments();
            _post.commentCount=totalCount;CommentCountChanged?.Invoke(totalCount);
            SetButtonText(_commentsButton,DesktopLocalization.Format("ui.social.post_card.button.comment_count", (object)(totalCount)));
            CommentStatus(comments==null||comments.Length==0?DesktopLocalization.Text("ui.social.post_card.empty.comments", "还没有留言\n给钓友留一句鼓励吧"):"");
            for(var i=0;comments!=null&&i<comments.Length;i++)AddCommentRow(comments[i]);
            RefreshCardLayout();
        }

        void FitCommentChrome()
        {
            if (_commentsPanel == null || _commentsHost == null) return;
            var rect = _commentsPanel.transform as RectTransform;
            rect.SetParent(_commentsHost, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            var viewport = _commentsPanel.transform.Find("CommentsViewport") as RectTransform;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.pivot = new Vector2(0.5f, 0.5f);
                viewport.offsetMin = new Vector2(0f, 106f);
                viewport.offsetMax = Vector2.zero;
            }
            // Preview form is left 18 / right 18 on the aside, not stretched across the comment host.
            PlaceFormControl(_commentInput != null ? _commentInput.transform as RectTransform : null, 369f, 39f);
            PlaceFormControl(_sendCommentButton != null ? _sendCommentButton.transform as RectTransform : null, 418f, 43f);
            StyleCommentField(_commentInput);
            if (_sendCommentButton != null)
            {
                _sendCommentButton.GetComponent<CozyButtonVisual>()?.SetCommentSend();
                var group = _sendCommentButton.GetComponent<LayoutGroup>();
                if (group != null) group.enabled = false;
                foreach (var caption in _sendCommentButton.GetComponentsInChildren<Text>(true))
                {
                    var captionRect = caption.rectTransform;
                    var captionLayout = caption.GetComponent<LayoutElement>();
                    if (captionLayout != null) captionLayout.ignoreLayout = true;
                    captionRect.anchorMin = Vector2.zero;
                    captionRect.anchorMax = Vector2.one;
                    captionRect.offsetMin = Vector2.zero;
                    captionRect.offsetMax = Vector2.zero;
                    caption.alignment = TextAnchor.MiddleCenter;
                }
            }
            if (_commentsContent != null)
            {
                var rows = _commentsContent.GetComponent<VerticalLayoutGroup>();
                if (rows != null) rows.spacing = 10f;
            }
        }

        void PlaceFormControl(RectTransform rect, float top, float height)
        {
            if (rect == null || _commentsHost == null) return;
            var aside = _commentsHost.parent as RectTransform;
            var asideWidth = aside != null && aside.rect.width > 64f ? aside.rect.width : 362f;
            var width = asideWidth - 36f;
            var hostHeight = HostSpan(_commentsHost, false);
            var hostLeft = _commentsHost.anchoredPosition.x - HostSpan(_commentsHost, true) * _commentsHost.pivot.x;
            var hostTop = -_commentsHost.anchoredPosition.y - hostHeight * (1f - _commentsHost.pivot.y);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(18f - hostLeft, hostTop - top);
            var layout = rect.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
                layout.flexibleWidth = 0f;
                layout.preferredWidth = width;
            }
        }

        static float HostSpan(RectTransform host, bool horizontal)
        {
            var live = horizontal ? host.rect.width : host.rect.height;
            var authored = horizontal ? host.sizeDelta.x : host.sizeDelta.y;
            return live > 1f ? live : authored;
        }

        static void StyleCommentField(InputField input)
        {
            if (input == null) return;
            var image = input.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Color.clear;
            }
            var plate = input.transform.Find("InputPlate");
            if (plate == null)
            {
                var go = new GameObject("InputPlate", typeof(RectTransform), typeof(CozyCssGraphic));
                go.transform.SetParent(input.transform, false);
                plate = go.transform;
            }
            plate.SetAsFirstSibling();
            var plateRect = (RectTransform)plate;
            plateRect.anchorMin = Vector2.zero;
            plateRect.anchorMax = Vector2.one;
            plateRect.offsetMin = plateRect.offsetMax = Vector2.zero;
            var wash = plate.GetComponent<CozyCssGraphic>();
            if (wash != null)
            {
                wash.shape = CozyCssGraphic.Shape.Paper;
                wash.radius = 10f;
                wash.border = 2f;
                wash.borderColor = new Color(230f / 255f, 212f / 255f, 181f / 255f);
                wash.color = new Color(1f, 253f / 255f, 247f / 255f);
                wash.raycastTarget = false;
            }
            foreach (var caption in input.GetComponentsInChildren<Text>(true))
            {
                var captionRect = caption.rectTransform;
                captionRect.anchorMin = Vector2.zero;
                captionRect.anchorMax = Vector2.one;
                captionRect.offsetMin = new Vector2(13f, 8f);
                captionRect.offsetMax = new Vector2(-13f, -8f);
                caption.fontSize = 15;
            }
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
            var bodyHeight = 24f;
            if (label != null)
            {
                label.supportRichText = false;
                var author = row.transform.Find("Author")?.GetComponent<Text>();
                if(author != null) { author.supportRichText=false;author.text=comment.nickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友");label.text=comment.text??""; }
                else label.text = (comment.nickname ?? DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友")) + "：" + comment.text;
                var width = _commentsContent.rect.width > 32f ? _commentsContent.rect.width - 24f : 298f;
                bodyHeight = CozySocialText.Apply(label, 15, 24, width);
                var layout=row.GetComponent<LayoutElement>();if(layout!=null)layout.preferredHeight=Mathf.Max(64f,bodyHeight+49f);
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
            RestyleCommentRow(row, label, delete, bodyHeight);
            if (delete != null &&
                (_api == null || (_api.PlayerId != comment.playerId && _api.PlayerId != _post.playerId)))
            {
                delete.gameObject.SetActive(false);
                var author = row.transform.Find("Author") as RectTransform;
                if (author != null) author.offsetMax = new Vector2(-12f, author.offsetMax.y);
            }
            if (delete != null)
            {
                delete.onClick.RemoveAllListeners();
                delete.onClick.AddListener(() => DeleteComment(comment, row, delete));
            }
        }

        static void RestyleCommentRow(GameObject row, Text label, Button delete, float bodyHeight)
        {
            DesktopModalUi.StyleTree(row.transform);
            var plate = row.GetComponent<Image>();
            if (plate != null)
            {
                plate.sprite = null;
                plate.type = Image.Type.Simple;
                plate.color = Color.clear;
            }
            var washNode = row.transform.Find("CommentWash");
            if (washNode == null)
            {
                var go = new GameObject("CommentWash", typeof(RectTransform), typeof(CozyCssGraphic));
                go.transform.SetParent(row.transform, false);
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
                wash.radius = 10f;
                wash.border = 1f;
                wash.borderColor = new Color(234f / 255f, 219f / 255f, 195f / 255f);
                wash.color = new Color(1f, 249f / 255f, 238f / 255f);
                wash.raycastTarget = false;
            }
            var author = row.transform.Find("Author") as RectTransform;
            if (author != null)
            {
                author.anchorMin = new Vector2(0f, 1f);
                author.anchorMax = new Vector2(1f, 1f);
                author.pivot = new Vector2(0f, 1f);
                author.offsetMin = new Vector2(12f, -32f);
                author.offsetMax = new Vector2(-56f, -11f);
                var authorText = author.GetComponent<Text>();
                if (authorText != null)
                {
                    authorText.fontSize = 15;
                    authorText.color = new Color(158f / 255f, 117f / 255f, 79f / 255f);
                    authorText.alignment = TextAnchor.MiddleLeft;
                }
            }
            if (label != null)
            {
                var body = label.rectTransform;
                body.anchorMin = new Vector2(0f, 1f);
                body.anchorMax = new Vector2(1f, 1f);
                body.pivot = new Vector2(0f, 1f);
                body.offsetMin = new Vector2(12f, -38f - Mathf.Max(24f, bodyHeight));
                body.offsetMax = new Vector2(-12f, -38f);
                label.fontSize = 15;
                label.color = new Color(117f / 255f, 71f / 255f, 46f / 255f);
                label.alignment = TextAnchor.UpperLeft;
            }
            if (delete != null)
            {
                var deleteRect = delete.transform as RectTransform;
                deleteRect.anchorMin = deleteRect.anchorMax = new Vector2(1f, 1f);
                deleteRect.pivot = new Vector2(1f, 1f);
                deleteRect.anchoredPosition = new Vector2(-8f, -8f);
                deleteRect.sizeDelta = new Vector2(42f, 24f);
                var deleteLayout = delete.GetComponent<LayoutElement>();
                if (deleteLayout != null) deleteLayout.ignoreLayout = true;
                SetButtonText(delete, DesktopLocalization.Text("ui.social.post_card.button.delete_comment", "删除"));
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
                    if (this == null || delete == null) return;
                    if (!ok)
                    {
                        delete.interactable = true;
                        return;
                    }
                    _post.commentCount = count;
                    CommentCountChanged?.Invoke(count);
                    SetButtonText(_commentsButton, DesktopLocalization.Format("ui.social.post_card.button.comment_count", (object)(count)));
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
            if (string.IsNullOrEmpty(value) || value.Length > 200) { CommentStatus(DesktopLocalization.Text("ui.social.post_card.error.comment_length", "请输入 1–200 字的留言。")); return; }
            _sendCommentButton.interactable = false;
            StartCoroutine(_api.AddPostComment(_post.id, value,
                (ok, comment, count, message) =>
                {
                    if (this == null || _sendCommentButton == null) return;
                    _sendCommentButton.interactable = true;
                    SetButtonText(_sendCommentButton, ok ? DesktopLocalization.Text("ui.social.post_card.placeholder.encouragement", "留下鼓励") : DesktopLocalization.Text("ui.social.post_card.error.comment_send_retry", "发送失败 · 重试"));
                    CommentStatus(ok ? "" : DesktopLocalization.Text("ui.social.post_card.error.comment_send_preserved", "发送失败，留言文字已保留。"));
                    if (!ok)
                        return;
                    _post.commentCount = count;
                    CommentCountChanged?.Invoke(count);
                    SetButtonText(_commentsButton, DesktopLocalization.Format("ui.social.post_card.button.comment_count", (object)(count)));
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
            return timestamp <= 0 ? DesktopLocalization.Text("ui.social.feed.label.just_now", "刚刚") :
                DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime
                    .ToString("yyyy-MM-dd HH:mm");
        }
    }
}
