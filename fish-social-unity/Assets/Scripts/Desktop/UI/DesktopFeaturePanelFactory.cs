using UnityEngine;

namespace FishSocial.Desktop
{
    static class DesktopFeaturePanelFactory
    {
        public static T Mount<T>(Transform parent, System.Action<T> bind, string prefabName = null)
            where T : MonoBehaviour
        {
            var name = string.IsNullOrEmpty(prefabName) ? PrefabName<T>() : prefabName;
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/" + name);
            if (prefab == null)
            {
                Debug.LogError("[DesktopUI] Required prefab is missing: Desktop/Prefabs/" + name);
                return null;
            }
            if (prefab.GetComponent<T>() == null)
            {
                Debug.LogError("[DesktopUI] Required component " + typeof(T).Name + " is missing from prefab: " + name);
                return null;
            }
            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = name;
            DesktopLocalization.BindPrefab(instance, "Desktop/Prefabs/" + name);
            return BindExisting(instance, bind);
        }

        public static T BindExisting<T>(GameObject root, System.Action<T> bind) where T : MonoBehaviour
        {
            var view = root != null ? root.GetComponent<T>() : null;
            if (view == null)
            {
                Debug.LogError("[DesktopUI] Required serialized panel/component is missing: " + typeof(T).Name);
                return null;
            }
            // Embedded authored panels also pass through this path without Mount.
            DesktopLocalization.BindPrefab(root, "Desktop/Prefabs/" + root.name.Replace("(Clone)", string.Empty));
            bind?.Invoke(view);
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
            if (typeof(T) == typeof(DesktopShopPanel))
                return "PanelShop";
            if (typeof(T) == typeof(DesktopProfilePanel))
                return "PanelProfile";
            if (typeof(T) == typeof(DesktopProfileHubPanel))
                return "PanelProfileHub";
            if (typeof(T) == typeof(DesktopProfileEditPanel))
                return "PanelProfileEdit";
            if (typeof(T) == typeof(DesktopSocialFeedPanel))
                return "PanelSocialFeed";
            if (typeof(T) == typeof(DesktopLeaderboardPanel))
                return "PanelLeaderboard";
            if (typeof(T) == typeof(CozyPondSelectionPanel))
                return "PanelPondSelection";
            if (typeof(T) == typeof(DesktopPondSettlementModalView))
                return "PanelPondSettlement";
            return typeof(T).Name;
        }
    }
}
