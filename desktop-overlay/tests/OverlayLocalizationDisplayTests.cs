using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using FishSocial.Localization;
using FishSocialOverlay;

static class OverlayLocalizationDisplayTests
{
    static int checks;
    static int failures;
    static void Equal(object actual, object expected, string reason)
    {
        checks++;
        if (Equals(actual, expected)) return;
        failures++;
        Console.WriteLine("FAIL " + reason + "\n expected: " + expected + "\n actual: " + actual);
    }
    static string T(string key) => OverlayLocalization.Text(key);
    static void Main()
    {
        OverlayLocalization.SetLanguage("zh-CN");
        var fishing = new ContentControl { Content = "开始钓鱼" };
        OverlayLocalization.SetKey(fishing, "ui.fishing.overlay.button.start_fishing");
        OverlayLocalization.SetContent(fishing, T("ui.fishing.overlay.button.stop_fishing"));
        fishing.RaiseLoaded();
        Equal(fishing.Content, "收杆", "First Loaded must preserve an already applied fishing state");
        OverlayLocalization.SetLanguage("en-US");
        Equal(fishing.Content, T("ui.fishing.overlay.button.stop_fishing"), "Dynamic state binding must remain active after Loaded");

        var xamlOrder = new TextBlock { Text = "暂无公屏消息" };
        OverlayLocalization.SetKey(xamlOrder, "ui.social.overlay_chat.empty.public_messages");
        xamlOrder.RaiseLoaded();
        Equal(xamlOrder.Text, T("ui.social.overlay_chat.empty.public_messages"), "Authored XAML fallback must initialize through attached Key");
        var external = new TextBlock();
        OverlayLocalization.SetKey(external, "ui.social.overlay_chat.empty.public_messages");
        external.Text = "直接赋值的玩家文本";
        external.RaiseLoaded();
        Equal(external.Text, "直接赋值的玩家文本", "Direct assignment before first Loaded must supersede attached Key");
        OverlayLocalization.SetLanguage("zh-CN");
        Equal(external.Text, "直接赋值的玩家文本", "Direct assignment must remain authoritative on language changes");
        OverlayLocalization.SetLanguage("en-US");
        var chat = new TextBlock();
        OverlayLocalization.SetKey(chat, "ui.social.overlay_chat.empty.public_messages");
        OverlayLocalization.SetText(chat, "玩家：你好");
        chat.RaiseLoaded();
        Equal(chat.Text, "玩家：你好", "First Loaded must not replace received chat with empty-state text");

        var toggle = new ContentControl();
        OverlayLocalization.SetToolTipKey(toggle, "ui.fishing.hud_layout.tooltip.expand_chat");
        OverlayLocalization.SetToolTip(toggle, T("ui.fishing.hud_layout.tooltip.collapse_chat"));
        toggle.RaiseLoaded();
        Equal(toggle.ToolTip, T("ui.fishing.hud_layout.tooltip.collapse_chat"), "First Loaded must preserve expanded chat tooltip");
        var externalTip = new ContentControl();
        OverlayLocalization.SetToolTipKey(externalTip, "ui.fishing.hud_layout.tooltip.expand_chat");
        externalTip.ToolTip = "直接赋值的提示";
        externalTip.RaiseLoaded();
        Equal(externalTip.ToolTip, "直接赋值的提示", "Direct tooltip assignment must supersede attached Key");

        OverlayLocalization.SetLanguage("zh-CN");
        var raw = new ContentControl();
        OverlayLocalization.SetContent(raw, T("ui.fishing.hud.config_button.groundbait"));
        // Retain the obsolete binding to make the normal pre-GC state deterministic.
        var table = typeof(OverlayLocalization).GetField("bindings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        var args = new object[] { raw, null };
        table.GetType().GetMethod("TryGetValue").Invoke(table, args);
        var dictionary = (System.Collections.IDictionary)args[1];
        var obsolete = dictionary[ContentControl.ContentProperty];
        OverlayLocalization.SetContent(raw, (object)null);
        OverlayLocalization.SetContent(raw, "打窝");
        OverlayLocalization.SetLanguage("en-US");
        Equal(raw.Content, "打窝", "A forgotten binding must not overwrite replacement text that happens to match its old value");
        GC.KeepAlive(obsolete);

        foreach (var language in new[] { "zh-CN", "en-US" })
        {
            OverlayLocalization.SetLanguage(language);
            Equal(OverlayLocalization.IsMessage("今日钓鱼时长已用完", "ui.fishing.session.error.daily_time_exhausted"), true, "Chinese IPC quota state in " + language);
            Equal(OverlayLocalization.IsMessage("You have used all your fishing time for today", "ui.fishing.session.error.daily_time_exhausted"), true, "English IPC quota state in " + language);
            Equal(OverlayLocalization.IsMessage("", "ui.fishing.session.error.daily_time_exhausted"), false, "Normal quota state in " + language);
        }

        var wire = IpcMessage.Parse(@"{""type"":""state"",""sequence"":100,""language"":""en-US"",""recentChats"":[{""messageId"":""player"",""userId"":""u1"",""nickname"":""开始钓鱼"",""text"":""巡警来了！快跑！""},{""messageId"":""system"",""userId"":""system"",""nickname"":""系统"",""text"":""鲫鱼钓到了【普通】的鲫鱼！""}],""guideTip"":""③ 正在装饵…"",""overlayPromptTitle"":""🎣 鱼上钩了！"",""overlayPromptBody"":""鲫鱼\n【灰色】\n尺寸：0.18 米\n已自动收入背包""}");
        var preview = new TextBlock(); var log = new StackPanel();
        var presenter = new OverlayChatPresenter(preview, new ScrollViewer(), log);
        OverlayLocalization.SetLanguage(wire.Language);
        presenter.UpdateMessages(wire.RecentChats);
        Equal(((TextBlock)log.Children[0]).Text, "开始钓鱼：巡警来了！快跑！", "Player nickname and chat must remain verbatim after IPC");
        Equal(preview.Text, "System：鲫鱼 caught a [Common] Crucian Carp!", "Announcement must translate while preserving a nickname equal to a fish name");
        Equal(OverlayLocalization.Message(wire.GuideTip), T("ui.onboarding.guide.baiting"), "IPC guide must resolve through real catalog");
        Equal(OverlayLocalization.Message(wire.OverlayPromptBody), T("ui.onboarding.catch_prompt.body"), "Multiline IPC catch prompt must translate");
        OverlayLocalization.SetLanguage("zh-CN");
        Equal(preview.Text, "系统：鲫鱼钓到了【普通】的鲫鱼！", "Retained announcement must switch back without a new snapshot");
        Equal(((TextBlock)log.Children[0]).Text, "开始钓鱼：巡警来了！快跑！", "Language toggle must never translate player chat");
        presenter.SetExpanded(false);
        OverlayLocalization.SetLanguage("en-US");
        presenter.UpdateMessages(wire.RecentChats);
        presenter.SetExpanded(true);
        Equal(((TextBlock)log.Children[1]).Text, preview.Text, "Deferred chat log must use the new language");

        // Exhaust the real static copy used by IPC onboarding and patrol messages.
        LocalizationEntry[] entries;
        using (var stream = File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources/Localization/text-catalog.json")))
            entries = OverlayLocalizationCatalogLoader.Read(stream);
        foreach (var entry in entries.Where(e => e.sourceText == "{0}钓到了【{1}】的{2}！"))
            Equal(entry.dataArguments != null && entry.dataArguments.Contains(1) && entry.dataArguments.Contains(2) && !entry.dataArguments.Contains(0), true,
                "Every announcement template must translate quality/species and preserve nickname: " + entry.key);
        foreach (var entry in entries.Where(e => e.key.StartsWith("ui.onboarding.guide.") || e.key.StartsWith("ui.onboarding.catch_prompt.") || e.key == "ui.common.police.message.run"))
            Equal(OverlayLocalization.Message(entry.zhCN), entry.enUS, "Actual IPC catalog mapping: " + entry.key);
        Console.WriteLine("Overlay display tests: " + checks + " checks; " + failures + " failures. Actual production binding/chat/IPC code, modelled WPF DP lifecycle; no pixel-layout claims.");
        if (failures > 0) Environment.Exit(1);
    }
}
