using System;
using System.Threading.Tasks;
using FishSocial.Localization;

static class LocalizationCatalogTests
{
    static int assertions;
    static void Equal(string expected, string actual)
    {
        assertions++;
        if (expected != actual) throw new Exception("Expected <" + expected + ">, received <" + actual + ">.");
    }
    static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    static LocalizationEntry E(string key, string source, string english, int[] data = null)
    {
        return new LocalizationEntry { key = key, sourceText = source, zhCN = source, enUS = english, dataArguments = data };
    }
    static LocalizationEntry[] Entries()
    {
        return new[] {
            E("ui.common.greeting", "你好", "Hello"),
            E("ui.common.amount", "金币 {0:N0}，共 {0:N0}", "Coins {0:N0}, total {0:N0}"),
            E("ui.common.different_formats", "金币 {0} / {0:N0}", "Coins {0} / {0:N0}"),
            E("game.codex.fish.crucian.name", "鲫鱼", "Crucian Carp"),
            E("game.fishing.pond_fish.crucian.name", "鲫鱼", "Crucian Carp"),
            new LocalizationEntry { key = "ui.codex.quality.rare", sourceText = "稀有", zhCN = "稀有", enUS = "Rare", dataText = true },
            E("game.common.ambiguous_one", "重名", "First Name"),
            E("game.common.ambiguous_two", "重名", "Second Name"),
            E("ui.common.catch", "{0}钓到了{1}！", "{0} caught {1}!"),
            E("server.common.catch", "{0}收获【{1}】！", "{0} landed [{1}]!", new[] { 1 }),
            E("ui.common.multiline", "第一行\n<color=red>第二行</color>", "First line\n<color=red>Second line</color>"),
            E("ui.common.english_missing", "仍用中文", ""),
            E("ui.common.malformed_translation", "数量 {0}", "Count {1}"),
            E("ui.common.escaped", "数值 {{{0}}}", "Value {{{0}}}"),
        };
    }
    static int Main()
    {
        LocalizationCatalog.Load(Entries());
        LocalizationCatalog.SetLanguage("zh-CN");
        var changes = 0;
        Action changed = () => changes++;
        LocalizationCatalog.LanguageChanged += changed;
        var hello = LocalizationCatalog.Text("ui.common.greeting");
        Equal("你好", hello);
        Check(!object.ReferenceEquals("你好", hello), "Returned values must not attach plans to interned strings.");
        Check(LocalizationCatalog.IsLocalized(hello), "Text creates a re-renderable string.");
        Check(!LocalizationCatalog.IsLocalized("你好"), "Ordinary user strings are not registered.");
        var nickname = new string(new[] { '鲫', '鱼' });
        var fish = LocalizationCatalog.BySource("鲫鱼");
        var catchText = LocalizationCatalog.Format("ui.common.catch", nickname, fish);
        Equal("鲫鱼钓到了鲫鱼！", catchText);
        var amount = LocalizationCatalog.Format("ui.common.amount", 1234);
        Equal("金币 1,234，共 1,234", amount);
        LocalizationCatalog.SetLanguage("en-US");
        Equal("Hello", LocalizationCatalog.Render(hello));
        Equal("鲫鱼 caught Crucian Carp!", LocalizationCatalog.Render(catchText));
        Equal("Coins 1,234, total 1,234", LocalizationCatalog.Render(amount));
        Equal("鲫鱼", LocalizationCatalog.Render(nickname));
        Equal("Rare", LocalizationCatalog.BySource("稀有"));
        Equal("你好", LocalizationCatalog.BySource("你好"));
        Check(object.ReferenceEquals(nickname, LocalizationCatalog.Render(nickname)), "Unmarked user text must preserve identity.");
        Check(changes == 1, "One language change fires one event.");
        LocalizationCatalog.SetLanguage("EN-us");
        Check(changes == 1, "Repeated or differently cased language does not fire another event.");
        Equal("First line\n<color=red>Second line</color>", LocalizationCatalog.Text("ui.common.multiline"));
        Equal("仍用中文", LocalizationCatalog.Text("ui.common.english_missing"));
        Equal("missing.key", LocalizationCatalog.Text("missing.key"));
        Equal("Fallback", LocalizationCatalog.Text("missing.key", "Fallback"));
        Equal("数量 3", LocalizationCatalog.Format("ui.common.malformed_translation", 3));
        Equal("Value {2}", LocalizationCatalog.Format("ui.common.escaped", 2));
        Equal("Value {2}", LocalizationCatalog.TranslateMessage("数值 {2}"));
        var translated = LocalizationCatalog.TranslateMessage("金币 1,234，共 1,234");
        Equal("Coins 1,234, total 1,234", translated);
        Equal("金币 1,234，共 4,567", LocalizationCatalog.TranslateMessage("金币 1,234，共 4,567"));
        Equal("Coins 1234 / 1,234", LocalizationCatalog.TranslateMessage("金币 1234 / 1,234"));
        Equal("Hi 你好 everyone", LocalizationCatalog.TranslateMessage("Hi 你好 everyone"));
        Equal("重名", LocalizationCatalog.BySource("重名"));
        Equal("鲫鱼 landed [Crucian Carp]!", LocalizationCatalog.TranslateMessage("鲫鱼收获【鲫鱼】！"));
        var plainUserText = "你好";
        Equal("你好", LocalizationCatalog.Render(plainUserText));
        LocalizationCatalog.SetLanguage("unsupported-locale");
        Equal("zh-CN", LocalizationCatalog.Language);
        Equal("金币 1,234，共 1,234", LocalizationCatalog.Render(translated));
        Equal("你好", LocalizationCatalog.TranslateMessage("Hello"));
        Check(changes == 2, "Unsupported language normalizes to Chinese once.");
        var beforeFailure = LocalizationCatalog.Text("ui.common.greeting");
        var rejected = false;
        try { LocalizationCatalog.Load(new[] { E("duplicate.key", "一", "One"), E("duplicate.key", "二", "Two") }); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Duplicate catalog keys must fail.");
        Equal("你好", LocalizationCatalog.Render(beforeFailure));
        Equal("你好", LocalizationCatalog.Text("ui.common.greeting"));
        LocalizationCatalog.SetLanguage("en-US");
        Parallel.For(0, 250, i => {
            var output = LocalizationCatalog.Format("ui.common.catch", "User " + i, LocalizationCatalog.BySource("鲫鱼"));
            if (output != "User " + i + " caught Crucian Carp!") throw new Exception("Concurrent render mismatch.");
        });
        LocalizationCatalog.SetLanguage("zh-CN");
        LocalizationCatalog.LanguageChanged -= changed;
        Console.WriteLine("PASS: " + assertions + " localization assertions and 250 concurrent renders.");
        return 0;
    }
}
