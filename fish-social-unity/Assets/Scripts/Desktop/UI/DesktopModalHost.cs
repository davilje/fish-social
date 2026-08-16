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
            _root = DesktopModalUi.Panel("FeatureModalRoot", canvas, new Color(0f, 0f, 0f, 0.45f));
            DesktopModalUi.Stretch(_root);
            var dim = _root.AddComponent<Button>();
            dim.transition = Selectable.Transition.None;
            dim.onClick.AddListener(Close);

            var panel = DesktopModalUi.Panel("ModalPanel", _root.transform, DesktopModalUi.PanelColor);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1040, 580);
            panel.AddComponent<Button>().transition = Selectable.Transition.None;

            var header = DesktopModalUi.Panel("Header", panel.transform, DesktopModalUi.Header);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = Vector2.one;
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 52f);
            headerRt.anchoredPosition = Vector2.zero;
            _title = DesktopModalUi.Label(header.transform, "Title", "弹窗", 22, TextAnchor.MiddleLeft);
            var titleRt = _title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(18f, 0f);
            titleRt.offsetMax = new Vector2(-80f, 0f);

            var close = DesktopModalUi.MakeButton(header.transform, "Close", "关闭", Close);
            var closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.sizeDelta = new Vector2(72f, 32f);
            closeRt.anchoredPosition = new Vector2(-12f, 0f);

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(panel.transform, false);
            var bodyRt = DesktopModalUi.Stretch(body);
            bodyRt.offsetMin = new Vector2(12f, 12f);
            bodyRt.offsetMax = new Vector2(-12f, -60f);

            _social = CreateView<DesktopSocialModalView>(body.transform, "SocialModal");
            _catchBag = CreateView<DesktopCatchBagModalView>(body.transform, "CatchBagModal");
            _gallery = CreateView<DesktopGalleryModalView>(body.transform, "GalleryModal");
            _settings = CreateView<DesktopSettingsModalView>(body.transform, "SettingsModal");
        }

        static T CreateView<T>(Transform parent, string name) where T : MonoBehaviour
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            DesktopModalUi.Stretch(go);
            return go.AddComponent<T>();
        }
    }
}
