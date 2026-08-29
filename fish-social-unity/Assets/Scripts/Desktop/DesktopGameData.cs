using System;
using System.Collections.Generic;
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
            public int feePer2hSellOnly;
            public int feePer2hAutoReturn;
            public bool allowsAutoReturn;
            public int maxFeeChargesPerDay;
            public string unlock;
            public bool isOpen = true;
            public bool showOnWorldMap = true;
            public int minPlayerLevel;
            public float mapX;
            public float mapY;
            public string bioRegion;
            public string waterType;
            public string realWorldRef;
            public int maxPopulation;
            public int minPopulation;
            public int initialPopulation;
        }

        [Serializable]
        public sealed class SpeciesDef
        {
            public string speciesId;
            public string name;
            public string diet;
            public string catchGroup;
            public float typicalMinM;
            public float typicalMaxM;
            public string rarityTier;
            public bool nationwide;
            public int qualityMin = 1;
            public int qualityMax = 7;
        }

        [Serializable]
        sealed class FormulaConstantRow
        {
            public string key;
            public float value;
            public string notes;
        }

        [Serializable]
        sealed class FormulaConstantArray { public FormulaConstantRow[] items; }

        [Serializable]
        sealed class MetaFile
        {
            public string version;
            public float SIZE_EXP = 1.15f;
        }

        [Serializable]
        sealed class QualityStatsRow
        {
            public string quality;
            public float sizeCapM;
            public float biteBaseAtMaxSize;
            public string displayName;
            public int QUALITY_BASE;
            public float SIZE_REF;
            public int MIN_SELL;
        }

        [Serializable]
        sealed class PondArray { public PondDef[] items; }

        [Serializable]
        sealed class SpeciesArray { public SpeciesDef[] items; }

        [Serializable]
        sealed class QualityStatsArray { public QualityStatsRow[] items; }

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

        [Serializable]
        public sealed class SpotClueTextDef
        {
            public string clueId;
            public string clueType;
            public string clueText;
            public int weight = 1;
            public int minPlayerLevel;
            public int minPondLevel;
            public string pondCategory;
            public string spotTag;
            public string activitySignal;
            public bool enabled = true;
        }

        [Serializable]
        public sealed class PondSpotTagDef
        {
            public string pondId;
            public string spotId;
            public string tags;
        }

        [Serializable]
        sealed class ReturnRulesRow
        {
            public string minQuality = "purple";
            public float minWeightJin = 10f;
            public float heavyWeightJin = 100f;
            public float goldMulVsSell = 1.5f;
            public float goldMulHeavy = 3f;
        }

        sealed class ReturnRulesArray { public ReturnRulesRow[] items; }

        sealed class RodArray { public RodDef[] items; }
        sealed class BaitArray { public BaitDef[] items; }
        sealed class VesselArray { public VesselDef[] items; }
        sealed class SpotClueTextArray { public SpotClueTextDef[] items; }
        sealed class PondSpotTagArray { public PondSpotTagDef[] items; }

        static bool _loaded;
        static PondDef[] _ponds = new PondDef[0];
        static SpeciesDef[] _species = new SpeciesDef[0];
        static QualityStatsRow[] _qualityStats = new QualityStatsRow[0];
        static RodDef[] _rods = new RodDef[0];
        static BaitDef[] _baits = new BaitDef[0];
        static VesselDef[] _vessels = new VesselDef[0];
        static SpotClueTextDef[] _spotClueTexts = new SpotClueTextDef[0];
        static PondSpotTagDef[] _pondSpotTags = new PondSpotTagDef[0];
        static FormulaConstantRow[] _formulaConstants = new FormulaConstantRow[0];
        static float _sizeExp = 1.15f;
        static float _lengthWeightA = 12f;
        static float _lengthWeightB = 3f;
        static ReturnRulesRow _returnRules;

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

        public static SpotClueTextDef[] SpotClueTexts
        {
            get
            {
                EnsureLoaded();
                return _spotClueTexts;
            }
        }

        public static string[] GetSpotIds(string pondId)
        {
            EnsureLoaded();
            var list = new List<string>();
            for (var i = 0; i < _pondSpotTags.Length; i++)
            {
                var row = _pondSpotTags[i];
                if (row == null || string.IsNullOrEmpty(row.spotId))
                    continue;
                if (!string.IsNullOrEmpty(pondId) &&
                    !string.Equals(row.pondId, pondId, StringComparison.Ordinal))
                    continue;
                if (!list.Contains(row.spotId))
                    list.Add(row.spotId);
            }

            return list.ToArray();
        }

        public static string[] GetSpotTags(string pondId, string spotId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(spotId))
                return new string[0];
            for (var i = 0; i < _pondSpotTags.Length; i++)
            {
                var row = _pondSpotTags[i];
                if (row == null) continue;
                if (!string.Equals(row.spotId, spotId, StringComparison.Ordinal))
                    continue;
                if (!string.IsNullOrEmpty(pondId) &&
                    !string.Equals(row.pondId, pondId, StringComparison.Ordinal))
                    continue;
                return ParseTagList(row.tags);
            }
            return new string[0];
        }

        static string[] ParseTagList(string tags)
        {
            if (string.IsNullOrEmpty(tags))
                return new string[0];
            var parts = tags.Split(',');
            var list = new List<string>();
            for (var i = 0; i < parts.Length; i++)
            {
                var t = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (!string.IsNullOrEmpty(t))
                    list.Add(t);
            }
            return list.ToArray();
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

        public static float CalcWeightKg(float sizeM)
        {
            EnsureLoaded();
            var L = Mathf.Max(0f, sizeM);
            return Mathf.Round(_lengthWeightA * Mathf.Pow(L, _lengthWeightB) * 100f) / 100f;
        }

        /** 市斤：1 斤 = 0.5 kg */
        public static float CalcWeightJin(float sizeM)
        {
            return Mathf.Round(CalcWeightKg(sizeM) * 2f * 100f) / 100f;
        }

        public static string FormatWeightKg(float weightKg)
        {
            if (weightKg >= 100f) return weightKg.ToString("0") + "kg";
            if (weightKg >= 10f) return weightKg.ToString("0.0") + "kg";
            return weightKg.ToString("0.00") + "kg";
        }

        /// <summary>回鱼金倍率（&gt;100 斤×3；≥10 斤×1.5；否则 0）</summary>
        public static float ResolveReturnGoldMul(float sizeM)
        {
            EnsureLoaded();
            var jin = CalcWeightJin(sizeM);
            var minJin = _returnRules != null ? _returnRules.minWeightJin : 10f;
            var heavyJin = _returnRules != null ? _returnRules.heavyWeightJin : 100f;
            if (jin > heavyJin) return _returnRules != null ? _returnRules.goldMulHeavy : 3f;
            if (jin >= minJin) return _returnRules != null ? _returnRules.goldMulVsSell : 1.5f;
            return 0f;
        }

        public static float MinReturnWeightJin()
        {
            EnsureLoaded();
            return _returnRules != null ? _returnRules.minWeightJin : 10f;
        }

        public static float HeavyReturnWeightJin()
        {
            EnsureLoaded();
            return _returnRules != null ? _returnRules.heavyWeightJin : 100f;
        }

        static int QualityRank(string quality)
        {
            switch (quality)
            {
                case "gray": return 1;
                case "green": return 2;
                case "blue": return 3;
                case "purple": return 4;
                case "red": return 5;
                case "orange": return 6;
                case "gold": return 7;
                default: return 0;
            }
        }

        public static bool IsReturnEligible(string quality, float sizeM)
        {
            EnsureLoaded();
            var minQ = _returnRules != null && !string.IsNullOrEmpty(_returnRules.minQuality)
                ? _returnRules.minQuality
                : "purple";
            if (QualityRank(quality) < QualityRank(minQ)) return false;
            return CalcWeightJin(sizeM) >= MinReturnWeightJin();
        }

        public static string QualityRankLabel(int rank)
        {
            switch (rank)
            {
                case 1: return "普通";
                case 2: return "优良";
                case 3: return "稀有";
                case 4: return "史诗";
                case 5: return "传说";
                case 6: return "神话";
                case 7: return "至尊";
                default: return "—";
            }
        }

        public static int EstimateSellPrice(string quality, float sizeM, string speciesId)
        {
            EnsureLoaded();
            QualityStatsRow row = null;
            for (var i = 0; i < _qualityStats.Length; i++)
            {
                if (_qualityStats[i] != null && _qualityStats[i].quality == quality)
                {
                    row = _qualityStats[i];
                    break;
                }
            }

            if (row == null || row.QUALITY_BASE <= 0)
                return DesktopFishCatalog.LegacyEstimateSellPrice(quality, sizeM);

            var sizeRef = row.SIZE_REF > 0f ? row.SIZE_REF : 0.2f;
            var ratio = Mathf.Max(0.01f, sizeM / sizeRef);
            var raw = row.QUALITY_BASE * Mathf.Pow(ratio, _sizeExp);
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

        public static BaitDef[] ListBaits()
        {
            EnsureLoaded();
            return _baits ?? new BaitDef[0];
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

        public static string RarityLabel(string tier)
        {
            switch (tier)
            {
                case "common": return "常见";
                case "uncommon": return "少见";
                case "rare": return "稀有";
                case "legendary": return "传说";
                default: return string.IsNullOrEmpty(tier) ? "未标注" : tier;
            }
        }

        public static SpeciesDef GetSpeciesDef(string speciesId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(speciesId))
                return null;
            speciesId = ResolveSpeciesId(speciesId);
            for (var i = 0; i < _species.Length; i++)
            {
                if (_species[i] != null && _species[i].speciesId == speciesId)
                    return _species[i];
            }
            return null;
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

        static readonly Dictionary<string, string> LegacySpeciesIds = new Dictionary<string, string>
        {
            { "bass", "black_bass" },
            { "trout", "rainbow_trout" },
            { "perch", "black_bass" },
            { "sturgeon", "chinese_sturgeon" },
        };

        public static string ResolveSpeciesId(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId))
                return speciesId;
            string mapped;
            return LegacySpeciesIds.TryGetValue(speciesId, out mapped) ? mapped : speciesId;
        }

        public static string SpeciesName(string speciesId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(speciesId))
                return speciesId;
            speciesId = ResolveSpeciesId(speciesId);
            for (var i = 0; i < _species.Length; i++)
            {
                if (_species[i] != null && _species[i].speciesId == speciesId &&
                    !string.IsNullOrEmpty(_species[i].name))
                    return _species[i].name;
            }
            return speciesId;
        }

        public static SpeciesDef[] Species
        {
            get
            {
                EnsureLoaded();
                return _species;
            }
        }

        static string ResolveCatchGroup(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId))
                return "still_bait";
            speciesId = ResolveSpeciesId(speciesId);
            for (var i = 0; i < _species.Length; i++)
            {
                if (_species[i] != null && _species[i].speciesId == speciesId &&
                    !string.IsNullOrEmpty(_species[i].catchGroup))
                    return _species[i].catchGroup;
            }
            return "still_bait";
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
            if (_species.Length == 0)
                Debug.LogWarning("[GameData] fish_species.json missing or failed to parse; 图鉴 will be empty.");
            else
                Debug.Log("[GameData] fish_species loaded " + _species.Length + " rows.");
            _qualityStats = ParseArray<QualityStatsArray, QualityStatsRow>(
                Resources.Load<TextAsset>("GameData/fish_quality_stats"),
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
            _spotClueTexts = ParseArray<SpotClueTextArray, SpotClueTextDef>(
                Resources.Load<TextAsset>("GameData/spot_clue_texts"),
                wrap => wrap != null ? wrap.items : null);
            _pondSpotTags = ParseArray<PondSpotTagArray, PondSpotTagDef>(
                Resources.Load<TextAsset>("GameData/pond_spot_tags"),
                wrap => wrap != null ? wrap.items : null);
            _formulaConstants = ParseArray<FormulaConstantArray, FormulaConstantRow>(
                Resources.Load<TextAsset>("GameData/fishing_formula_constants"),
                wrap => wrap != null ? wrap.items : null);
            for (var i = 0; i < _formulaConstants.Length; i++)
            {
                var row = _formulaConstants[i];
                if (row == null || string.IsNullOrEmpty(row.key)) continue;
                if (row.key == "LENGTH_WEIGHT_A" && row.value > 0f) _lengthWeightA = row.value;
                if (row.key == "LENGTH_WEIGHT_B" && row.value > 0f) _lengthWeightB = row.value;
            }
            var returnRows = ParseArray<ReturnRulesArray, ReturnRulesRow>(
                Resources.Load<TextAsset>("GameData/return_rules"),
                wrap => wrap != null ? wrap.items : null);
            _returnRules = returnRows.Length > 0 ? returnRows[0] : new ReturnRulesRow();
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
