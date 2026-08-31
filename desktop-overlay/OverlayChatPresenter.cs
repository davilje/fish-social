using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FishSocialOverlay
{
    /// <summary>
    /// Chat dock preview line + expandable scroll log.
    /// </summary>
    internal sealed class OverlayChatPresenter
    {
        readonly TextBlock _preview;
        readonly ScrollViewer _logScroll;
        readonly StackPanel _logList;
        FontFamily _logFontFamily;
        double _logFontSize = 12;
        Brush _logForeground;

        public OverlayChatPresenter(TextBlock preview, ScrollViewer logScroll, StackPanel logList)
        {
            _preview = preview;
            _logScroll = logScroll;
            _logList = logList;
            _logForeground = new SolidColorBrush(Color.FromRgb(0xD4, 0xE3, 0xEA));
        }

        public void ConfigureLogStyle(FontFamily fontFamily, double fontSize, Brush foreground)
        {
            if (fontFamily != null)
                _logFontFamily = fontFamily;
            if (fontSize > 0)
                _logFontSize = fontSize;
            if (foreground != null)
                _logForeground = foreground;
        }

        public void UpdateMessages(OverlayChatDto[] chats, bool scrollToEnd = true)
        {
            UpdateLatest(chats);
            UpdateLog(chats, scrollToEnd);
        }

        public void UpdateLatest(OverlayChatDto[] chats)
        {
            if (_preview == null)
                return;

            if (chats == null || chats.Length == 0)
            {
                _preview.Text = "暂无公屏消息";
                return;
            }

            OverlayChatDto latest = null;
            for (var i = chats.Length - 1; i >= 0; i--)
            {
                if (chats[i] != null && !string.IsNullOrWhiteSpace(chats[i].Text))
                {
                    latest = chats[i];
                    break;
                }
            }

            if (latest == null)
            {
                _preview.Text = "暂无公屏消息";
                return;
            }

            _preview.Text = FormatLine(latest);
        }

        void UpdateLog(OverlayChatDto[] chats, bool scrollToEnd)
        {
            if (_logList == null)
                return;

            _logList.Children.Clear();
            if (chats == null || chats.Length == 0)
            {
                _logList.Children.Add(CreateLogLine("暂无公屏消息"));
                return;
            }

            var appended = 0;
            for (var i = 0; i < chats.Length; i++)
            {
                if (chats[i] == null || string.IsNullOrWhiteSpace(chats[i].Text))
                    continue;
                _logList.Children.Add(CreateLogLine(FormatLine(chats[i])));
                appended++;
            }

            if (appended == 0)
                _logList.Children.Add(CreateLogLine("暂无公屏消息"));

            if (scrollToEnd && _logScroll != null)
            {
                _logScroll.UpdateLayout();
                _logScroll.ScrollToEnd();
            }
        }

        TextBlock CreateLogLine(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4),
                FontFamily = _logFontFamily,
                FontSize = _logFontSize,
                Foreground = _logForeground,
            };
        }

        static string FormatLine(OverlayChatDto chat)
        {
            var nick = string.IsNullOrWhiteSpace(chat.Nickname) ? "钓友" : chat.Nickname.Trim();
            return nick + "：" + (chat.Text ?? string.Empty).Trim();
        }

        public static bool ContainsText(OverlayChatDto[] chats, string text)
        {
            if (chats == null || string.IsNullOrEmpty(text))
                return false;
            for (var i = 0; i < chats.Length; i++)
            {
                if (chats[i] != null && chats[i].Text == text)
                    return true;
            }

            return false;
        }
    }
}
