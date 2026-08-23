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

        public static readonly SpeciesInfo[] Species =
        {
            S("crucian", "鲫鱼", "herbivore", 0.12f, 0.08f),
            S("tilapia", "罗非鱼", "herbivore", 0.12f, 0.08f),
            S("perch", "河鲈", "omnivore", 0.12f, 0.08f),
            S("carp", "鲤鱼", "herbivore", 0.1f, 0.1f),
            S("herring", "鲱鱼", "omnivore", 0.1f, 0.1f),
            S("mackerel", "鲭鱼", "omnivore", 0.1f, 0.1f),
            S("cod", "鳕鱼", "omnivore", 0.1f, 0.1f),
            S("snapper", "鲷鱼", "omnivore", 0.1f, 0.1f),
            S("catfish", "鲶鱼", "omnivore", 0.1f, 0.1f),
            S("koi", "锦鲤", "omnivore", 0.1f, 0.1f),
            S("bass", "大口黑鲈", "carnivore", 0.08f, 0.14f),
            S("trout", "鳟鱼", "carnivore", 0.08f, 0.14f),
            S("mandarin", "桂鱼", "carnivore", 0.08f, 0.14f),
            S("eel", "鳗鱼", "carnivore", 0.08f, 0.14f),
            S("topmouth", "翘嘴", "carnivore", 0.08f, 0.14f),
            S("tuna", "黄鳍金枪鱼", "carnivore", 0.06f, 0.18f),
            S("salmon", "三文鱼", "carnivore", 0.06f, 0.18f),
            S("pike", "狗鱼", "carnivore", 0.06f, 0.18f),
            S("marlin", "蓝旗鱼", "carnivore", 0.04f, 0.25f),
            S("sturgeon", "鲟鱼", "carnivore", 0.04f, 0.25f),
        };

        public static SpeciesInfo GetSpecies(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId))
                return null;
            for (var i = 0; i < Species.Length; i++)
            {
                if (Species[i].Id == speciesId)
                    return Species[i];
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
