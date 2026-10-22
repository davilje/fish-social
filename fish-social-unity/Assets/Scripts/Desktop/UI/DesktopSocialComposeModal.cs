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
                RefreshControls();
            });
            _visibilityChoice.onValueChanged.RemoveAllListeners();
            _visibilityChoice.onValueChanged.AddListener(index => SelectVisibility(index == 1 ? "friends" : "public"));
            _publish.onClick.RemoveAllListeners();
            _publish.onClick.AddListener(Submit);
            _story.onValueChanged.RemoveAllListeners();
            _story.onValueChanged.AddListener(value => _counter.text = value.Length + " / " + StoryLimit);
            return true;
        }

        public void Open(IAuthenticatedApiClient api, string nickname, Action published)
        {
            if (_busy) return;
            StopAllCoroutines();
            _api = api;
            _nickname = nickname;
            _published = published;
            _previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _busy = false;
            _fishId = null;
            _visibility = "public";
            _visibilityChoice.SetValueWithoutNotify(0);
            _visibilityChoice.RefreshShownValue();
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

        void OnDisable() { ResetOpenMotion(); }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left &&
                eventData.pointerCurrentRaycast.gameObject == gameObject)
                Close();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
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
            var choices = new List<Dropdown.OptionData>();
            foreach (var item in fish ?? Array.Empty<FishInventoryItemDto>())
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                _fishIds.Add(item.id);
                choices.Add(new Dropdown.OptionData(DesktopFishCatalog.SpeciesName(item.speciesId) + " · " +
                    item.sizeM.ToString("0.00") + " m · " + DesktopFishCatalog.QualityName(item.quality)));
            }
            _fishId = _fishIds.Count > 0 ? _fishIds[0] : null;
            _fishChoice.ClearOptions();
            if (choices.Count == 0) choices.Add(new Dropdown.OptionData("背包还没有鱼获"));
            _fishChoice.AddOptions(choices);
            _fishChoice.SetValueWithoutNotify(0);
            _fishChoice.RefreshShownValue();
            _status.text = _fishIds.Count == 0 ? "背包还没有鱼获，先去湖边钓一条吧。" : "分享会保留背包里的鱼获。留空心情将使用鱼获描述。";
            RefreshControls();
        }

        void SelectVisibility(string visibility)
        {
            if (_busy) return;
            _visibility = visibility;
            RefreshControls();
        }

        void RefreshControls()
        {
            _story.interactable = !_busy;
            _close.interactable = !_busy;
            _visibilityChoice.interactable = !_busy;
            _fishChoice.interactable = !_busy && !_loading && _fishIds.Count > 0;
            _publish.interactable = !_busy && !_loading && (_loadFailed || !string.IsNullOrEmpty(_fishId));
            DesktopModalUi.SetButtonLabel(_publish, _busy ? "正在发布…" : _loading ? "正在读取鱼获…" : _loadFailed ? "重试加载" : "贴进动态手账");
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

    }
}
