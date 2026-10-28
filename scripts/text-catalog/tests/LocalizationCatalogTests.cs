using System;
using System.Collections.Generic;
using System.Globalization;
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
            E("ui.common.date", "M月d日", "MMM d"),
            E("ui.common.date_offset", "M月d日 HH:mm zzz", "MMM d, yyyy HH:mm zzz"),
            E("ui.common.date_standard", "D", "D"),
            E("ui.common.separator", "、", ", "),
            E("ui.common.padded_clue", "  你好\n", "\tHello \r\n"),
            E("ui.common.detail_lines", "鲫鱼\n中文描述", "Crucian Carp\nEnglish details"),
            E("ui.common.bait_list", "饵、另一种饵", "Very Long Bait Name、Other Bait"),
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
        CompositionAndDateTests();
        BuilderAndTrimTests();
        TransformTests();
        LocalizationCatalog.SetLanguage("en-US");
        Parallel.For(0, 250, i => {
            var output = LocalizationCatalog.Format("ui.common.catch", "User " + i, LocalizationCatalog.BySource("鲫鱼"));
            if (output != "User " + i + " caught Crucian Carp!") throw new Exception("Concurrent render mismatch.");
        });
        LocalizationCatalog.SetLanguage("zh-CN");
        LocalizationCatalog.LanguageChanged -= changed;
        GarbageCollectionIdentityTests();
        Console.WriteLine("PASS: " + assertions + " localization assertions and 250 concurrent renders.");
        return 0;
    }

    static void CompositionAndDateTests()
    {
        var date = new DateTime(2026, 10, 29, 15, 4, 0, DateTimeKind.Unspecified);
        var offsetDate = new DateTimeOffset(date, TimeSpan.FromHours(8));
        var shortDate = LocalizationCatalog.FormatDate("ui.common.date", date);
        var dateWithOffset = LocalizationCatalog.FormatDate("ui.common.date_offset", offsetDate);
        var standardDate = LocalizationCatalog.FormatDate("ui.common.date_standard", date);
        var nickname = new string(new[] { '你', '好' });
        var values = new List<string> { LocalizationCatalog.BySource("鲫鱼"), nickname, null, shortDate };
        var list = LocalizationCatalog.Join(LocalizationCatalog.Text("ui.common.separator"), values);
        values[0] = "Changed after joining";
        var parts = new object[] { nickname, " / ", LocalizationCatalog.Concat("[", list, "]"), " #", 7, null };
        var combined = LocalizationCatalog.Concat(parts);
        parts[0] = "Changed after concatenating";
        var nestedFormat = LocalizationCatalog.Format("ui.common.catch", nickname, combined);

        Equal("10月29日", shortDate);
        Equal("10月29日 15:04 +08:00", dateWithOffset);
        Equal(date.ToString("D", CultureInfo.GetCultureInfo("zh-CN")), standardDate);
        Equal("鲫鱼、你好、、10月29日", list);
        Equal("你好 / [鲫鱼、你好、、10月29日] #7", combined);
        Equal("你好钓到了你好 / [鲫鱼、你好、、10月29日] #7！", nestedFormat);
        Check(LocalizationCatalog.IsLocalized(combined), "Concatenation must retain its nested render plans.");

        LocalizationCatalog.SetLanguage("en-US");
        Equal("Oct 29", LocalizationCatalog.Render(shortDate));
        Equal("Oct 29, 2026 15:04 +08:00", LocalizationCatalog.Render(dateWithOffset));
        Equal(date.ToString("D", CultureInfo.GetCultureInfo("en-US")), LocalizationCatalog.Render(standardDate));
        Equal("Crucian Carp, 你好, , Oct 29", LocalizationCatalog.Render(list));
        Equal("你好 / [Crucian Carp, 你好, , Oct 29] #7", LocalizationCatalog.Render(combined));
        Equal("你好 caught 你好 / [Crucian Carp, 你好, , Oct 29] #7!", LocalizationCatalog.Render(nestedFormat));
        Equal("你好", LocalizationCatalog.Render(nickname));

        LocalizationCatalog.SetLanguage("zh-CN");
        Equal("你好 / [鲫鱼、你好、、10月29日] #7", LocalizationCatalog.Render(combined));
        Equal("10月29日 15:04 +08:00", LocalizationCatalog.Render(dateWithOffset));
        Equal("", LocalizationCatalog.Join(null, new string[0]));
        Equal("ab", LocalizationCatalog.Join(null, new[] { "a", null, "b" }));
        Equal("", LocalizationCatalog.Concat((object[])null));
        var cacheKey = "cache";
        for (var i = 0; i < 64; i++) cacheKey = LocalizationCatalog.Concat(cacheKey, ":", i);
        Check(cacheKey.StartsWith("cache:0:1:2", StringComparison.Ordinal) && cacheKey.EndsWith(":63", StringComparison.Ordinal),
            "Plain cache keys and logs must preserve arbitrarily long concatenations.");
        Check(!LocalizationCatalog.IsLocalized(cacheKey), "Plain infrastructure strings must not acquire localization plans.");
        Check(!LocalizationCatalog.IsLocalized(LocalizationCatalog.Join("/", new[] { "path", "file" })),
            "Joining plain paths must not acquire localization plans.");
        var longDisplay = LocalizationCatalog.Text("ui.common.greeting");
        for (var i = 0; i < 64; i++) longDisplay = LocalizationCatalog.Concat(longDisplay, "/", i);
        LocalizationCatalog.SetLanguage("en-US");
        Check(LocalizationCatalog.Render(longDisplay).StartsWith("Hello/0/1/2", StringComparison.Ordinal) &&
            LocalizationCatalog.Render(longDisplay).EndsWith("/63", StringComparison.Ordinal),
            "Long localized concatenations must retain every fragment when re-rendered.");
        LocalizationCatalog.SetLanguage("zh-CN");
    }

    static void BuilderAndTrimTests()
    {
        var nickname = new string(new[] { '你', '好' });
        var mutable = new System.Text.StringBuilder("item");
        var builder = new LocalizedStringBuilder(LocalizationCatalog.Text("ui.common.greeting"))
            .Append(':').Append(nickname).Append(" / ").Append(LocalizationCatalog.BySource("鲫鱼"))
            .Append(' ').Append(7).AppendLine().Append(mutable);
        var first = builder.ToString();
        mutable.Append(" changed");
        builder.AppendLine().AppendLine(LocalizationCatalog.Text("ui.common.greeting"));
        var second = builder.ToString();
        builder.Clear().Append("replacement");
        var chinese = "你好:你好 / 鲫鱼 7" + Environment.NewLine + "item";
        Equal(chinese, first);
        Equal(chinese + Environment.NewLine + "你好" + Environment.NewLine, second);
        Equal("replacement", builder.ToString());
        var clue = LocalizationCatalog.Trim(LocalizationCatalog.Text("ui.common.padded_clue"));
        Equal("你好", clue);
        Check(LocalizationCatalog.IsLocalized(clue), "Trimming display clues must retain their translation plan.");
        var plainNickname = LocalizationCatalog.Trim(" \t你好  ");
        Check(!LocalizationCatalog.IsLocalized(plainNickname), "Trimming ordinary user text must not register a translation plan.");
        Equal(null, LocalizationCatalog.Trim(null));
        LocalizationCatalog.SetLanguage("en-US");
        var english = "Hello:你好 / Crucian Carp 7" + Environment.NewLine + "item";
        Equal(english, LocalizationCatalog.Render(first));
        Equal(english + Environment.NewLine + "Hello" + Environment.NewLine, LocalizationCatalog.Render(second));
        Equal("Hello", LocalizationCatalog.Render(clue));
        Equal("你好", LocalizationCatalog.Render(plainNickname));
        LocalizationCatalog.SetLanguage("zh-CN");
        Equal(chinese, LocalizationCatalog.Render(first));
        Equal("你好", LocalizationCatalog.Render(clue));
    }

    static void GarbageCollectionIdentityTests()
    {
        var entries = new LocalizationEntry[512];
        var labels = new string[entries.Length];
        var userLabels = new string[entries.Length];
        for (var i = 0; i < entries.Length; i++)
            entries[i] = E("ui.gc.label_" + i, "相同中文", "Label " + i);
        LocalizationCatalog.Load(entries);
        LocalizationCatalog.SetLanguage("zh-CN");
        for (var i = 0; i < entries.Length; i++)
        {
            labels[i] = LocalizationCatalog.Concat(LocalizationCatalog.Text(entries[i].key), "!");
            userLabels[i] = new string("相同中文!".ToCharArray());
        }
        for (var pass = 0; pass < 8; pass++)
        {
            Parallel.For(0, 1024, i => LocalizationCatalog.Text(entries[i % entries.Length].key));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var english = pass % 2 == 0;
            LocalizationCatalog.SetLanguage(english ? "en-US" : "zh-CN");
            for (var i = 0; i < entries.Length; i++)
            {
                Equal(english ? "Label " + i + "!" : "相同中文!", LocalizationCatalog.Render(labels[i]));
                Equal("相同中文!", LocalizationCatalog.Render(userLabels[i]));
                Check(!LocalizationCatalog.IsLocalized(userLabels[i]), "GC must not transfer a plan to equal player text.");
            }
        }
    }

    static void TransformTests()
    {
        var label = LocalizationCatalog.Text("ui.common.detail_lines");
        var title = LocalizationCatalog.Transform(label, current =>
        {
            var split = current.IndexOf('\n');
            return split >= 0 ? current.Substring(0, split) : current;
        });
        var description = LocalizationCatalog.Transform(label, current =>
        {
            var split = current.IndexOf('\n');
            return split >= 0 ? current.Substring(split + 1) : "";
        });
        var rich = LocalizationCatalog.Transform(label, current =>
        {
            var split = current.IndexOf('\n');
            return split > 0 ? "<b>" + current.Substring(0, split) + "</b>" + current.Substring(split) : current;
        });
        var bait = LocalizationCatalog.Transform(LocalizationCatalog.Text("ui.common.bait_list"), current =>
        {
            var split = current.IndexOf('、');
            return split > 0 ? current.Substring(0, split) : current;
        });
        Equal("鲫鱼", title);
        Equal("中文描述", description);
        Equal("饵", bait);
        LocalizationCatalog.SetLanguage("en-US");
        Equal("Crucian Carp", LocalizationCatalog.Render(title));
        Equal("English details", LocalizationCatalog.Render(description));
        Equal("<b>Crucian Carp</b>\nEnglish details", LocalizationCatalog.Render(rich));
        Equal("Very Long Bait Name", LocalizationCatalog.Render(bait));
        var nickname = LocalizationCatalog.Transform("你好", current => current.Substring(0, 1));
        Equal("你", nickname);
        Check(!LocalizationCatalog.IsLocalized(nickname), "Ordinary substring results must not acquire translation plans.");
        LocalizationCatalog.SetLanguage("zh-CN");
        Equal("鲫鱼", LocalizationCatalog.Render(title));
        Equal("中文描述", LocalizationCatalog.Render(description));
        Equal("<b>鲫鱼</b>\n中文描述", LocalizationCatalog.Render(rich));
        Equal("饵", LocalizationCatalog.Render(bait));
    }
}
