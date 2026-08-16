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
            return typeof(T).Name;
        }
    }
}
