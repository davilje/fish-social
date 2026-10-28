using System;

namespace FishSocial.Desktop.Auth
{
    /// <summary>
    /// STEAM-DESKTOP-19: Overlay hook-end settle card copy.
    /// </summary>
    public sealed class OverlayHookPromptState
    {
        public string Kind = string.Empty;
        public string Title = string.Empty;
        public string Body = string.Empty;
        public string Button = DesktopLocalization.Text("ui.fishing.overlay.button.dismiss_prompt", "知道了");
        public long DeadlineMs;

        public bool IsVisible(long nowMs)
        {
            if (string.IsNullOrEmpty(Kind))
                return false;
            if (DeadlineMs <= 0)
                return true;
            return nowMs < DeadlineMs;
        }
    }

    public static class OverlayHookPrompt
    {
        public const string KindKeep = "hook_keep";
        public const string KindReturn = "hook_return";
        public const string KindEscape = "hook_escape";
        public const int AutoCloseMs = 5000;

        public static OverlayHookPromptState Keep(float sizeM, long nowMs)
        {
            return Create(KindKeep, DesktopLocalization.Text("ui.fishing.hook_prompt.title.catch_kept", "钓到了！"), DesktopLocalization.Format("ui.fishing.hook_prompt.message.catch_kept", (object)(SizeWeightLines(sizeM))), nowMs);
        }

        public static OverlayHookPromptState Return(float sizeM, int gold, long nowMs)
        {
            return Create(
                KindReturn,
                DesktopLocalization.Text("ui.fishing.hook_prompt.title.catch_kept", "钓到了！"),
                DesktopLocalization.Format("ui.fishing.hook_prompt.message.catch_released", (object)(SizeWeightLines(sizeM)), (object)(Math.Max(0, gold))),
                nowMs);
        }

        public static OverlayHookPromptState Escape(long nowMs)
        {
            return Create(KindEscape, DesktopLocalization.Text("ui.fishing.hook_prompt.title.catch_escaped", "脱钩了！"), DesktopLocalization.Text("ui.fishing.hook_prompt.message.catch_escaped", "鱼儿挣脱了鱼钩，溜走了"), nowMs);
        }

        static OverlayHookPromptState Create(string kind, string title, string body, long nowMs)
        {
            return new OverlayHookPromptState
            {
                Kind = kind,
                Title = title,
                Body = body,
                Button = DesktopLocalization.Text("ui.fishing.overlay.button.dismiss_prompt", "知道了"),
                DeadlineMs = nowMs + AutoCloseMs,
            };
        }

        static string SizeWeightLines(float sizeM)
        {
            var weight = DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(sizeM));
            return DesktopLocalization.Format("ui.fishing.hook_prompt.label.size_and_weight", (object)(sizeM.ToString("0.00")), (object)(weight));
        }
    }
}
