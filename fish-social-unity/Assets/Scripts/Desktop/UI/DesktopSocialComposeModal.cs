using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>Native journal composer. Inventory and publication remain server authoritative.</summary>
    public sealed class DesktopSocialComposeModal : MonoBehaviour, IPointerClickHandler
    {
        public const int StoryLimit = 160;
        IAuthenticatedApiClient _api;
        Action _published;
        string _nickname;
        string _fishId;
        string _visibility = "public";
        bool _busy;
        bool _loading;
        bool _loadFailed;
        [SerializeField] Text _status;
        [SerializeField] Text _counter;
        [SerializeField] InputField _story;
        [SerializeField] Button _publish;
        [SerializeField] Button _close;
        [SerializeField] Dropdown _fishChoice;
        [SerializeField] Dropdown _visibilityChoice;
        readonly List<FishInventoryItemDto> _inventory = new List<FishInventoryItemDto>();
        readonly List<Button> _cards = new List<Button>();
        Button _cancel, _visibilityPublic, _visibilityFriends;
        Transform _cardContent;
        Button _cardTemplate;
        string _preferredFishId;
        GameObject _previousFocus;
        RectTransform _paper;
        CanvasGroup _paperAlpha;
        Vector2 _paperRest;
        readonly List<string> _fishIds = new List<string>();

        public const string ResourcePath = "Desktop/Prefabs/JournalComposeModal";

        public static DesktopSocialComposeModal Create(Transform canvas)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError("Missing journal composer prefab: " + ResourcePath);
                return null;
            }
            var root = Instantiate(prefab, canvas, false);
            root.name = "JournalComposeModal";
            root.SetActive(false);
            var modal = root.GetComponent<DesktopSocialComposeModal>();
            if (modal != null && modal.BindControls()) return modal;
            Debug.LogError("Journal composer prefab has missing serialized controls.", root);
            Destroy(root);
            return null;
        }

        // Layout and control references are authored and saved in the prefab. Only inventory rows are dynamic.
        public bool BindControls()
        {
            if (_status == null || _counter == null || _story == null || _publish == null ||
                _close == null || _fishChoice == null || _visibilityChoice == null) return false;
            if (_paper == null)
            {
                _paper = transform.Find("ModalPanel") as RectTransform;
                if (_paper == null) return false;
                _paperRest = _paper.anchoredPosition;
                _paperAlpha = _paper.GetComponent<CanvasGroup>();
                if (_paperAlpha == null) _paperAlpha = _paper.gameObject.AddComponent<CanvasGroup>();
            }
            _close.onClick.RemoveAllListeners();
            _close.onClick.AddListener(() => Close());
            _fishChoice.onValueChanged.RemoveAllListeners();
            _fishChoice.onValueChanged.AddListener(index =>
            {
                _fishId = index >= 0 && index < _fishIds.Count ? _fishIds[index] : null;
                RefreshCatchPreview();
                RefreshControls();
            });
            _visibilityChoice.onValueChanged.RemoveAllListeners();
            _visibilityChoice.onValueChanged.AddListener(index => SelectVisibility(index == 1 ? "friends" : "public"));
            _publish.onClick.RemoveAllListeners();
            _publish.onClick.AddListener(Submit);
            _story.onValueChanged.RemoveAllListeners();
            _story.onValueChanged.AddListener(value => _counter.text = value.Length + " / " + StoryLimit);
            _cancel = _paper.Find("Cancel")?.GetComponent<Button>();
            _visibilityPublic = _paper.Find("VisibilityPublic")?.GetComponent<Button>();
            _visibilityFriends = _paper.Find("VisibilityFriends")?.GetComponent<Button>();
            _cardContent = _paper.Find("FishCards/Content");
            _cardTemplate = _cardContent?.Find("FishCardTemplate")?.GetComponent<Button>();
            BindClick(_cancel, () => Close());
            BindClick(_visibilityPublic, () => SetVisibility("public"));
            BindClick(_visibilityFriends, () => SetVisibility("friends"));
            return true;
        }

        public void Open(IAuthenticatedApiClient api, string nickname, Action published, string avatarId = null, string selectedFishId = null)
        {
            if (_busy) return;
            StopAllCoroutines();
            _api = api;
            _nickname = nickname;
            _preferredFishId = selectedFishId;
            PresentAuthor(nickname, avatarId);
            _published = published;
            _previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _busy = false;
            _fishId = null;
            _visibility = "public";
            _visibilityChoice.SetValueWithoutNotify(0);
            _visibilityChoice.RefreshShownValue();
            SelectVisibility("public");
            _story.SetTextWithoutNotify(string.Empty);
            _counter.text = "0 / " + StoryLimit;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            PlayOpenMotion();
            EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            StartCoroutine(LoadFish());
        }

        public void Close(bool force = false)
        {
            if (_busy && !force) return;
            StopAllCoroutines();
            _busy = _loading = false;
            ResetOpenMotion();
            _fishChoice.Hide();
            _visibilityChoice.Hide();
            gameObject.SetActive(false);
            if (_previousFocus != null && _previousFocus.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_previousFocus);
        }

        public void PlayOpenMotion()
        {
            if (_paper == null && !BindControls()) return;
            ResetOpenMotion();
            var offset = CozyMotion.Reduced ? 0 : 16;
            _paper.anchoredPosition = _paperRest + Vector2.down * offset;
            _paperAlpha.alpha = 0;
            CozyMotion.Animate(_paper, .2f, t =>
            {
                if (_paper == null || _paperAlpha == null) return;
                _paper.anchoredPosition = _paperRest + Vector2.down * (offset * (1 - t));
                _paperAlpha.alpha = Mathf.Clamp01(t);
            }, RestorePaperPose, CozyWebMotion.Ease.OutCubic, cleanup: RestorePaperPose);
        }

        void RestorePaperPose()
        {
            if (_paper != null) _paper.anchoredPosition = _paperRest;
            if (_paperAlpha != null) _paperAlpha.alpha = 1;
        }

        void ResetOpenMotion()
        {
            if (_paper != null) CozyMotion.Cancel(_paper);
            RestorePaperPose();
        }

        void OnDisable()
        {
            ResetOpenMotion();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left &&
                eventData.pointerCurrentRaycast.gameObject == gameObject)
                Close();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            if (!Input.GetKeyDown(KeyCode.Tab) || EventSystem.current == null) return;
            var choices = new List<Selectable>();
            foreach (var selectable in GetComponentsInChildren<Selectable>())
                if (selectable.IsInteractable()) choices.Add(selectable);
            if (choices.Count == 0) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            var index = choices.FindIndex(choice => choice.gameObject == selected);
            var direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            index = (index + direction + choices.Count) % choices.Count;
            EventSystem.current.SetSelectedGameObject(choices[index].gameObject);
        }

        IEnumerator LoadFish()
        {
            _loading = true;
            _loadFailed = false;
            _status.text = "正在读取背包鱼获…";
            _fishIds.Clear();
            _inventory.Clear();
            ClearCards();
            RefreshCatchPreview();
            _fishChoice.ClearOptions();
            _fishChoice.AddOptions(new List<string> { "正在读取背包鱼获…" });
            RefreshControls();
            if (_api == null || !_api.CanUse)
            {
                _loading = false;
                _loadFailed = true;
                _status.text = "当前会话已失效，请重新登录。";
                RefreshControls();
                yield break;
            }
            FishInventoryItemDto[] fish = null;
            var ok = false;
            string error = null;
            yield return _api.GetInventoryItems((success, items, message) => { ok = success; fish = items; error = message; });
            _loading = false;
            if (!ok)
            {
                _loadFailed = true;
                _status.text = error ?? "背包加载失败，请重试。";
                RefreshControls();
                yield break;
            }
            PresentInventory(fish, _preferredFishId);
        }

        /// <summary>Bind the real inventory response; never cap or duplicate the user's catches.</summary>
        public void PresentInventory(FishInventoryItemDto[] fish, string selectedFishId = null)
        {
            _fishIds.Clear(); _inventory.Clear(); ClearCards();
            var choices = new List<Dropdown.OptionData>();
            foreach (var item in fish ?? Array.Empty<FishInventoryItemDto>())
            {
                if (item == null || string.IsNullOrEmpty(item.id) || _fishIds.Contains(item.id)) continue;
                _fishIds.Add(item.id); _inventory.Add(item);
                choices.Add(new Dropdown.OptionData(DesktopFishCatalog.SpeciesName(item.speciesId) + " · " + item.sizeM.ToString("0.00") + " m"));
            }
            var requestedIndex = _fishIds.IndexOf(selectedFishId ?? "");
            var missingRequested = !string.IsNullOrEmpty(selectedFishId) && requestedIndex < 0;
            var selected = Mathf.Max(0, requestedIndex);
            _fishId = !missingRequested && _fishIds.Count > 0 ? _fishIds[selected] : null;
            _fishChoice.ClearOptions();
            if (choices.Count == 0) choices.Add(new Dropdown.OptionData("背包还没有鱼获"));
            _fishChoice.AddOptions(choices); _fishChoice.SetValueWithoutNotify(selected); _fishChoice.RefreshShownValue();
            _loading = _loadFailed = false;
            _status.text = missingRequested ? "这条鱼获已不在背包中，请重新选择要分享的鱼获。" : _fishIds.Count == 0 ? "背包还没有鱼获，先去湖边钓一条吧。" : "";
            if (_cardTemplate != null)
            {
                for (var i = 0; i < _inventory.Count; i++)
                {
                    var index = i; var item = _inventory[i];
                    var card = Instantiate(_cardTemplate, _cardContent, false);
                    card.name = "FishCard_" + i; card.gameObject.SetActive(true);
                    var name = card.transform.Find("Name")?.GetComponent<Text>();
                    if (name != null) name.text = DesktopFishCatalog.SpeciesName(item.speciesId);
                    var art = card.transform.Find("Art")?.GetComponent<Image>();
                    if (art != null) { art.sprite = CozyItemArt.Fish(item.speciesId); art.enabled = art.sprite != null; }
                    card.onClick.RemoveAllListeners();
                    card.onClick.AddListener(() => { if (!_busy && !_loading) SelectFish(index); });
                    _cards.Add(card);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_cardContent);
                var scroll = _cardContent.GetComponentInParent<ScrollRect>();
                if (scroll != null) scroll.verticalNormalizedPosition = 1;
            }
            RefreshCatchPreview(); RefreshControls();
        }

        public void PresentPreview(FishInventoryItemDto[] fish, string nickname, string avatarId = null, string story = null)
        {
            if (!BindControls()) return;
            _nickname = nickname; _busy = _loading = _loadFailed = false;
            PresentAuthor(nickname, avatarId);
            _story.SetTextWithoutNotify(story ?? ""); _counter.text = _story.text.Length + " / " + StoryLimit;
            SetVisibility("public"); PresentInventory(fish); RestorePaperPose();
        }

        void SelectFish(int index)
        {
            if (index < 0 || index >= _inventory.Count) return;
            _fishChoice.SetValueWithoutNotify(index); _fishChoice.RefreshShownValue();
            _fishId = _inventory[index].id; _status.text = ""; RefreshCatchPreview(); RefreshControls();
        }

        void ClearCards()
        {
            foreach (var card in _cards) if (card != null) { card.gameObject.SetActive(false); DestroyUi(card.gameObject); }
            _cards.Clear();
        }

        void PresentAuthor(string nickname, string avatarId)
        {
            if (_paper == null) return;
            SetText("AuthorName", string.IsNullOrEmpty(nickname) ? "我的钓鱼手账" : nickname);
            var avatar = _paper.Find("AuthorAvatar")?.GetComponent<Image>();
            if (avatar != null) { avatar.sprite = DesktopDefaultAvatars.Portrait(avatarId); avatar.enabled = avatar.sprite != null; }
        }

        void RefreshCatchPreview()
        {
            var index = _fishIds.IndexOf(_fishId ?? "");
            var item = index >= 0 && index < _inventory.Count ? _inventory[index] : null;
            var art = _paper?.Find("CatchPreview/FishArt")?.GetComponent<Image>();
            if (art != null) { art.sprite = item == null ? null : CozyItemArt.Fish(item.speciesId); art.enabled = art.sprite != null; }
            SetText("CatchPreview/FishName", item == null ? "挑一条今天的鱼获" : DesktopFishCatalog.SpeciesName(item.speciesId));
            SetText("CatchPreview/Size", item == null ? "—" : item.sizeM.ToString("0.00") + " m");
            SetText("CatchPreview/Weight", item == null ? "—" : DesktopGameData.CalcWeightKg(item.sizeM).ToString("0.00") + " kg");
            var timestamp = "记录时间未知";
            if (item != null && item.caughtAt > 0)
            {
                try { timestamp = DateTimeOffset.FromUnixTimeMilliseconds(item.caughtAt).LocalDateTime.ToString("yyyy.MM.dd HH:mm:ss"); }
                catch (ArgumentOutOfRangeException) { }
            }
            var pond = item == null ? null : DesktopGameData.GetPond(item.pondId)?.name;
            SetText("CatchPreview/CaughtAt", item == null ? "选择后查看记录时间与水域" : timestamp + " · " + (pond ?? "未知水域"));
            for (var i = 0; i < _cards.Count; i++) _cards[i].GetComponent<CozyButtonVisual>()?.SetSelected(i == index);
        }

        void SetText(string path, string value)
        {
            var text = _paper?.Find(path)?.GetComponent<Text>(); if (text != null) text.text = value;
        }

        static void BindClick(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null) return; button.onClick.RemoveAllListeners(); button.onClick.AddListener(callback);
        }

        void SetVisibility(string visibility)
        {
            if (_busy) return;
            _visibilityChoice.SetValueWithoutNotify(visibility == "friends" ? 1 : 0);
            _visibilityChoice.RefreshShownValue(); SelectVisibility(visibility);
        }

        void SelectVisibility(string visibility)
        {
            if (_busy) return;
            _visibility = visibility;
            SetText("VisibilityHint", visibility == "friends" ? "只出现在好友动态里，与熟悉的钓友分享。" : "出现在公开动态里，和更多钓友分享快乐。");
            _visibilityPublic?.GetComponent<CozyButtonVisual>()?.SetSelected(visibility == "public");
            _visibilityFriends?.GetComponent<CozyButtonVisual>()?.SetSelected(visibility == "friends");
            RefreshControls();
        }

        void RefreshControls()
        {
            _story.interactable = !_busy;
            _close.interactable = !_busy;
            if (_cancel != null) _cancel.interactable = !_busy;
            if (_visibilityPublic != null) _visibilityPublic.interactable = !_busy;
            if (_visibilityFriends != null) _visibilityFriends.interactable = !_busy;
            foreach (var card in _cards) if (card != null) card.interactable = !_busy && !_loading;
            _visibilityChoice.interactable = !_busy;
            _fishChoice.interactable = !_busy && !_loading && _fishIds.Count > 0;
            _publish.interactable = !_busy && !_loading && (_loadFailed || !string.IsNullOrEmpty(_fishId));
            DesktopModalUi.SetButtonLabel(_publish, _busy ? "正在发布…" : _loading ? "正在读取鱼获…" : _loadFailed ? "重试加载" : "发布动态");
        }

        void Submit()
        {
            if (_busy || _loading) return;
            if (_loadFailed) { _fishId = null; StartCoroutine(LoadFish()); return; }
            if (string.IsNullOrEmpty(_fishId) || _api == null || !_api.CanUse)
            {
                _status.text = "请选择鱼获，并确认已登录。";
                return;
            }
            StartCoroutine(Publish());
        }

        IEnumerator Publish()
        {
            _busy = true;
            _status.text = "正在把鱼获故事贴进手账…";
            RefreshControls();
            var ok = false;
            string error = null;
            yield return _api.ShareFish(_fishId, _nickname, _visibility, (_story.text ?? "").Trim(),
                (success, message) => { ok = success; error = message; });
            _busy = false;
            if (!ok)
            {
                _status.text = error ?? "发布失败，请重试。";
                RefreshControls();
                EventSystem.current?.SetSelectedGameObject(_publish.gameObject);
                yield break;
            }
            var completed = _published;
            Close();
            completed?.Invoke();
        }

        static void DestroyUi(GameObject node)
        {
            if (node == null)
                return;
            if (Application.isPlaying)
                Destroy(node);
            else
                DestroyImmediate(node);
        }

    }

}
