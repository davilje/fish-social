using UnityEngine;

namespace FishSocial.Desktop
{
    static class DesktopUiPrefabFactory
    {
        public static GameObject Instantiate(string prefabName, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/" + prefabName);
            if (prefab == null)
            {
                Debug.LogError("[DesktopUI] Required item prefab is missing: " + prefabName);
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = prefabName;
            return instance;
        }

        public static Transform Child(GameObject root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            return root.transform.Find(name);
        }
    }
}
