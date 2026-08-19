using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Steam main-window profile page. Prefab owns layout; this view binds
    /// server profile fields and never leaves the pond or rebuilds Overlay.
    /// </summary>
    public sealed class DesktopProfilePanel : MonoBehaviour
    {
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        System.Action _openEdit;
        Text _nickname;
        Text _playerId;
        Text _online;
        Text _bio;
        Text _coins;
        Text _avatarLabel;
        Text _status;
        Image _avatar;
        Button _edit;
        Button _retry;
        Transform _showcase;
        FishInventoryItemDto[] _inventory = new FishInventoryItemDto[0];
        Coroutine _loadRoutine;
        Coroutine _avatarRoutine;

        public void Bind(
            IAuthenticatedApiClient api,
            SocialPondSessionController pond,
            System.Action openEdit)
        {
            _api = api;
            _pond = pond;
            _openEdit = openEdit;
            EnsureUi();
            SetStatus("尚未加载个人资料。");
        }

        public void OnOpened()
        {
            Refresh();
        }

        public void OnClosed()
        {
            StopLoad();
        }

        public void BuildEditorLayout()
        {
            if (transform.Find("Header") == null)
                BuildFallbackUi();
        }

        void EnsureUi()
        {
            _nickname = Find<Text>("Header/Nickname");
            _playerId = Find<Text>("Header/PlayerId");
            _online = Find<Text>("Header/Online");
            _bio = Find<Text>("Header/Bio");
            _coins = Find<Text>("Header/Coins");
            _avatarLabel = Find<Text>("Header/Avatar/Label");
            _avatar = Find<Image>("Header/Avatar");
            _status = Find<Text>("Status");
            _edit = Find<Button>("Actions/Edit");
            _retry = Find<Button>("Actions/Retry");
            var showcase = transform.Find("Showcase/Viewport/Content");
            _showcase = showcase;
            if (_nickname == null || _playerId == null || _online == null ||
                _bio == null || _coins == null || _status == null ||
                _edit == null || _retry == null || _showcase == null)
                BuildFallbackUi();

            _edit.onClick.RemoveAllListeners();
            _edit.onClick.AddListener(OpenEdit);
            _retry.onClick.RemoveAllListeners();
            _retry.onClick.AddListener(Refresh);
        }

        void OpenEdit()
        {
            if (_openEdit != null)
                _openEdit();
        }

        void Refresh()
        {
            StopLoad();
            _loadRoutine = StartCoroutine(LoadRoutine());
        }

        IEnumerator LoadRoutine()
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("当前没有有效的 Steam 会话，请重新登录。");
                yield break;
            }

            SetStatus("正在加载个人资料…");
            var done = false;
            var ok = false;
            string error = null;
            PlayerProfileDto profile = null;
            yield return _api.GetPlayerProfile((success, loaded, message) =>
            {
                ok = success;
                profile = loaded;
                error = message;
                done = true;
            });
            while (!done)
                yield return null;

            if (!ok || profile == null)
            {
                _loadRoutine = null;
                SetStatus(error ?? "个人资料加载失败，请点击重试。");
                yield break;
            }

            DesktopProfileCache.Latest = profile;
            var inventoryDone = false;
            var inventoryOk = false;
            string inventoryError = null;
            FishInventoryItemDto[] inventory = null;
            yield return _api.GetInventoryItems((success, items, message) =>
            {
                inventoryOk = success;
                inventoryError = message;
                if (success)
                    inventory = items;
                inventoryDone = true;
            });
            while (!inventoryDone)
                yield return null;
            _inventory = inventory ?? (_pond != null ? _pond.CurrentInventory : null);
            Render(profile);
            if (!inventoryOk)
            {
                _loadRoutine = null;
                SetStatus(inventoryError ?? "背包数据加载失败，请点击重试。");
                yield break;
            }
            SetStatus("资料已与服务端同步。");
            _loadRoutine = null;
        }

        void Render(PlayerProfileDto profile)
        {
            if (profile == null)
                return;
            if (_nickname != null)
                _nickname.text = string.IsNullOrEmpty(profile.nickname) ? "钓友" : profile.nickname;
            if (_playerId != null)
                _playerId.text = "玩家 ID：" + (profile.playerId ?? "");
            if (_online != null)
                _online.text = DesktopProfileCache.OnlineLabel(_pond, _api != null && _api.CanUse);
            if (_bio != null)
                _bio.text = string.IsNullOrEmpty(profile.bio)
                    ? "这个人很懒，还没有写简介…"
                    : profile.bio;
            if (_coins != null)
                _coins.text = "金币：" + profile.coins;
            RenderAvatar(profile);
            RenderShowcase(profile);
        }

        void RenderAvatar(PlayerProfileDto profile)
        {
            if (_avatarLabel != null)
            {
                _avatarLabel.text = string.IsNullOrEmpty(profile.avatarUrl)
                    ? DesktopDefaultAvatars.InitialFor(profile)
                    : DesktopDefaultAvatars.LabelFor(profile.avatarUrl);
            }
            if (_avatarRoutine != null)
                StopCoroutine(_avatarRoutine);
            _avatarRoutine = StartCoroutine(LoadAvatarRoutine(profile));
        }

        IEnumerator LoadAvatarRoutine(PlayerProfileDto profile)
        {
            if (_avatar == null)
                yield break;
            _avatar.color = new Color(0.22f, 0.36f, 0.42f, 1f);
            var url = profile != null ? profile.avatarUrl : null;
            if (string.IsNullOrEmpty(url))
                yield break;
            if (url.StartsWith("data:image/"))
            {
                ApplyDataUrlAvatar(url);
                yield break;
            }
            if (_api == null || string.IsNullOrEmpty(_api.BaseUrl))
                yield break;
            if (url.StartsWith("/"))
                url = _api.BaseUrl + url;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                yield break;

            using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    yield break;
                var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                    yield break;
                _avatar.sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                _avatar.color = Color.white;
                if (_avatarLabel != null)
                    _avatarLabel.text = string.Empty;
            }
        }

        void ApplyDataUrlAvatar(string dataUrl)
        {
            var comma = dataUrl.IndexOf(',');
            if (comma < 0 || comma >= dataUrl.Length - 1)
                return;
            try
            {
                var bytes = System.Convert.FromBase64String(dataUrl.Substring(comma + 1));
                var texture = new Texture2D(2, 2);
                if (!ImageConversion.LoadImage(texture, bytes) || _avatar == null)
                    return;
                _avatar.sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                _avatar.color = Color.white;
                if (_avatarLabel != null)
                    _avatarLabel.text = string.Empty;
            }
            catch
            {
            }
        }

        void RenderShowcase(PlayerProfileDto profile)
        {
            if (_showcase == null)
                return;
            for (var i = _showcase.childCount - 1; i >= 0; i--)
                Destroy(_showcase.GetChild(i).gameObject);

            var slots = DesktopProfileCache.Slots(profile);
            var inventory = _inventory != null && _inventory.Length > 0
                ? _inventory
                : (_pond != null ? _pond.CurrentInventory : null);
            for (var i = 0; i < slots.Length; i++)
            {
                var fish = FindFish(inventory, slots[i]);
                var slot = DesktopUiPrefabFactory.Instantiate("ShowcaseSlot", _showcase) ??
                           DesktopUiPrefabFactory.Instantiate("CatchSlot", _showcase);
                if (slot == null)
                    continue;
                slot.name = "Slot" + i;
                var labelNode = DesktopUiPrefabFactory.Child(slot, "Label");
                var label = labelNode != null ? labelNode.GetComponent<Text>() : null;
                if (label != null)
                    label.text = fish != null ? DesktopProfileCache.FishLabel(fish) : "空";
            }
        }

        static FishInventoryItemDto FindFish(FishInventoryItemDto[] items, string fishId)
        {
            if (items == null || string.IsNullOrEmpty(fishId))
                return null;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].id == fishId)
                    return items[i];
            }
            return null;
        }

        void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message ?? string.Empty;
        }

        void StopLoad()
        {
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }
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
            Place(header, 0f, 0.58f, 1f, 1f);
            _avatar = NewRect("Avatar", header).gameObject.AddComponent<Image>();
            _avatar.color = new Color(0.22f, 0.36f, 0.42f, 1f);
            Place(_avatar.rectTransform, 0.04f, 0.28f, 0.22f, 0.92f);
            _avatarLabel = CreateText(_avatar.transform, "Label", "钓", 28);
            Stretch(_avatarLabel.rectTransform);
            _avatarLabel.alignment = TextAnchor.MiddleCenter;
            _nickname = CreateText(header, "Nickname", "昵称", 26);
            Place(_nickname.rectTransform, 0.26f, 0.68f, 0.96f, 0.92f);
            _playerId = CreateText(header, "PlayerId", "玩家 ID：", 14);
            Place(_playerId.rectTransform, 0.26f, 0.5f, 0.96f, 0.68f);
            _online = CreateText(header, "Online", "在线状态", 14);
            Place(_online.rectTransform, 0.26f, 0.34f, 0.96f, 0.5f);
            _coins = CreateText(header, "Coins", "金币：0", 16);
            Place(_coins.rectTransform, 0.26f, 0.16f, 0.96f, 0.34f);
            _bio = CreateText(header, "Bio", "简介", 15);
            Place(_bio.rectTransform, 0.04f, 0.02f, 0.96f, 0.16f);
            _status = CreateText(root, "Status", string.Empty, 14);
            Place(_status.rectTransform, 0.04f, 0.5f, 0.96f, 0.58f);
            var showcase = NewRect("Showcase", root);
            Place(showcase, 0.04f, 0.16f, 0.96f, 0.5f);
            var viewport = NewRect("Viewport", showcase);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _showcase = NewRect("Content", viewport);
            Stretch(_showcase as RectTransform);
            var grid = _showcase.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(110f, 72f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            var actions = NewRect("Actions", root);
            Place(actions, 0.04f, 0.04f, 0.96f, 0.14f);
            _edit = CreateButton(actions, "Edit", "编辑资料");
            Place(_edit.transform as RectTransform, 0f, 0f, 0.48f, 1f);
            _retry = CreateButton(actions, "Retry", "重试");
            Place(_retry.transform as RectTransform, 0.52f, 0f, 1f, 1f);
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
