using UnityEngine;

namespace FishSocial.Desktop
{
    static class DesktopFeaturePanelFactory
    {
        public static T Mount<T>(Transform parent, System.Action<T> bind) where T : MonoBehaviour
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/" + PrefabName<T>());
            if (prefab == null)
            {
                if (typeof(T) == typeof(DesktopWorldMapPanel))
                {
                    var fallback = new GameObject(
                        "PanelWorldMap",
                        typeof(RectTransform),
                        typeof(UnityEngine.UI.Image),
                        typeof(DesktopWorldMapPanel));
                    fallback.transform.SetParent(parent, false);
                    var fallbackRect = fallback.GetComponent<RectTransform>();
                    fallbackRect.anchorMin = Vector2.zero;
                    fallbackRect.anchorMax = Vector2.one;
                    fallbackRect.offsetMin = Vector2.zero;
                    fallbackRect.offsetMax = Vector2.zero;
                    var fallbackView = fallback.GetComponent<T>();
                    bind(fallbackView);
                    Debug.LogWarning("[DesktopUI] PanelWorldMap prefab is missing; using editor-generated fallback.");
                    return fallbackView;
                }
                Debug.LogError("[DesktopUI] Required prefab is missing: Desktop/Prefabs/" +
                               PrefabName<T>());
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = typeof(T).Name;
            var view = instance.GetComponent<T>();
            if (view == null)
            {
                Debug.LogError("[DesktopUI] Required component is missing from prefab: " +
                               PrefabName<T>());
                Object.Destroy(instance);
                return null;
            }

            bind(view);
            return view;
        }

        static string PrefabName<T>()
        {
            if (typeof(T) == typeof(DesktopSocialModalView))
                return "PanelSocial";
            if (typeof(T) == typeof(DesktopCatchBagModalView))
                return "PanelCatch";
            if (typeof(T) == typeof(DesktopGalleryModalView))
                return "PanelGallery";
            if (typeof(T) == typeof(DesktopSettingsModalView))
                return "PanelSettings";
            if (typeof(T) == typeof(DesktopWorldMapPanel))
                return "PanelWorldMap";
            return typeof(T).Name;
        }
    }
}
