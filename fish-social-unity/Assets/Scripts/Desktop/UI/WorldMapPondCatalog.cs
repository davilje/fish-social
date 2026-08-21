using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop
{
    public sealed class WorldMapPondView
    {
        public string pondId;
        public string displayName;
        public string pondCategory;
        public string theme;
        public float x;
        public float y;
        public int capacity = 20;
        public int feePer2h;
        public int maxFeeChargesPerDay;
        public int minPlayerLevel;
        public bool isOpen = true;
        public string unlock;
    }

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
        public static WorldMapPondView[] LoadVisible()
        {
            var coords = Load();
            var byId = new Dictionary<string, WorldMapPondDefinition>(StringComparer.Ordinal);
            for (var i = 0; i < coords.Length; i++)
            {
                if (coords[i] != null && !string.IsNullOrEmpty(coords[i].pondId))
                    byId[coords[i].pondId] = coords[i];
            }

            var source = DesktopGameData.Ponds;
            var list = new List<WorldMapPondView>();
            for (var i = 0; i < source.Length; i++)
            {
                var pond = source[i];
                if (pond == null || !pond.showOnWorldMap)
                    continue;
                if (pond.pondCategory == "novice")
                    continue;
                WorldMapPondDefinition coord;
                byId.TryGetValue(pond.pondId, out coord);
                list.Add(new WorldMapPondView
                {
                    pondId = pond.pondId,
                    displayName = !string.IsNullOrEmpty(pond.name) ? pond.name :
                        (coord != null ? coord.displayName : pond.pondId),
                    pondCategory = pond.pondCategory,
                    theme = coord != null ? coord.theme : pond.mapZoneId,
                    x = coord != null ? coord.x : pond.mapX,
                    y = coord != null ? coord.y : pond.mapY,
                    capacity = coord != null ? coord.capacity : 20,
                    feePer2h = pond.feePer2h,
                    maxFeeChargesPerDay = pond.maxFeeChargesPerDay,
                    minPlayerLevel = pond.minPlayerLevel,
                    isOpen = pond.isOpen && pond.pondCategory != "giant",
                    unlock = pond.unlock,
                });
            }

            return list.ToArray();
        }

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
                if (document == null || document.ponds == null)
                {
                    Debug.LogError("[WorldMap] Pond catalog is missing the ponds array.");
                    return new WorldMapPondDefinition[0];
                }
                return document.ponds;
            }
            catch (Exception error)
            {
                Debug.LogError("[WorldMap] Failed to parse pond catalog: " + error.Message);
                return new WorldMapPondDefinition[0];
            }
        }
    }
}
