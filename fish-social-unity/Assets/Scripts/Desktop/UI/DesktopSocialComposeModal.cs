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
    public sealed class DesktopSocialComposeModal : MonoBehaviour
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
        [SerializeField] Button _public;
        [SerializeField] Button _friends;
        [SerializeField] Transform _fishList;
        GameObject _previousFocus;
        readonly List<Button> _fishChoices = new List<Button>();

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
                _close == null || _public == null || _friends == null || _fishList == null) return false;
            _close.onClick.RemoveAllListeners();
            _close.onClick.AddListener(() => Close());
            _public.onClick.RemoveAllListeners();
            _public.onClick.AddListener(() => SelectVisibility("public"));
            _friends.onClick.RemoveAllListeners();
            _friends.onClick.AddListener(() => SelectVisibility("friends"));
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
            _story.SetTextWithoutNotify(string.Empty);
            _counter.text = "0 / " + StoryLimit;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            StartCoroutine(LoadFish());
        }

        public void Close(bool force = false)
        {
            if (_busy && !force) return;
            StopAllCoroutines();
            _busy = _loading = false;
            gameObject.SetActive(false);
            if (_previousFocus != null && _previousFocus.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_previousFocus);
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
            _fishChoices.Clear();
            DesktopModalUi.Clear(_fishList);
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
            foreach (var item in fish ?? Array.Empty<FishInventoryItemDto>())
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                var id = item.id;
                if (_fishId == null) _fishId = id;
                var button = DesktopModalUi.MakeButton(_fishList, id,
                    DesktopFishCatalog.SpeciesName(item.speciesId) + " · " + item.sizeM.ToString("0.00") + " m · " + DesktopFishCatalog.QualityName(item.quality),
                    () => { _fishId = id; RefreshControls(); });
                button.GetComponent<LayoutElement>().minHeight = button.GetComponent<LayoutElement>().preferredHeight = 48;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                foreach (var label in button.GetComponentsInChildren<Text>()) label.fontSize = 18;
                _fishChoices.Add(button);
            }
            _status.text = _fishChoices.Count == 0 ? "背包还没有鱼获，先去湖边钓一条吧。" : "分享会保留背包里的鱼获。留空心情将使用鱼获描述。";
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
            _public.interactable = _friends.interactable = !_busy;
            _public.GetComponent<CozyButtonVisual>()?.SetSelected(_visibility == "public");
            _friends.GetComponent<CozyButtonVisual>()?.SetSelected(_visibility == "friends");
            foreach (var button in _fishChoices)
            {
                button.interactable = !_busy && !_loading;
                button.GetComponent<CozyButtonVisual>()?.SetSelected(button.name == _fishId);
            }
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
