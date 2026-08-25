using System;
using System.Collections.Generic;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Display-only copy of shared fish/economy catalog. Sell/inventory mutations
    /// still come from the server; this is used only to render names and estimates.
    /// </summary>
    public static class DesktopFishCatalog
    {
        public sealed class SpeciesInfo
        {
            public string Id;
            public string Name;
            public string Diet;
            public string DietLabel;
            public string CatchGroup;
            public string CatchGroupLabel;
            public string RarityTier;
            public string RarityLabel;
            public float TypicalMinM;
            public float TypicalMaxM;
            public int QualityMin;
            public int QualityMax;
            public float BiteWeight;
            public float BaseEscapeRate;
        }

        static readonly Dictionary<string, string> QualityNames = new Dictionary<string, string>
        {
            { "gray", "普通" },
            { "green", "优良" },
            { "blue", "稀有" },
            { "purple", "史诗" },
            { "red", "传说" },
            { "orange", "神话" },
            { "gold", "至尊" },
        };

        static readonly Dictionary<string, int> QualityBaseCoins = new Dictionary<string, int>
        {
            { "gray", 5 },
            { "green", 15 },
            { "blue", 40 },
            { "purple", 120 },
            { "red", 300 },
            { "orange", 800 },
            { "gold", 2000 },
        };

        public static readonly SpeciesInfo[] LegacySpeciesFallback =
        {
            S("crucian", "鲫鱼", "herbivore", 0.12f, 0.08f),
            S("carp", "鲤鱼", "herbivore", 0.1f, 0.1f),
            S("loach", "泥鳅", "omnivore", 0.1f, 0.1f),
        };

        static SpeciesInfo[] _speciesFromGameData;

        public static SpeciesInfo[] Species
        {
            get
            {
                EnsureSpeciesFromGameData();
                return _speciesFromGameData;
            }
        }

        static void EnsureSpeciesFromGameData()
        {
            if (_speciesFromGameData != null)
                return;
            var defs = DesktopGameData.Species;
            if (defs != null && defs.Length > 0)
            {
                var list = new SpeciesInfo[defs.Length];
                for (var i = 0; i < defs.Length; i++)
                    list[i] = FromGameDef(defs[i]);
                _speciesFromGameData = list;
                return;
            }
            _speciesFromGameData = LegacySpeciesFallback;
        }

        public static SpeciesInfo GetSpecies(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId))
                return null;
            var resolved = DesktopGameData.ResolveSpeciesId(speciesId);
            var all = Species;
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].Id == resolved || all[i].Id == speciesId)
                    return all[i];
            }
            var name = DesktopGameData.SpeciesName(resolved);
            if (!string.IsNullOrEmpty(name) && name != resolved)
            {
                var def = DesktopGameData.GetSpeciesDef(resolved);
                return def != null ? FromGameDef(def) : S(resolved, name, "omnivore", 0.1f, 0.1f);
            }
            return null;
        }

        public static string SpeciesName(string speciesId)
        {
            var species = GetSpecies(speciesId);
            return species != null ? species.Name : speciesId;
        }

        public static string QualityName(string quality)
        {
            if (string.IsNullOrEmpty(quality))
                return "未知";
            string name;
            return QualityNames.TryGetValue(quality, out name) ? name : quality;
        }

        public static int EstimateSellPrice(string quality, float sizeM, string speciesId = null)
        {
            return DesktopGameData.EstimateSellPrice(quality, sizeM, speciesId);
        }

        public static int LegacyEstimateSellPrice(string quality, float sizeM)
        {
            int baseCoins;
            if (!QualityBaseCoins.TryGetValue(quality ?? "", out baseCoins))
                baseCoins = 5;
            return baseCoins + (int)Math.Floor(sizeM * 10f);
        }

        public static string DietLabel(string diet)
        {
            switch (diet)
            {
                case "herbivore": return "草食";
                case "carnivore": return "肉食";
                default: return "杂食";
            }
        }

        public static string FormatBiteRate(SpeciesInfo species)
        {
            if (species == null)
                return "—";
            var rate = species.BiteWeight * 0.2f;
            return (rate * 100f).ToString("0.00") + "% / tick";
        }

        public static string TopBaits(SpeciesInfo species)
        {
            if (species == null)
                return "—";
            var baits = DesktopGameData.ListBaits();
            if (baits == null || baits.Length == 0)
                return "—";

            var ranked = new List<KeyValuePair<float, string>>(baits.Length);
            for (var i = 0; i < baits.Length; i++)
            {
                var bait = baits[i];
                if (bait == null || string.IsNullOrEmpty(bait.name))
                    continue;
                ranked.Add(new KeyValuePair<float, string>(
                    ScoreBaitForDiet(bait, species.Diet), bait.name));
            }
            if (ranked.Count == 0)
                return "—";
            ranked.Sort((a, b) => b.Key.CompareTo(a.Key));
            var count = Math.Min(3, ranked.Count);
            var text = string.Empty;
            for (var i = 0; i < count; i++)
            {
                if (i > 0) text += "、";
                text += ranked[i].Value;
            }
            return text;
        }

        static float ScoreBaitForDiet(DesktopGameData.BaitDef bait, string diet)
        {
            if (bait == null)
                return 0f;
            // Prefer matching diet bonus from GameData/baits（与商店同源）。
            float score;
            if (diet == "herbivore")
                score = bait.biteBonusHerbivore;
            else if (diet == "carnivore")
                score = bait.biteBonusCarnivore;
            else
                score = bait.biteBonusOmnivore;
            if (bait.diet == "any")
                score += 0.001f;
            else if (!string.IsNullOrEmpty(bait.diet) && bait.diet == diet)
                score += 0.02f;
            return score;
        }

        public static string FormatCodexProfile(SpeciesInfo species)
        {
            if (species == null)
                return "未知鱼种。";
            var size = species.TypicalMinM > 0f || species.TypicalMaxM > 0f
                ? species.TypicalMinM.ToString("0.00") + "–" + species.TypicalMaxM.ToString("0.00") + "m"
                : "—";
            var qMax = DesktopGameData.QualityRankLabel(species.QualityMax > 0 ? species.QualityMax : 7);
            return species.Name + "（" + species.Id + "）\n" +
                   "食性：" + species.DietLabel + "\n" +
                   "钓组：" + (string.IsNullOrEmpty(species.CatchGroupLabel) ? "—" : species.CatchGroupLabel) + "\n" +
                   "稀有度：" + (string.IsNullOrEmpty(species.RarityLabel) ? "—" : species.RarityLabel) + "\n" +
                   "品质上限：" + qMax + "\n" +
                   "体型参考：" + size + "（图鉴）\n" +
                   "推荐鱼饵：" + TopBaits(species);
        }

        static SpeciesInfo FromGameDef(DesktopGameData.SpeciesDef d)
        {
            if (d == null)
                return null;
            var diet = string.IsNullOrEmpty(d.diet) ? "omnivore" : d.diet;
            var info = S(d.speciesId, string.IsNullOrEmpty(d.name) ? d.speciesId : d.name, diet, 0.1f, 0.1f);
            info.CatchGroup = d.catchGroup;
            info.CatchGroupLabel = DesktopGameData.CatchGroupLabel(d.catchGroup);
            info.RarityTier = d.rarityTier;
            info.RarityLabel = DesktopGameData.RarityLabel(d.rarityTier);
            info.TypicalMinM = d.typicalMinM;
            info.TypicalMaxM = d.typicalMaxM;
            info.QualityMin = d.qualityMin > 0 ? d.qualityMin : 1;
            info.QualityMax = d.qualityMax > 0 ? d.qualityMax : 7;
            return info;
        }

        static SpeciesInfo S(string id, string name, string diet, float bite, float escape)
        {
            return new SpeciesInfo
            {
                Id = id,
                Name = name,
                Diet = diet,
                DietLabel = DietLabel(diet),
                BiteWeight = bite,
                BaseEscapeRate = escape,
            };
        }
    }
}
