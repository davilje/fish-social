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
        bool _uiReady;
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
        string _viewingOtherPlayerId;

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

        public void ShowOtherPlayer(string playerId)
        {
            _viewingOtherPlayerId = playerId ?? string.Empty;
            if (isActiveAndEnabled)
                Refresh();
        }

        public void ShowSelfProfile()
        {
            _viewingOtherPlayerId = null;
            if (isActiveAndEnabled)
                Refresh();
        }

        public void OnClosed()
        {
            StopLoad();
        }

        public void BuildEditorLayout()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            if (transform.Find("Header") == null)
                BuildFallbackUi();
#endif
        }

        void EnsureUi()
        {
            _uiReady = false;
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
                _edit == null || _retry == null || _showcase == null || _avatar == null || _avatarLabel == null)
            {
                DesktopModalUi.RequireNodes(transform, "PanelProfile", "Header/Nickname", "Header/PlayerId", "Header/Online", "Header/Bio", "Header/Coins", "Status", "Actions/Edit", "Actions/Retry", "Showcase/Viewport/Content");
                Debug.LogError("[DesktopUI] PanelProfile requires complete prefab controls; runtime layout generation is disabled.", this);
                return;
            }
            _uiReady = true;
            DesktopModalUi.StyleTree(transform);

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
            if (!_uiReady)
                return;
            if (!isActiveAndEnabled)
                return;
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
            if (!string.IsNullOrEmpty(_viewingOtherPlayerId))
            {
                yield return LoadOtherPlayerRoutine(_viewingOtherPlayerId);
                _loadRoutine = null;
                yield break;
            }

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

        IEnumerator LoadOtherPlayerRoutine(string playerId)
        {
            SetStatus("正在加载玩家资料…");
            var done = false;
            var ok = false;
            string error = null;
            PublicPlayerViewDto view = null;
            yield return _api.GetPublicPlayerView(playerId, 20, (success, loaded, message) =>
            {
                ok = success;
                view = loaded;
                error = message;
                done = true;
            });
            while (!done)
                yield return null;

            if (!ok || view == null || view.profile == null)
            {
                SetStatus(error ?? "玩家资料加载失败，请点击重试。");
                yield break;
            }

            var profile = MapPublicProfile(view.profile);
            _inventory = view.showcaseFish ?? new FishInventoryItemDto[0];
            RenderOtherPlayer(profile);
            RenderShowcase(profile);
            SetStatus("已加载 " + profile.nickname + " 的资料摘要。");
        }

        static PlayerProfileDto MapPublicProfile(PublicPlayerProfileDto source)
        {
            return new PlayerProfileDto
            {
                playerId = source.playerId,
                nickname = source.nickname,
                bio = source.bio,
                avatarUrl = source.avatarUrl,
                showcaseFishIds = source.showcaseFishIds,
                coins = 0,
            };
        }

        void RenderOtherPlayer(PlayerProfileDto profile)
        {
            Render(profile);
            if (_edit != null)
                _edit.gameObject.SetActive(false);
            if (_coins != null)
                _coins.text = "他人资料";
            if (_online != null)
                _online.text = "同塘玩家";
        }

        void Render(PlayerProfileDto profile)
        {
            if (_edit != null)
                _edit.gameObject.SetActive(string.IsNullOrEmpty(_viewingOtherPlayerId));
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
            if (!isActiveAndEnabled)
                return;
            _avatarRoutine = StartCoroutine(LoadAvatarRoutine(profile));
        }

        IEnumerator LoadAvatarRoutine(PlayerProfileDto profile)
        {
            if (_avatar == null)
                yield break;
            _avatar.color = _avatar.sprite != null ? Color.white : CatTheme.Cream;
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
                var slot = DesktopUiPrefabFactory.Instantiate("ShowcaseSlot", _showcase);
                if (slot == null)
                    continue;
                slot.name = "Slot" + i;
                var labelNode = DesktopUiPrefabFactory.Child(slot, "Label");
                var label = labelNode != null ? labelNode.GetComponent<Text>() : null;
                if (label != null)
                    label.text = fish != null ? DesktopProfileCache.FishLabel(fish) : "空";
                DesktopModalUi.StyleTree(slot.transform);
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
            Debug.LogError("[DesktopUI] Prefab is missing required controls. Runtime will not build a second layout.");
            return;
        }

        static Button CreateButton(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = CatTheme.Cream;
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
