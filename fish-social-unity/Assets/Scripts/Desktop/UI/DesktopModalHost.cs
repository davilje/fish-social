using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Social;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Unused after STEAM-DESKTOP-07E: feature pages live on main-window tabs.
    /// Kept so existing scene/script references do not break.
    /// </summary>
    public sealed class DesktopModalHost : MonoBehaviour
    {
        GameObject _root;
        Text _title;
        DesktopSocialModalView _social;
        DesktopCatchBagModalView _catchBag;
        DesktopGalleryModalView _gallery;
        DesktopSettingsModalView _settings;

        public DesktopModalId Current { get; private set; }

        public void Bind(
            Transform canvas,
            IAuthenticatedApiClient api,
            SocialPondSessionController pond,
            SocialLobbyController lobby)
        {
            Build(canvas);
            if (_root == null)
                return;
            _social.Bind(api, pond, lobby);
            _catchBag.Bind(api, pond);
            _gallery.Bind(api, pond);
            _settings.Bind();
            Close();
        }

        public void Open(DesktopModalId id)
        {
            if (id == DesktopModalId.None)
            {
                Close();
                return;
            }

            if (_root == null)
                return;
            Current = id;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _social.gameObject.SetActive(id == DesktopModalId.Social);
            _catchBag.gameObject.SetActive(id == DesktopModalId.CatchBag);
            _gallery.gameObject.SetActive(id == DesktopModalId.Gallery);
            _settings.gameObject.SetActive(id == DesktopModalId.Settings);
            switch (id)
            {
                case DesktopModalId.Social:
                    _title.text = "好友与聊天";
                    _social.OnOpened();
                    break;
                case DesktopModalId.CatchBag:
                    _title.text = "鱼获 / 背包";
                    _catchBag.OnOpened();
                    break;
                case DesktopModalId.Gallery:
                    _title.text = "图鉴";
                    _gallery.OnOpened();
                    break;
                case DesktopModalId.Settings:
                    _title.text = "设置";
                    _settings.OnOpened();
                    break;
            }
        }

        public void Close()
        {
            if (Current == DesktopModalId.Social) _social.OnClosed();
            if (Current == DesktopModalId.CatchBag) _catchBag.OnClosed();
            if (Current == DesktopModalId.Gallery) _gallery.OnClosed();
            Current = DesktopModalId.None;
            if (_root != null)
                _root.SetActive(false);
        }

        void Update()
        {
            if (Current == DesktopModalId.None)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        void Build(Transform canvas)
        {
            if (_root != null || canvas == null)
                return;
            var existing = canvas.Find("FeatureModalRoot");
            var instance = existing != null ? existing.gameObject : null;
            var created = instance == null;
            if (created)
            {
                var prefab = Resources.Load<GameObject>("Desktop/Prefabs/FeatureModalRoot");
                if (prefab == null)
                {
                    Debug.LogError("[DesktopUI] Required prefab is missing: Desktop/Prefabs/FeatureModalRoot", this);
                    return;
                }
                instance = Object.Instantiate(prefab, canvas, false);
                instance.name = "FeatureModalRoot";
            }
            _title = DesktopModalUi.FindComponent<Text>(instance.transform, "ModalPanel/Header/Title");
            var close = DesktopModalUi.FindComponent<Button>(instance.transform, "ModalPanel/Header/Close");
            var dismiss = instance.GetComponent<Button>();
            _social = DesktopModalUi.FindComponent<DesktopSocialModalView>(instance.transform, "ModalPanel/Body/SocialModal");
            _catchBag = DesktopModalUi.FindComponent<DesktopCatchBagModalView>(instance.transform, "ModalPanel/Body/CatchBagModal");
            _gallery = DesktopModalUi.FindComponent<DesktopGalleryModalView>(instance.transform, "ModalPanel/Body/GalleryModal");
            _settings = DesktopModalUi.FindComponent<DesktopSettingsModalView>(instance.transform, "ModalPanel/Body/SettingsModal");
            if (_title == null || close == null || dismiss == null ||
                DesktopModalUi.FindComponent<Button>(instance.transform, "ModalPanel") == null || _social == null ||
                _catchBag == null || _gallery == null || _settings == null)
            {
                Debug.LogError("[DesktopUI] FeatureModalRoot requires ModalPanel/Header/{Title,Close} and complete ModalPanel/Body/{SocialModal,CatchBagModal,GalleryModal,SettingsModal} views.", instance);
                instance.SetActive(false);
                if (created)
                    Object.Destroy(instance);
                return;
            }
            _root = instance;
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(Close);
            dismiss.onClick.RemoveAllListeners();
            dismiss.onClick.AddListener(Close);
            DesktopModalUi.StyleTree(_root.transform);
        }

    }
}
