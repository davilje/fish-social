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
        public string Button = "知道了";
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
            return Create(KindKeep, "钓到了！", SizeWeightLines(sizeM) + "\n已放入背包", nowMs);
        }

        public static OverlayHookPromptState Return(float sizeM, int gold, long nowMs)
        {
            return Create(
                KindReturn,
                "钓到了！",
                SizeWeightLines(sizeM) + "\n回鱼价格：" + Math.Max(0, gold) + " 金币",
                nowMs);
        }

        public static OverlayHookPromptState Escape(long nowMs)
        {
            return Create(KindEscape, "脱钩了！", "鱼儿挣脱了鱼钩，溜走了", nowMs);
        }

        static OverlayHookPromptState Create(string kind, string title, string body, long nowMs)
        {
            return new OverlayHookPromptState
            {
                Kind = kind,
                Title = title,
                Body = body,
                Button = "知道了",
                DeadlineMs = nowMs + AutoCloseMs,
            };
        }

        static string SizeWeightLines(float sizeM)
        {
            var weight = DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(sizeM));
            return "尺寸：" + sizeM.ToString("0.00") + "m\n体重：" + weight;
        }
    }
}
