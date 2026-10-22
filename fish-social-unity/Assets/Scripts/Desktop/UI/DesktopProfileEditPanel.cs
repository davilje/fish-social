using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Game-profile editor. Saves nickname/bio/avatar/showcase through REST
    /// and does not leave the pond or rebuild Overlay.
    /// </summary>
    public sealed class DesktopProfileEditPanel : MonoBehaviour, IPointerClickHandler
    {
        bool _uiReady;
        const int NicknameLimit = 12;
        const int BioLimit = 120;

        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        System.Action _openProfile;
        System.Action<DesktopProfileHubPanel.HubTab> _openProfileTab;
        InputField _nickname;
        InputField _bio;
        Text _status;
        Button _save;
        Button _retry;
        Button _back;
        Button _clearSlot;
        Button _closePicker;
        Button _confirmPicker;
        InputField _search;
        string _pendingFishId;
        bool _showcaseTab;
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
            System.Action openProfile,
            System.Action<DesktopProfileHubPanel.HubTab> openProfileTab = null)
        {
            _api = api;
            _pond = pond;
            _openProfile = openProfile;
            _openProfileTab = openProfileTab;
            EnsureUi();
            SetStatus("打开后将加载当前服务端资料。");
        }

        public void OnOpened()
        {
            if (!_uiReady)
                return;
            if (Application.isPlaying)
                PlayJournalEntrance();
            Refresh();
        }

        public void OnClosed()
        {
            StopRoutines();
            HidePicker();
        }

        public void BuildEditorLayout()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            BuildFallbackUi();
            RefineLayout();
            EnsureUi();
#endif
        }

        void EnsureUi()
        {
            _uiReady = false;
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
            var pickerCanvas = _picker != null ? _picker.GetComponent<Canvas>() : null;
            if (pickerCanvas != null) { pickerCanvas.overrideSorting = true; pickerCanvas.sortingOrder = 100; }
            _clearSlot = Find<Button>("Picker/Clear");
            _closePicker = Find<Button>("Picker/Close");
            _confirmPicker = Find<Button>("Picker/Confirm");
            _search = Find<InputField>("Picker/Search");
            if (_nickname == null || _bio == null || _status == null ||
                _save == null || _retry == null || _back == null ||
                _avatars == null || _showcase == null || _picker == null ||
                _pickerContent == null || _clearSlot == null || _closePicker == null)
            {
                Debug.LogError("[DesktopUI] PanelProfileEdit requires Form/{Nickname,Bio}, Header/Back, Actions/{Save,Retry}, Status, Avatars/Viewport/Content, Showcase/Viewport/Content and Picker/{Viewport/Content,Clear,Close}.", this);
                return;
            }

            _uiReady = true;
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
            var authoredClose = Find<Button>("DesignHeader/Close");
            if (authoredClose != null)
            {
                authoredClose.onClick.RemoveAllListeners();
                authoredClose.onClick.AddListener(GoBack);
            }
            _clearSlot.onClick.RemoveAllListeners();
            _clearSlot.onClick.AddListener(ClearPickedSlot);
            _closePicker.onClick.RemoveAllListeners();
            _closePicker.onClick.AddListener(HidePicker);
            BindWorkspaceButton("Picker/Dismiss",HidePicker);
            _nickname.onValueChanged.RemoveListener(UpdateJournalPreview);
            _nickname.onValueChanged.AddListener(UpdateJournalPreview);
            _bio.onValueChanged.RemoveListener(UpdateJournalBio);
            _bio.onValueChanged.AddListener(UpdateJournalBio);
            var tabLabels = new[] { "资料", "展示柜", "图鉴", "相册", "成就" };
            for (var i = 0; i < tabLabels.Length; i++)
            {
                var target = (DesktopProfileHubPanel.HubTab)i;
                BindWorkspaceButton("JournalEditRail/Tab_" + tabLabels[i], () => NavigateProfileTab(target));
            }
            BindWorkspaceButton("WorkspaceAvatarTab", () => SelectWorkspaceTab(false));
            BindWorkspaceButton("WorkspaceShowcaseTab", () => SelectWorkspaceTab(true));
            if (_confirmPicker != null) { _confirmPicker.onClick.RemoveAllListeners(); _confirmPicker.onClick.AddListener(ConfirmPickedFish); }
            if (_search != null) { _search.onValueChanged.RemoveAllListeners(); _search.onValueChanged.AddListener(_ => RenderPicker()); }
            ApplyCozyStyle();
            HidePicker();
            SelectWorkspaceTab(false);
            if (Application.isPlaying)
                EnsureJournalMotion();
        }

        void EnsureJournalMotion()
        {
            var motion = GetComponent<CozyJournalPageMotion>() ?? gameObject.AddComponent<CozyJournalPageMotion>();
            motion.Configure(
                transform.Find("JournalEditHeading"),
                null,
                transform.Find("Status"),
                transform.Find("Form"),
                transform.Find("AvatarsTitle"),
                transform.Find("Avatars"),
                transform.Find("Actions"));
            motion.ConfigureAside(
                transform.Find("JournalPreview"),
                transform.Find("ShowcaseTitle"),
                transform.Find("Showcase"));
        }

        void PlayJournalEntrance()
        {
            var motion = GetComponent<CozyJournalPageMotion>();
            if (motion == null)
                EnsureJournalMotion();
            motion = GetComponent<CozyJournalPageMotion>();
            if (motion == null)
                return;
            motion.PlayEntrance(GetComponentsInChildren<CozyStickerMotion>(true).Length);
            CozyJournalStaticPose.RevealAside(transform);
        }

        void GoBack()
        {
            if (_busy) return;
            HidePicker();
            if (_openProfile != null)
                _openProfile();
        }

        void NavigateProfileTab(DesktopProfileHubPanel.HubTab tab)
        {
            if (_busy) return;
            // Draft values remain local. Navigating away never invokes either save API.
            HidePicker();
            if (_openProfileTab != null) _openProfileTab(tab);
            else GoBack();
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
            SetStatus("");
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
            _avatarUrl = DesktopDefaultAvatars.NormalizeAssetPath(profile.avatarUrl) ?? "";
            _slots = DesktopProfileCache.Slots(profile);
            RenderAvatars();
            RenderShowcase();
        }

        void UpdateJournalBio(string value)
        {
            CozyItemArt.Text(transform, "JournalPreview/Bio", value);
            CozyItemArt.Text(transform, "Form/BioCount", (value ?? "").Length + " / " + BioLimit);
        }

        void UpdateJournalPreview(string value)
        {
            CozyItemArt.Text(transform, "JournalPreview/Name", string.IsNullOrWhiteSpace(value) ? "钓友" : value);
            CozyItemArt.Set(transform, "JournalPreview/Avatar", DesktopDefaultAvatars.Portrait(_avatarUrl));
            UpdateJournalBio(_bio != null ? _bio.text : "");
        }

        void RenderAvatars()
        {
            UpdateJournalPreview(_nickname != null ? _nickname.text : "");
            if (_avatars == null)
                return;
            ClearChoices(_avatars);

            for (var i = 0; i < DesktopDefaultAvatars.All.Length; i++)
            {
                var entry = DesktopDefaultAvatars.All[i];
                var go = WorkspaceChoice("WorkspaceAvatarTemplate", "AvatarChoice", _avatars);
                if (go == null)
                    continue;
                go.name = "Avatar_" + entry.Id;
                CozyItemArt.Set(go.transform, "DesignItemArt", DesktopDefaultAvatars.Portrait(entry.Path));
                var labelNode = DesktopUiPrefabFactory.Child(go, "Label");
                var label = labelNode != null ? labelNode.GetComponent<Text>() : null;
                if (label != null)
                    label.text = entry.Label;
                StyleChoice(go, entry.Path == _avatarUrl);
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
            if (_busy) return;
            _avatarUrl = path ?? string.Empty;
            RenderAvatars();
            SetStatus("已选择角色：" + DesktopDefaultAvatars.LabelFor(_avatarUrl) + "，保存后生效。");
        }

        void RenderShowcase()
        {
            if (_showcase == null)
                return;
            ClearChoices(_showcase);

            for (var i = 0; i < _slots.Length; i++)
            {
                var fish = FindFish(_slots[i]);
                var slot = WorkspaceChoice("WorkspaceSlotTemplate", "ShowcaseSlot", _showcase);
                if (slot == null)
                    continue;
                slot.name = "Slot" + i;
                CozyItemArt.Set(slot.transform, "DesignItemArt", fish != null ? CozyItemArt.Fish(fish.speciesId) : null);
                var labelNode = DesktopUiPrefabFactory.Child(slot, "Label");
                var label = labelNode != null ? labelNode.GetComponent<Text>() : null;
                if (label != null)
                    label.text = fish != null ? DesktopFishCatalog.SpeciesName(fish.speciesId) : "留给下一次惊喜";
                CozyItemArt.Text(slot.transform, "WorkspaceSlot", (i + 1).ToString("00"));
                CozyItemArt.Text(slot.transform, "WorkspaceMeta", fish != null ? fish.sizeM.ToString("0.00") + " m · 点击更换" : "点击选择鱼获");
                var empty = slot.transform.Find("WorkspaceEmpty"); if (empty != null) empty.gameObject.SetActive(fish == null);
                StyleChoice(slot);
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
            if (_busy) return;
            _pickingSlot = index;
            _pendingFishId = index >= 0 && index < _slots.Length ? _slots[index] : null;
            if (_search != null) _search.SetTextWithoutNotify("");
            CozyItemArt.Text(transform, "Picker/Title", "整理展示位 " + (index + 1));
            if (_picker != null)
            {
                _picker.SetActive(true);
                var overlay = _picker.GetComponent<Canvas>();
                if (overlay != null) { overlay.overrideSorting = true; overlay.sortingOrder = 100; }
            }
            RenderPicker();
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (!_busy && data != null && data.button == PointerEventData.InputButton.Left && data.pointerCurrentRaycast.gameObject == transform.Find("Picker/WorkspaceDim")?.gameObject) HidePicker();
        }

        void Update()
        {
            if (!_busy && _picker != null && _picker.activeSelf && Input.GetKeyDown(KeyCode.Escape)) HidePicker();
        }

        void HidePicker()
        {
            _pickingSlot = -1;
            if (_picker != null)
                _picker.SetActive(false);
        }

        void ClearPickedSlot()
        {
            if (_busy) return;
            if (_pickingSlot < 0 || _pickingSlot >= _slots.Length)
                return;
            _slots[_pickingSlot] = string.Empty;
            RenderShowcase();
            HidePicker();
            SetStatus("已清空展示格，保存后才会同步到服务端。");
        }

        void RenderPicker()
        {
            if (_pickerContent == null) return;
            ClearChoices(_pickerContent);
            var query = _search != null ? _search.text.Trim() : "";
            var count = 0;
            foreach (var item in _inventory ?? new FishInventoryItemDto[0])
            {
                if (item == null) continue;
                var name = DesktopFishCatalog.SpeciesName(item.speciesId);
                if (query.Length > 0 && name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var row = WorkspaceChoice("WorkspacePickerTemplate", "ShowcaseSlot", _pickerContent);
                if (row == null) continue;
                row.name = "Fish_" + item.id;
                count++;
                CozyItemArt.Text(row.transform, "Label", name);
                CozyItemArt.Text(row.transform, "WorkspaceMeta", item.sizeM.ToString("0.00") + " m · 估算 " + DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(item.sizeM)));
                CozyItemArt.Text(row.transform, "WorkspaceRecord", RecordTime(item.caughtAt) + " · " + PondName(item.pondId));
                CozyItemArt.Set(row.transform, "DesignItemArt", CozyItemArt.Fish(item.speciesId));
                StyleChoice(row, item.id == _pendingFishId);
                var button = row.GetComponent<Button>();
                if (button != null)
                {
                    var captured = item;
                    button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => PickFish(captured));
                }
            }
            if (count == 0)
            {
                var empty = DesktopUiPrefabFactory.Instantiate("TextStatusRow", _pickerContent);
                if (empty != null)
                {
                    var text = empty.GetComponentInChildren<Text>(true);
                    if (text != null) text.text = query.Length == 0 ? "背包没有可展示的鱼获。" : "没有找到这种鱼，换个名字试试。";
                }
            }
            var selected = FindFish(_pendingFishId);
            CozyItemArt.Text(transform, "Picker/Selection", selected != null ? "已选：" + DesktopFishCatalog.SpeciesName(selected.speciesId) : "选择一条鱼获");
            if (_confirmPicker != null) _confirmPicker.interactable = selected != null;
        }

        void PickFish(FishInventoryItemDto item)
        {
            if (item == null || _pickingSlot < 0 || _pickingSlot >= _slots.Length) return;
            _pendingFishId = item.id;
            if (_confirmPicker == null) { ConfirmPickedFish(); return; }
            RenderPicker();
        }

        void ConfirmPickedFish()
        {
            if (_busy) return;
            var item = FindFish(_pendingFishId);
            if (item == null || _pickingSlot < 0 || _pickingSlot >= _slots.Length) return;
            _slots[_pickingSlot] = item.id;
            RenderShowcase(); HidePicker();
            SetStatus("已选择展示鱼获，保存后生效。");
        }

        void BindWorkspaceButton(string path, UnityEngine.Events.UnityAction action)
        {
            var button = Find<Button>(path); if (button == null) return;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(action);
        }

        void SelectWorkspaceTab(bool showcase)
        {
            if (_busy) return;
            _showcaseTab = showcase;
            if (transform.Find("WorkspaceAvatarTab") == null) return;
            if (_avatars != null) _avatars.parent.parent.gameObject.SetActive(!showcase);
            if (_showcase != null) _showcase.parent.parent.gameObject.SetActive(showcase);
            Find<Button>("WorkspaceAvatarTab")?.GetComponent<CozyButtonVisual>()?.SetSelected(!showcase);
            Find<Button>("WorkspaceShowcaseTab")?.GetComponent<CozyButtonVisual>()?.SetSelected(showcase);
            CozyItemArt.Text(transform,"WorkspaceTabHint",showcase ? "8 个展示位 · 点击整张卡片更换" : DesktopDefaultAvatars.All.Length + " 个模样，选你喜欢的");
        }

        GameObject WorkspaceChoice(string templateName, string fallback, Transform parent)
        {
            var template = transform.Find(templateName);
            var item = template != null ? Instantiate(template.gameObject, parent, false) : DesktopUiPrefabFactory.Instantiate(fallback, parent);
            if (item != null) item.SetActive(true);
            return item;
        }

        static void ClearChoices(Transform content)
        {
            for (var i = content.childCount - 1; i >= 0; i--)
            {
                var go = content.GetChild(i).gameObject;
                if (Application.isPlaying) { go.SetActive(false); go.transform.SetParent(null); Destroy(go); }
                else DestroyImmediate(go);
            }
        }

        static string RecordTime(long time)
        {
            if (time <= 0) return "时间未记录";
            try { return System.DateTimeOffset.FromUnixTimeMilliseconds(time).ToLocalTime().ToString("MM.dd HH:mm"); }
            catch (System.ArgumentOutOfRangeException) { return "时间未记录"; }
        }

        static string PondName(string id)
        { return DesktopGameData.GetPond(id)?.name ?? (string.IsNullOrEmpty(id) ? "水域未记录" : id); }

        /// <summary>Use the same editor bindings for API snapshots and editor preview fixtures.</summary>
        public void PresentSnapshot(PlayerProfileDto profile, FishInventoryItemDto[] inventory, bool showcase = false, int pickingSlot = -1)
        {
            EnsureUi();
            _inventory = inventory ?? new FishInventoryItemDto[0];
            ApplyProfile(profile); _ready = profile != null;
            SelectWorkspaceTab(showcase);
            SetStatus("");
            if (pickingSlot >= 0) OpenPicker(pickingSlot);
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
            if (_pond != null)
            {
                if (!string.IsNullOrEmpty(profile.nickname))
                    _pond.ApplyGameNickname(profile.nickname);
                _pond.ApplyGameAvatarUrl(profile.avatarUrl);
            }
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();

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
            SetStatus("保存成功。");
            GoBack();
        }

        void SetStatus(string message)
        {
            if (_status != null) _status.text = message ?? string.Empty;
            if (_retry != null) _retry.gameObject.SetActive(_lastSaveFailed || (!_ready && _loadRoutine == null && !string.IsNullOrEmpty(message)));
            if (_save != null) _save.interactable = !_busy && _ready;
            if (_back != null) _back.interactable = !_busy;
            if (_nickname != null) _nickname.interactable = !_busy;
            if (_bio != null) _bio.interactable = !_busy;
            var rail = transform.Find("JournalEditRail");
            if (rail != null) foreach (var button in rail.GetComponentsInChildren<Button>(true)) button.interactable = !_busy;
            foreach (var container in new[] { _avatars, _showcase })
                if (container != null) foreach (var button in container.GetComponentsInChildren<Button>(true)) button.interactable = !_busy;
            foreach (var path in new[] { "WorkspaceAvatarTab", "WorkspaceShowcaseTab", "DesignHeader/Close" })
            { var button = Find<Button>(path); if (button != null) button.interactable = !_busy; }
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
            var header = EnsureRect("Header", transform);
            EnsureText(header, "Title", "编辑资料", 22);
            _back = Find<Button>("Header/Back") ?? CreateButton(header, "Back", "返回资料");
            var form = EnsureRect("Form", transform);
            EnsureText(form, "NicknameLabel", "游戏昵称", 17);
            EnsureText(form, "BioLabel", "个人简介", 17);
            _nickname = Find<InputField>("Form/Nickname") ?? CreateInput(form, "Nickname", "请输入游戏昵称", NicknameLimit);
            _bio = Find<InputField>("Form/Bio") ?? CreateInput(form, "Bio", "介绍一下自己吧", BioLimit);
            _status = Find<Text>("Status") ?? EnsureText(transform, "Status", string.Empty, 16);
            var actions = EnsureRect("Actions", transform);
            _save = Find<Button>("Actions/Save") ?? CreateButton(actions, "Save", "保存资料");
            _retry = Find<Button>("Actions/Retry") ?? CreateButton(actions, "Retry", "重试");
            EnsureText(transform, "AvatarsTitle", "选择角色 / 头像", 17);
            EnsureText(transform, "ShowcaseTitle", "展示鱼获 · 点击格子选择", 17);
            _avatars = EnsureScroll("Avatars", transform, 3, 38f);
            _showcase = EnsureScroll("Showcase", transform, 4, 64f);
            _pickerContent = EnsureScroll("Picker", transform, 3, 68f);
            _picker = transform.Find("Picker").gameObject;
            EnsureText(_picker.transform, "Title", "选择背包鱼获", 20);
            _clearSlot = Find<Button>("Picker/Clear") ?? CreateButton(_picker.transform, "Clear", "清空展示格");
            _closePicker = Find<Button>("Picker/Close") ?? CreateButton(_picker.transform, "Close", "关闭");
        }

        void ApplyCozyStyle()
        {
            DesktopModalUi.StyleTree(transform);
        }

        void RefineLayout()
        {
            PlacePath("Header", 0.025f, 0.89f, 0.975f, 0.99f);
            PlacePath("Header/Title", 0.015f, 0f, 0.75f, 1f);
            PlacePath("Header/Back", 0.79f, 0.12f, 0.985f, 0.88f);
            PlacePath("Form", 0.025f, 0.16f, 0.47f, 0.875f);
            PlacePath("Form/NicknameLabel", 0f, 0.82f, 0.28f, 0.95f);
            PlacePath("Form/Nickname", 0.3f, 0.82f, 1f, 0.95f);
            PlacePath("Form/BioLabel", 0f, 0.7f, 1f, 0.8f);
            PlacePath("Form/Bio", 0f, 0.02f, 1f, 0.69f);
            PlacePath("AvatarsTitle", 0.51f, 0.81f, 0.975f, 0.875f);
            PlacePath("Avatars", 0.51f, 0.59f, 0.975f, 0.8f);
            PlacePath("ShowcaseTitle", 0.51f, 0.53f, 0.975f, 0.58f);
            PlacePath("Showcase", 0.51f, 0.16f, 0.975f, 0.52f);
            PlacePath("Status", 0.025f, 0.015f, 0.72f, 0.13f);
            PlacePath("Actions", 0.75f, 0.025f, 0.975f, 0.125f);
            PlacePath("Actions/Save", 0f, 0f, 0.61f, 1f);
            PlacePath("Actions/Retry", 0.66f, 0f, 1f, 1f);
            PlacePath("Picker", 0.08f, 0.08f, 0.92f, 0.92f);
            PlacePath("Picker/Title", 0.04f, 0.86f, 0.75f, 0.98f);
            PlacePath("Picker/Viewport", 0.04f, 0.17f, 0.96f, 0.84f);
            PlacePath("Picker/Clear", 0.04f, 0.025f, 0.32f, 0.135f);
            PlacePath("Picker/Close", 0.75f, 0.025f, 0.96f, 0.135f);
            FitChoices(_avatars);
            FitChoices(_showcase);
            FitChoices(_pickerContent);
        }

        void OnRectTransformDimensionsChange()
        {

        }

        void PlacePath(string path, float minX, float minY, float maxX, float maxY)
        {
            var rect = transform.Find(path) as RectTransform;
            if (rect != null)
                Place(rect, minX, minY, maxX, maxY);
        }

        static void StyleInput(InputField input)
        {
            if (input != null)
                DesktopModalUi.StyleTree(input.transform);
        }

        static void StyleChoice(GameObject choice, bool selected = false)
        {
            DesktopModalUi.StyleTree(choice.transform);
            var visual = choice.GetComponent<CozyButtonVisual>();
            if (visual != null)
                visual.SetSelected(selected);
        }

        static Transform EnsureScroll(string name, Transform parent, int columns, float height)
        {
            var root = EnsureRect(name, parent);
            var image = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            image.color = CatTheme.Cream;
            var scroll = root.GetComponent<ScrollRect>() ?? root.gameObject.AddComponent<ScrollRect>();
            var viewport = EnsureRect("Viewport", root);
            Stretch(viewport);
            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
            var content = EnsureRect("Content", viewport);
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var grid = content.GetComponent<GridLayoutGroup>();
            if (grid == null && content.GetComponent<LayoutGroup>() == null)
                grid = content.gameObject.AddComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
                grid.cellSize = new Vector2(100f, height);
                grid.spacing = new Vector2(8f, 6f);
                grid.padding = new RectOffset(6, 6, 6, 6);
            }
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            return content;
        }

        static void FitChoices(Transform content)
        {
            if (content == null)
                return;
            var grid = content.GetComponent<GridLayoutGroup>();
            var viewport = content.parent as RectTransform;
            if (grid == null || viewport == null || grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
                return;
            var columns = Mathf.Max(1, grid.constraintCount);
            var width = viewport.rect.width - grid.padding.left - grid.padding.right - grid.spacing.x * (columns - 1);
            grid.cellSize = new Vector2(Mathf.Max(48f, width / columns), grid.cellSize.y);
        }

        static RectTransform EnsureRect(string name, Transform parent)
        {
            return parent.Find(name) as RectTransform ?? NewRect(name, parent);
        }

        static Text EnsureText(Transform parent, string name, string value, int size)
        {
            var node = parent.Find(name);
            var label = node != null ? node.GetComponent<Text>() : null;
            return label != null ? label : CreateText(parent, name, value, size);
        }

        static InputField CreateInput(Transform parent, string name, string placeholder, int limit)
        {
            return DesktopModalUi.MakeInput(parent, name, placeholder, limit);
        }

        static Button CreateButton(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = CatTheme.Cream;
            var label = CreateText(go.transform, "Label", text, 16);
            Stretch(label.rectTransform);
            label.alignment = TextAnchor.MiddleCenter;
            CatTheme.StyleButton(go.GetComponent<Button>());
            return go.GetComponent<Button>();
        }

        static Text CreateText(Transform parent, string name, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = CatTheme.Font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = size;
            label.color = CatTheme.Ink;
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
