using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Product controls that the cozy-journal web preview does not draw.
    /// <see cref="Visible"/> stays false so the shell matches that preview.
    /// Set it true to bring this group back. Delete this file, the marked objects,
    /// and <c>DesktopProductMenuView</c> to remove the extras permanently.
    /// </summary>
    public sealed class CozySurplus : MonoBehaviour
    {
        public static bool Visible;

        public static void Park(GameObject node)
        {
            if (node == null)
                return;
            if (node.GetComponent<CozySurplus>() == null)
                node.AddComponent<CozySurplus>();
            if (!Visible)
                node.SetActive(false);
        }

        void OnEnable()
        {
            if (!Visible)
                gameObject.SetActive(false);
        }
    }
}
