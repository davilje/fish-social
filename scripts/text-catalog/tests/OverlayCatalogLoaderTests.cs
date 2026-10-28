using System;
using System.IO;
using System.Text;
using FishSocial.Localization;
using FishSocialOverlay;

static class OverlayCatalogLoaderTests
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Main()
    {
        // Optional metadata is absent on most exported UI entries.
        const string json = @"{""schemaVersion"":1,""entries"":[
            {""key"":""ui.test.hello"",""sourceText"":""你好"",""zhCN"":""你好"",""enUS"":""Hello""},
            {""key"":""game.test.fish"",""sourceText"":""鲫鱼"",""zhCN"":""鲫鱼"",""enUS"":""Crucian Carp"",""dataText"":true},
            {""key"":""server.test.catch"",""sourceText"":""{0}钓到{1}"",""zhCN"":""{0}钓到{1}"",""enUS"":""{0} caught {1}"",""dataArguments"":[1]}
        ]}";
        LocalizationEntry[] entries;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            entries = OverlayLocalizationCatalogLoader.Read(stream);
        Check(entries.Length == 3, "All catalog entries must load.");
        Check(!entries[0].dataText && entries[0].dataArguments == null,
            "Omitted optional metadata must retain its defaults.");
        Check(entries[1].dataText && entries[2].dataArguments[0] == 1,
            "Present data metadata must survive JSON loading.");
        LocalizationCatalog.Load(entries);
        LocalizationCatalog.SetLanguage("en-US");
        Check(LocalizationCatalog.Text("ui.test.hello") == "Hello", "Loaded UI entry must translate.");
        var message = LocalizationCatalog.TranslateMessage("鲫鱼钓到鲫鱼");
        Check(message == "鲫鱼 caught Crucian Carp", "Loaded metadata must preserve nicknames and translate game data.");
        LocalizationCatalog.SetLanguage("zh-CN");
        Check(LocalizationCatalog.Render(message) == "鲫鱼钓到鲫鱼", "Loaded messages must support switching back.");
        Console.WriteLine("Overlay catalog loader tests passed.");
    }
}
