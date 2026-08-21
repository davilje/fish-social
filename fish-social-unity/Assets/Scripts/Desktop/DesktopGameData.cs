using System;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Reads FEAT-PROG-01 exported JSON under Resources/GameData.
    /// Display-only; sell/join mutations stay on the server.
    /// </summary>
    public static class DesktopGameData
    {
        [Serializable]
        public sealed class PondDef
        {
            public string pondId;
            public string name;
            public string pondCategory;
            public string mapZoneId;
            public int feePer2h;
            public int maxFeeChargesPerDay;
            public string unlock;
            public bool isOpen = true;
            public bool showOnWorldMap = true;
            public int minPlayerLevel;
            public float mapX;
            public float mapY;
        }

        [Serializable]
        public sealed class SpeciesDef
        {
            public string speciesId;
            public string name;
            public string diet;
            public string catchGroup;
        }

        [Serializable]
        sealed class MetaFile
        {
            public string version;
            public float SIZE_EXP = 1.15f;
        }

        [Serializable]
        sealed class SellRow
        {
            public string quality;
            public int QUALITY_BASE;
            public float SIZE_REF;
            public int MIN_SELL;
            public string catchGroup;
            public float SPECIES_MULT;
        }

        [Serializable]
        sealed class PondArray { public PondDef[] items; }

        [Serializable]
        sealed class SpeciesArray { public SpeciesDef[] items; }

        [Serializable]
        sealed class SellArray { public SellRow[] items; }

        [Serializable]
        public sealed class RodDef
        {
            public string rodId;
            public string name;
            public string subType;
            public int priceGold;
            public float biteBonus;
            public float escapeReduction;
            public float breakSizeM;
            public int breakMaxLandings;
            public float fitGray;
            public float fitGreen;
            public float fitBlue;
            public float fitPurple;
            public float fitRed;
            public float fitOrange;
            public float fitGold;
            public float fitStillBait;
            public float fitStreamLight;
            public float fitLurePredator;
            public float fitCastHeavy;
            public float fitGiantGame;
        }

        [Serializable]
        public sealed class BaitDef
        {
            public string baitId;
            public string name;
            public string diet;
            public int unlockPlayerLevel;
            public int costGoldPerUse;
            public float biteBonusHerbivore;
            public float biteBonusOmnivore;
            public float biteBonusCarnivore;
            public bool isDefaultInfinite;
        }

        [Serializable]
        public sealed class VesselDef
        {
            public string vesselId;
            public string name;
            public int unlockPlayerLevel;
            public int priceGold;
            public int placeholderCatchCount;
            public bool enabledUse;
        }

        sealed class RodArray { public RodDef[] items; }
        sealed class BaitArray { public BaitDef[] items; }
        sealed class VesselArray { public VesselDef[] items; }

        static bool _loaded;
        static PondDef[] _ponds = new PondDef[0];
        static SpeciesDef[] _species = new SpeciesDef[0];
        static SellRow[] _sell = new SellRow[0];
        static RodDef[] _rods = new RodDef[0];
        static BaitDef[] _baits = new BaitDef[0];
        static VesselDef[] _vessels = new VesselDef[0];
        static float _sizeExp = 1.15f;

        public static PondDef[] Ponds
        {
            get
            {
                EnsureLoaded();
                return _ponds;
            }
        }

        public static PondDef GetPond(string pondId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(pondId))
                return null;
            for (var i = 0; i < _ponds.Length; i++)
            {
                if (_ponds[i] != null && _ponds[i].pondId == pondId)
                    return _ponds[i];
            }
            return null;
        }

        public static string CategoryLabel(string category)
        {
            switch (category)
            {
                case "novice": return "新手塘";
                case "advanced": return "高级塘";
                case "veteran": return "老手塘";
                case "wilderness": return "野外塘";
                case "reservoir": return "水库塘";
                case "forbidden": return "禁止钓鱼塘";
                case "giant": return "巨物塘";
                default: return string.IsNullOrEmpty(category) ? "未分类" : category;
            }
        }

        public static Color CategoryColor(string category, bool locked)
        {
            if (locked)
                return new Color(0.38f, 0.4f, 0.42f, 0.95f);
            switch (category)
            {
                case "advanced": return new Color(0.95f, 0.75f, 0.28f, 0.95f);
                case "veteran": return new Color(0.62f, 0.42f, 0.86f, 0.95f);
                case "wilderness": return new Color(0.38f, 0.72f, 0.42f, 0.95f);
                case "reservoir": return new Color(0.28f, 0.62f, 0.78f, 0.95f);
                case "forbidden": return new Color(0.86f, 0.38f, 0.32f, 0.95f);
                case "giant": return new Color(0.72f, 0.78f, 0.86f, 0.95f);
                default: return new Color(0.7f, 0.7f, 0.7f, 0.95f);
            }
        }

        public static int EstimateSellPrice(string quality, float sizeM, string speciesId)
        {
            EnsureLoaded();
            SellRow row = null;
            for (var i = 0; i < _sell.Length; i++)
            {
                if (_sell[i] != null && _sell[i].quality == quality)
                {
                    row = _sell[i];
                    break;
                }
            }

            if (row == null || row.QUALITY_BASE <= 0)
                return DesktopFishCatalog.LegacyEstimateSellPrice(quality, sizeM);

            var sizeRef = row.SIZE_REF > 0f ? row.SIZE_REF : 0.2f;
            var ratio = Mathf.Max(0.01f, sizeM / sizeRef);
            var catchGroup = ResolveCatchGroup(speciesId);
            var speciesMult = ResolveSpeciesMult(catchGroup);
            var raw = row.QUALITY_BASE * Mathf.Pow(ratio, _sizeExp) * speciesMult;
            var sold = Mathf.FloorToInt(raw);
            return Mathf.Max(sold, row.MIN_SELL);
        }

        public static RodDef GetRod(string rodId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(rodId))
                return null;
            for (var i = 0; i < _rods.Length; i++)
            {
                if (_rods[i] != null && _rods[i].rodId == rodId)
                    return _rods[i];
            }
            return null;
        }

        public static BaitDef GetBait(string baitId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(baitId))
                return null;
            for (var i = 0; i < _baits.Length; i++)
            {
                if (_baits[i] != null && _baits[i].baitId == baitId)
                    return _baits[i];
            }
            return null;
        }

        public static VesselDef GetVessel(string vesselId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(vesselId))
                return null;
            for (var i = 0; i < _vessels.Length; i++)
            {
                if (_vessels[i] != null && _vessels[i].vesselId == vesselId)
                    return _vessels[i];
            }
            return null;
        }

        public static string DietLabel(string diet)
        {
            switch (diet)
            {
                case "any": return "通用";
                case "herbivore": return "草食";
                case "omnivore": return "杂食";
                case "carnivore": return "肉食";
                default: return string.IsNullOrEmpty(diet) ? "未标注" : diet;
            }
        }

        public static string CatchGroupLabel(string group)
        {
            switch (group)
            {
                case "still_bait": return "静水底钓";
                case "stream_light": return "溪流轻口";
                case "lure_predator": return "路亚掠食";
                case "cast_heavy": return "重抛";
                case "giant_game": return "巨物";
                default: return string.IsNullOrEmpty(group) ? "未标注" : group;
            }
        }

        public static string FormatMul(float value)
        {
            return "×" + value.ToString("0.00");
        }

        public static string FormatPct(float value)
        {
            var pct = value * 100f;
            return (pct >= 0f ? "+" : "") + pct.ToString("0.#") + "%";
        }

        static string ResolveCatchGroup(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId))
                return "still_bait";
            for (var i = 0; i < _species.Length; i++)
            {
                if (_species[i] != null && _species[i].speciesId == speciesId &&
                    !string.IsNullOrEmpty(_species[i].catchGroup))
                    return _species[i].catchGroup;
            }
            return "still_bait";
        }

        static float ResolveSpeciesMult(string catchGroup)
        {
            for (var i = 0; i < _sell.Length; i++)
            {
                if (_sell[i] != null && _sell[i].catchGroup == catchGroup &&
                    _sell[i].SPECIES_MULT > 0f)
                    return _sell[i].SPECIES_MULT;
            }
            return 1f;
        }

        static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;
            var metaAsset = Resources.Load<TextAsset>("GameData/_meta");
            if (metaAsset != null)
            {
                var meta = JsonUtility.FromJson<MetaFile>(metaAsset.text);
                if (meta != null && meta.SIZE_EXP > 0f)
                    _sizeExp = meta.SIZE_EXP;
            }

            _ponds = ParseArray<PondArray, PondDef>(
                Resources.Load<TextAsset>("GameData/ponds"),
                wrap => wrap != null ? wrap.items : null);
            _species = ParseArray<SpeciesArray, SpeciesDef>(
                Resources.Load<TextAsset>("GameData/fish_species"),
                wrap => wrap != null ? wrap.items : null);
            _sell = ParseArray<SellArray, SellRow>(
                Resources.Load<TextAsset>("GameData/fish_sell"),
                wrap => wrap != null ? wrap.items : null);
            _rods = ParseArray<RodArray, RodDef>(
                Resources.Load<TextAsset>("GameData/rods"),
                wrap => wrap != null ? wrap.items : null);
            _baits = ParseArray<BaitArray, BaitDef>(
                Resources.Load<TextAsset>("GameData/baits"),
                wrap => wrap != null ? wrap.items : null);
            _vessels = ParseArray<VesselArray, VesselDef>(
                Resources.Load<TextAsset>("GameData/vessels"),
                wrap => wrap != null ? wrap.items : null);
        }

        static TItem[] ParseArray<TWrap, TItem>(TextAsset asset, Func<TWrap, TItem[]> pick)
            where TWrap : class
        {
            if (asset == null || string.IsNullOrEmpty(asset.text))
                return new TItem[0];
            var json = asset.text.Trim();
            if (json.StartsWith("["))
                json = "{\"items\":" + json + "}";
            try
            {
                var wrap = JsonUtility.FromJson<TWrap>(json);
                var items = pick(wrap);
                return items ?? new TItem[0];
            }
            catch (Exception error)
            {
                Debug.LogWarning("[GameData] Failed to parse " + asset.name + ": " + error.Message);
                return new TItem[0];
            }
        }
    }
}
