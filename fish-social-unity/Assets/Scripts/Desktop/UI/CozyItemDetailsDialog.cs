using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Displays the complete item text in the page's authored details dialog.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyItemDetailsDialog : MonoBehaviour
    {
        GameObject dialog;
        Text titleLabel;
        Text bodyLabel;
        Image artImage;
        ScrollRect scroll;
        Button dismiss;
        Button closeButton;
        GameObject previousSelection;

        public bool IsOpen => dialog != null && dialog.activeSelf;

        public static CozyItemDetailsDialog Bind(Transform page)
        {
            if (page == null || page.Find("DetailsDialog") == null)
                return null;
            var view = page.GetComponent<CozyItemDetailsDialog>() ?? page.gameObject.AddComponent<CozyItemDetailsDialog>();
            return view.BindNodes() ? view : null;
        }

        bool BindNodes()
        {
            var root = transform.Find("DetailsDialog");
            if (root == null)
                return false;
            dialog = root.gameObject;
            titleLabel = root.Find("Content/Title")?.GetComponent<Text>();
            bodyLabel = root.Find("Content/Scroll/Viewport/Body")?.GetComponent<Text>();
            artImage = root.Find("Content/Image")?.GetComponent<Image>();
            scroll = root.Find("Content/Scroll")?.GetComponent<ScrollRect>();
            var nextDismiss = root.GetComponent<Button>();
            var nextClose = root.Find("Content/Close")?.GetComponent<Button>();
            if (titleLabel == null || bodyLabel == null || artImage == null || scroll == null ||
                nextDismiss == null || nextClose == null)
            {
                Debug.LogError("[DesktopUI] DetailsDialog requires Content/{Title,Image,Scroll/Viewport/Body,Close}.", this);
                return false;
            }
            if (dismiss != null) dismiss.onClick.RemoveListener(Close);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            dismiss = nextDismiss;
            closeButton = nextClose;
            dismiss.onClick.AddListener(Close);
            closeButton.onClick.AddListener(Close);
            return true;
        }

        public void Show(string title, string body, Sprite art)
        {
            if (dialog == null && !BindNodes())
                return;
            if (!gameObject.activeInHierarchy)
                return;
            if (!IsOpen && EventSystem.current != null)
                previousSelection = EventSystem.current.currentSelectedGameObject;
            DesktopLocalization.SetText(titleLabel, title ?? string.Empty);
            DesktopLocalization.SetText(bodyLabel, body ?? string.Empty);
            artImage.sprite = art;
            artImage.enabled = art != null;
            dialog.transform.SetAsLastSibling();
            dialog.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyLabel.rectTransform);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void Close()
        {
            if (dialog == null || !dialog.activeSelf)
                return;
            scroll?.StopMovement();
            dialog.SetActive(false);
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }

        void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        void OnDisable() { Close(); }

        void OnDestroy()
        {
            if (dismiss != null) dismiss.onClick.RemoveListener(Close);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }
    }
}
