using System;
using UnityEngine;

namespace FishSocial.Desktop
{
    [Serializable]
    public sealed class WorldMapPondDefinition
    {
        public string pondId;
        public string displayName;
        public string regionId;
        public string theme;
        [Range(0f, 1f)] public float x;
        [Range(0f, 1f)] public float y;
        public int capacity = 20;
    }

    [Serializable]
    sealed class WorldMapPondCatalogDocument
    {
        public WorldMapPondDefinition[] ponds = null;
    }

    public static class WorldMapPondCatalog
    {
        public static WorldMapPondDefinition[] Load()
        {
            var asset = Resources.Load<TextAsset>("Desktop/WorldMapPonds");
            if (asset == null)
            {
                Debug.LogError("[WorldMap] Missing Resources/Desktop/WorldMapPonds.json.");
                return new WorldMapPondDefinition[0];
            }

            try
            {
                var document = JsonUtility.FromJson<WorldMapPondCatalogDocument>(asset.text);
                return document?.ponds ?? new WorldMapPondDefinition[0];
            }
            catch (Exception error)
            {
                Debug.LogError("[WorldMap] Failed to parse pond catalog: " + error.Message);
                return new WorldMapPondDefinition[0];
            }
        }
    }
}
