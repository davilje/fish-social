using UnityEngine;

namespace FishSocial.Desktop
{
    static class DesktopFeaturePanelFactory
    {
        public static T Mount<T>(Transform parent, System.Action<T> bind) where T : MonoBehaviour
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/" + PrefabName<T>());
            T view;
            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab, parent, false);
                instance.name = typeof(T).Name;
                DesktopModalUi.Stretch(instance);
                view = instance.GetComponent<T>();
                if (view == null)
                    view = instance.AddComponent<T>();
            }
            else
            {
                var go = new GameObject(typeof(T).Name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                DesktopModalUi.Stretch(go);
                view = go.AddComponent<T>();
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
