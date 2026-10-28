using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using FishSocial.Localization;

namespace FishSocialOverlay
{
    internal static class OverlayLocalizationCatalogLoader
    {
        [DataContract]
        sealed class CatalogDocument
        {
            [DataMember(Name = "entries")]
            public CatalogEntry[] Entries { get; set; }
        }

        // Keep JSON's optional fields optional. The shared Unity entry is marked
        // Serializable, which otherwise makes every field required by this serializer.
        [DataContract]
        sealed class CatalogEntry
        {
            [DataMember(Name = "key")] public string Key { get; set; }
            [DataMember(Name = "sourceText")] public string SourceText { get; set; }
            [DataMember(Name = "zhCN")] public string Chinese { get; set; }
            [DataMember(Name = "enUS")] public string English { get; set; }
            [DataMember(Name = "dataText")] public bool DataText { get; set; }
            [DataMember(Name = "dataArguments")] public int[] DataArguments { get; set; }
        }

        public static LocalizationEntry[] Read(Stream stream)
        {
            var document = (CatalogDocument)new DataContractJsonSerializer(typeof(CatalogDocument)).ReadObject(stream);
            if (document?.Entries == null) return Array.Empty<LocalizationEntry>();
            var entries = new LocalizationEntry[document.Entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = document.Entries[i];
                if (entry == null) continue;
                entries[i] = new LocalizationEntry
                {
                    key = entry.Key,
                    sourceText = entry.SourceText,
                    zhCN = entry.Chinese,
                    enUS = entry.English,
                    dataText = entry.DataText,
                    dataArguments = entry.DataArguments,
                };
            }
            return entries;
        }
    }
}
