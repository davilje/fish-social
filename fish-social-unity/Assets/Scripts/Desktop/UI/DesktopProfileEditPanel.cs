using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Game-profile editor. Saves nickname/bio/avatar/showcase through REST
    /// and does not leave the pond or rebuild Overlay.
    /// </summary>
    public sealed class DesktopProfileEditPanel : MonoBehaviour
    {
        const int NicknameLimit = 12;
        const int BioLimit = 120;

        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        System.Action _openProfile;
        InputField _nickname;
        InputField _bio;
        Text _status;
        Button _save;
        Button _retry;
        Button _back;
        Button _clearSlot;
        Button _closePicker;
        Transform _avatars;
        Transform _showcase;
        Transform _pickerContent;
        GameObject _picker;
        string _avatarUrl = string.Empty;
        string[] _slots = new string[PlayerProfileDto.ShowcaseSlotCount];
        FishInventoryItemDto[] _inventory = new FishInventoryItemDto[0];
        int _pickingSlot = -1;
        bool _busy;
        bool _lastSaveFailed;
        bool _ready;
        Coroutine _loadRoutine;
        Coroutine _saveRoutine;

        public void Bind(
            IAuthenticatedApiClient api,
            SocialPondSessionController pond,
            System.Action openProfile)
        {
            _api = api;
            _pond = pond;
            _openProfile = openProfile;
            EnsureUi();
            SetStatus("打开后将加载当前服务端资料。");
        }

        public void OnOpened()
        {
            Refresh();
        }

        public void OnClosed()
        {
            StopRoutines();
            HidePicker();
        }

        public void BuildEditorLayout()
        {
            if (transform.Find("Header") == null)
                BuildFallbackUi();
        }

        void EnsureUi()
        {
            _nickname = Find<InputField>("Form/Nickname");
            _bio = Find<InputField>("Form/Bio");
            _status = Find<Text>("Status");
            _save = Find<Button>("Actions/Save");
            _retry = Find<Button>("Actions/Retry");
            _back = Find<Button>("Header/Back");
            _avatars = transform.Find("Avatars/Viewport/Content");
            _showcase = transform.Find("Showcase/Viewport/Content");
            _picker = transform.Find("Picker") != null ? transform.Find("Picker").gameObject : null;
            _pickerContent = transform.Find("Picker/Viewport/Content");
            _clearSlot = Find<Button>("Picker/Clear");
            _closePicker = Find<Button>("Picker/Close");
            if (_nickname == null || _bio == null || _status == null ||
                _save == null || _retry == null || _back == null ||
                _avatars == null || _showcase == null || _picker == null ||
                _pickerContent == null || _clearSlot == null || _closePicker == null)
                BuildFallbackUi();

            if (_nickname != null)
                _nickname.characterLimit = NicknameLimit;
            if (_bio != null)
            {
                _bio.characterLimit = BioLimit;
                _bio.lineType = InputField.LineType.MultiLineNewline;
            }

            _save.onClick.RemoveAllListeners();
            _save.onClick.AddListener(Save);
            _retry.onClick.RemoveAllListeners();
            _retry.onClick.AddListener(OnRetry);
            _back.onClick.RemoveAllListeners();
            _back.onClick.AddListener(GoBack);
            _clearSlot.onClick.RemoveAllListeners();
            _clearSlot.onClick.AddListener(ClearPickedSlot);
            _closePicker.onClick.RemoveAllListeners();
            _closePicker.onClick.AddListener(HidePicker);
            HidePicker();
        }

        void GoBack()
        {
            if (_openProfile != null)
                _openProfile();
        }

        void OnRetry()
        {
            if (_lastSaveFailed)
                Save();
            else
                Refresh();
        }

        void Refresh()
        {
            StopRoutines();
            _loadRoutine = StartCoroutine(LoadRoutine());
        }

        IEnumerator LoadRoutine()
        {
            _ready = false;
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前没有有效的 Steam 会话，请重新登录。");
                _loadRoutine = null;
                yield break;
            }

            SetStatus("正在加载可编辑资料…");
            ApplyProfile(DesktopProfileCache.Latest);

            var profileDone = false;
            var profileOk = false;
            string profileError = null;
            PlayerProfileDto profile = null;
            yield return _api.GetPlayerProfile((ok, loaded, message) =>
            {
                profileOk = ok;
                profile = loaded;
                profileError = message;
                profileDone = true;
            });
            while (!profileDone)
                yield return null;

            var inventoryDone = false;
            var inventoryOk = false;
            yield return _api.GetInventoryItems((ok, items, message) =>
            {
                inventoryOk = ok;
                if (ok)
                    _inventory = items ?? new FishInventoryItemDto[0];
                else if (profileError == null)
                    profileError = message;
                inventoryDone = true;
            });
            while (!inventoryDone)
                yield return null;

            if (!profileOk || profile == null)
            {
                _loadRoutine = null;
                SetStatus(profileError ?? "资料加载失败，请点击重试。");
                yield break;
            }

            if (!inventoryOk)
            {
                _loadRoutine = null;
                SetStatus(profileError ?? "背包数据加载失败，请点击重试。");
                yield break;
            }

            DesktopProfileCache.Latest = profile;
            if (_pond != null && !string.IsNullOrEmpty(profile.nickname))
                _pond.ApplyGameNickname(profile.nickname);
            ApplyProfile(profile);
            _ready = true;
            SetStatus("可以编辑游戏昵称、简介、默认头像和展示鱼获。");
            _loadRoutine = null;
        }

        void ApplyProfile(PlayerProfileDto profile)
        {
            if (profile == null)
                return;
            if (_nickname != null)
                _nickname.text = profile.nickname ?? "";
            if (_bio != null)
                _bio.text = profile.bio ?? "";
            _avatarUrl = profile.avatarUrl ?? "";
            _slots = DesktopProfileCache.Slots(profile);
            RenderAvatars();
            RenderShowcase();
        }

        void RenderAvatars()
        {
            if (_avatars == null)
                return;
            for (var i = _avatars.childCount - 1; i >= 0; i--)
                Destroy(_avatars.GetChild(i).gameObject);

            for (var i = 0; i < DesktopDefaultAvatars.All.Length; i++)
            {
                var entry = DesktopDefaultAvatars.All[i];
                var go = DesktopUiPrefabFactory.Instantiate("AvatarChoice", _avatars) ??
                         DesktopUiPrefabFactory.Instantiate("CatchSlot", _avatars);
                if (go == null)
                    continue;
                go.name = "Avatar_" + entry.Id;
                var image = go.GetComponent<Image>();
                if (image != null)
                    image.color = entry.Path == _avatarUrl
                        ? new Color(0.28f, 0.55f, 0.62f, 1f)
                        : new Color(0.16f, 0.22f, 0.28f, 1f);
                var labelNode = DesktopUiPrefabFactory.Child(go, "Label");
                var label = labelNode != null ? labelNode.GetComponent<Text>() : null;
                if (label != null)
                    label.text = entry.Label;
                var button = go.GetComponent<Button>();
                if (button != null)
                {
                    var path = entry.Path;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => SelectAvatar(path));
                }
            }
        }

        void SelectAvatar(string path)
        {
            _avatarUrl = path ?? string.Empty;
            RenderAvatars();
            SetStatus("已选择默认头像：" + DesktopDefaultAvatars.LabelFor(_avatarUrl));
        }

        void RenderShowcase()
        {
            if (_showcase == null)
                return;
            for (var i = _showcase.childCount - 1; i >= 0; i--)
                Destroy(_showcase.GetChild(i).gameObject);

            for (var i = 0; i < _slots.Length; i++)
            {
                var fish = FindFish(_slots[i]);
                var slot = DesktopUiPrefabFactory.Instantiate("ShowcaseSlot", _showcase) ??
                           DesktopUiPrefabFactory.Instantiate("CatchSlot", _showcase);
                if (slot == null)
                    continue;
                slot.name = "Slot" + i;
                var labelNode = DesktopUiPrefabFactory.Child(slot, "Label");
                var label = labelNode != null ? labelNode.GetComponent<Text>() : null;
                if (label != null)
                    label.text = fish != null ? DesktopProfileCache.FishLabel(fish) : "空";
                var button = slot.GetComponent<Button>();
                if (button != null)
                {
                    var index = i;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OpenPicker(index));
                }
            }
        }

        void OpenPicker(int index)
        {
            _pickingSlot = index;
            if (_picker != null)
                _picker.SetActive(true);
            RenderPicker();
        }

        void HidePicker()
        {
            _pickingSlot = -1;
            if (_picker != null)
                _picker.SetActive(false);
        }

        void ClearPickedSlot()
        {
            if (_pickingSlot < 0 || _pickingSlot >= _slots.Length)
                return;
            _slots[_pickingSlot] = string.Empty;
            RenderShowcase();
            HidePicker();
            SetStatus("已清空展示格，保存后才会同步到服务端。");
        }

        void RenderPicker()
        {
            if (_pickerContent == null)
                return;
            for (var i = _pickerContent.childCount - 1; i >= 0; i--)
                Destroy(_pickerContent.GetChild(i).gameObject);

            if (_inventory == null || _inventory.Length == 0)
            {
                var empty = DesktopUiPrefabFactory.Instantiate("TextStatusRow", _pickerContent) ??
                            DesktopUiPrefabFactory.Instantiate("CatchSlot", _pickerContent);
                if (empty != null)
                {
                    var node = DesktopUiPrefabFactory.Child(empty, "Message") ??
                               DesktopUiPrefabFactory.Child(empty, "Label");
                    var label = node != null ? node.GetComponent<Text>() : null;
                    if (label != null)
                        label.text = "背包没有可展示的鱼获。";
                }
                return;
            }

            for (var i = 0; i < _inventory.Length; i++)
            {
                var item = _inventory[i];
                if (item == null)
                    continue;
                var row = DesktopUiPrefabFactory.Instantiate("ShowcaseSlot", _pickerContent) ??
                          DesktopUiPrefabFactory.Instantiate("CatchSlot", _pickerContent);
                if (row == null)
                    continue;
                var labelNode = DesktopUiPrefabFactory.Child(row, "Label");
                var label = labelNode != null ? labelNode.GetComponent<Text>() : null;
                if (label != null)
                    label.text = DesktopProfileCache.FishLabel(item);
                var button = row.GetComponent<Button>();
                if (button != null)
                {
                    var captured = item;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => PickFish(captured));
                }
            }
        }

        void PickFish(FishInventoryItemDto item)
        {
            if (item == null || _pickingSlot < 0 || _pickingSlot >= _slots.Length)
                return;
            _slots[_pickingSlot] = item.id;
            RenderShowcase();
            HidePicker();
            SetStatus("已选择展示鱼获，保存后才会同步到服务端。");
        }

        FishInventoryItemDto FindFish(string fishId)
        {
            if (_inventory == null || string.IsNullOrEmpty(fishId))
                return null;
            for (var i = 0; i < _inventory.Length; i++)
            {
                if (_inventory[i] != null && _inventory[i].id == fishId)
                    return _inventory[i];
            }
            return null;
        }

        void Save()
        {
            if (_busy)
                return;
            if (!_ready)
            {
                SetStatus("资料尚未加载完成，请稍后或点击重试。");
                return;
            }
            var nickname = _nickname != null ? _nickname.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(nickname))
            {
                SetStatus("昵称不能为空。");
                return;
            }
            if (nickname.Length > NicknameLimit)
            {
                SetStatus("昵称不能超过 " + NicknameLimit + " 个字符。");
                return;
            }

            StopRoutines();
            _saveRoutine = StartCoroutine(SaveRoutine(nickname));
        }

        IEnumerator SaveRoutine(string nickname)
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前会话已失效，请重新登录。");
                yield break;
            }

            _busy = true;
            _lastSaveFailed = false;
            SetStatus("正在保存资料…");
            var bio = _bio != null ? _bio.text : string.Empty;
            if (bio != null && bio.Length > BioLimit)
                bio = bio.Substring(0, BioLimit);

            var profileDone = false;
            var profileOk = false;
            string error = null;
            PlayerProfileDto profile = null;
            yield return _api.UpdatePlayerProfile(nickname, bio, _avatarUrl,
                (ok, loaded, message) =>
                {
                    profileOk = ok;
                    profile = loaded;
                    error = message;
                    profileDone = true;
                });
            while (!profileDone)
                yield return null;

            if (!profileOk || profile == null)
            {
                _busy = false;
                _lastSaveFailed = true;
                _saveRoutine = null;
                SetStatus(error ?? "保存失败，资料未修改。可点击重试。");
                yield break;
            }

            DesktopProfileCache.Latest = profile;
            if (_pond != null && !string.IsNullOrEmpty(profile.nickname))
                _pond.ApplyGameNickname(profile.nickname);

            SetStatus("资料已保存，正在同步展示鱼获…");
            var showcaseDone = false;
            var showcaseOk = false;
            yield return _api.SetShowcase(_slots, (ok, loaded, message) =>
            {
                showcaseOk = ok;
                if (ok)
                    profile = loaded;
                else
                    error = message;
                showcaseDone = true;
            });
            while (!showcaseDone)
                yield return null;

            _busy = false;
            _saveRoutine = null;
            if (!showcaseOk)
            {
                _lastSaveFailed = true;
                SetStatus(error ?? "展示鱼获保存失败，可点击重试。昵称和简介已保存。");
                yield break;
            }

            _lastSaveFailed = false;
            DesktopProfileCache.Latest = profile;
            ApplyProfile(profile);
            SetStatus("保存成功。重新打开个人中心将显示服务端结果。");
        }

        void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message ?? string.Empty;
        }

        void StopRoutines()
        {
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }
            if (_saveRoutine != null)
            {
                StopCoroutine(_saveRoutine);
                _saveRoutine = null;
            }
            _busy = false;
        }

        T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path);
            return node == null ? null : node.GetComponent<T>();
        }

        void BuildFallbackUi()
        {
            var root = GetComponent<RectTransform>();
            if (root.GetComponent<Image>() == null)
                root.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.12f, 0.16f, 1f);

            var header = NewRect("Header", root);
            Place(header, 0f, 0.9f, 1f, 1f);
            var title = CreateText(header, "Title", "编辑资料", 20);
            Place(title.rectTransform, 0.04f, 0f, 0.7f, 1f);
            _back = CreateButton(header, "Back", "返回");
            Place(_back.transform as RectTransform, 0.74f, 0.15f, 0.96f, 0.85f);

            _status = CreateText(root, "Status", string.Empty, 14);
            Place(_status.rectTransform, 0.04f, 0.84f, 0.96f, 0.9f);

            var form = NewRect("Form", root);
            Place(form, 0.04f, 0.66f, 0.96f, 0.84f);
            _nickname = CreateInput(form, "Nickname", "游戏昵称", NicknameLimit);
            Place(_nickname.transform as RectTransform, 0f, 0.55f, 1f, 1f);
            _bio = CreateInput(form, "Bio", "个人简介", BioLimit);
            _bio.lineType = InputField.LineType.MultiLineNewline;
            Place(_bio.transform as RectTransform, 0f, 0f, 1f, 0.5f);

            var avatars = NewRect("Avatars", root);
            Place(avatars, 0.04f, 0.5f, 0.96f, 0.66f);
            var avatarViewport = NewRect("Viewport", avatars);
            Stretch(avatarViewport);
            avatarViewport.gameObject.AddComponent<RectMask2D>();
            _avatars = NewRect("Content", avatarViewport);
            Stretch(_avatars as RectTransform);
            var avatarGrid = _avatars.gameObject.AddComponent<GridLayoutGroup>();
            avatarGrid.cellSize = new Vector2(88f, 48f);
            avatarGrid.spacing = new Vector2(8f, 6f);
            avatarGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            avatarGrid.constraintCount = 6;

            var showcase = NewRect("Showcase", root);
            Place(showcase, 0.04f, 0.18f, 0.96f, 0.5f);
            var showcaseViewport = NewRect("Viewport", showcase);
            Stretch(showcaseViewport);
            showcaseViewport.gameObject.AddComponent<RectMask2D>();
            _showcase = NewRect("Content", showcaseViewport);
            Stretch(_showcase as RectTransform);
            var showcaseGrid = _showcase.gameObject.AddComponent<GridLayoutGroup>();
            showcaseGrid.cellSize = new Vector2(110f, 72f);
            showcaseGrid.spacing = new Vector2(8f, 8f);
            showcaseGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            showcaseGrid.constraintCount = 4;

            var actions = NewRect("Actions", root);
            Place(actions, 0.04f, 0.04f, 0.96f, 0.14f);
            _save = CreateButton(actions, "Save", "保存");
            Place(_save.transform as RectTransform, 0f, 0f, 0.48f, 1f);
            _retry = CreateButton(actions, "Retry", "重试");
            Place(_retry.transform as RectTransform, 0.52f, 0f, 1f, 1f);

            _picker = NewRect("Picker", root).gameObject;
            var pickerImage = _picker.AddComponent<Image>();
            pickerImage.color = new Color(0.05f, 0.07f, 0.1f, 0.96f);
            pickerImage.raycastTarget = true;
            Place(_picker.GetComponent<RectTransform>(), 0.08f, 0.12f, 0.92f, 0.88f);
            var pickerTitle = CreateText(_picker.transform, "Title", "选择背包中的鱼获", 16);
            Place(pickerTitle.rectTransform, 0.04f, 0.88f, 0.7f, 0.98f);
            _closePicker = CreateButton(_picker.transform, "Close", "关闭");
            Place(_closePicker.transform as RectTransform, 0.72f, 0.88f, 0.96f, 0.98f);
            _clearSlot = CreateButton(_picker.transform, "Clear", "清空此格");
            Place(_clearSlot.transform as RectTransform, 0.04f, 0.04f, 0.96f, 0.14f);
            var pickerViewport = NewRect("Viewport", _picker.transform);
            Place(pickerViewport, 0.04f, 0.16f, 0.96f, 0.86f);
            pickerViewport.gameObject.AddComponent<RectMask2D>();
            _pickerContent = NewRect("Content", pickerViewport);
            var pickerContentRt = _pickerContent as RectTransform;
            pickerContentRt.anchorMin = new Vector2(0f, 1f);
            pickerContentRt.anchorMax = new Vector2(1f, 1f);
            pickerContentRt.pivot = new Vector2(0.5f, 1f);
            pickerContentRt.offsetMin = Vector2.zero;
            pickerContentRt.offsetMax = Vector2.zero;
            var pickerLayout = _pickerContent.gameObject.AddComponent<GridLayoutGroup>();
            pickerLayout.cellSize = new Vector2(110f, 72f);
            pickerLayout.spacing = new Vector2(8f, 8f);
            pickerLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            pickerLayout.constraintCount = 4;
            _pickerContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            _picker.SetActive(false);
        }

        static InputField CreateInput(Transform parent, string name, string placeholder, int limit)
        {
            return DesktopModalUi.MakeInput(parent, name, placeholder, limit);
        }

        static Button CreateButton(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.55f, 1f);
            var label = CreateText(go.transform, "Label", text, 16);
            Stretch(label.rectTransform);
            label.alignment = TextAnchor.MiddleCenter;
            return go.GetComponent<Button>();
        }

        static Text CreateText(Transform parent, string name, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = size;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Place(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rt)
        {
            Place(rt, 0f, 0f, 1f, 1f);
        }
    }
}
