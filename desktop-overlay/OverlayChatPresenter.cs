using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Threading;
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
            _logForeground = CozyTheme.Ink;
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

        public void UpdateMessages(OverlayChatDto[] chats, bool forceScrollToEnd = false)
        {
            var signature = Signature(chats);
            if (!forceScrollToEnd && signature == _lastSignature)
                return;
            _lastSignature = signature;
            _latest = chats;
            UpdateLatest(chats);
            if (_expanded) UpdateLog(chats, forceScrollToEnd);
            else { _logDirty = true; _forceScroll |= forceScrollToEnd; }
        }

        string _lastSignature;
        OverlayChatDto[] _latest;
        bool _expanded = true;
        bool _logDirty;
        bool _forceScroll;
        DispatcherOperation _scrollOperation;

        public void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            if (expanded && _logDirty)
            {
                UpdateLog(_latest, _forceScroll);
                _forceScroll = false;
                _logDirty = false;
            }
        }

        static string Signature(OverlayChatDto[] chats)
        {
            if (chats == null || chats.Length == 0)
                return string.Empty;
            var signature = new StringBuilder();
            foreach (var chat in chats)
            {
                // Length-prefixed values cannot collide when text contains separators.
                var line = chat == null ? string.Empty : FormatLine(chat);
                var id = chat?.MessageId ?? string.Empty;
                signature.Append(id.Length).Append(':').Append(id).Append(line.Length).Append(':').Append(line);
            }
            return signature.ToString();
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

        void UpdateLog(OverlayChatDto[] chats, bool forceScrollToEnd)
        {
            if (_logList == null)
                return;

            var previousOffset = _logScroll?.VerticalOffset ?? 0;
            var scrollToEnd = forceScrollToEnd || _logScroll == null ||
                              _logScroll.ScrollableHeight <= 0 ||
                              previousOffset >= _logScroll.ScrollableHeight - 4;
            var lines = new List<string>();
            if (chats != null)
                foreach (var chat in chats)
                    if (chat != null && !string.IsNullOrWhiteSpace(chat.Text)) lines.Add(FormatLine(chat));
            if (lines.Count == 0) lines.Add("暂无公屏消息");

            // Keep the longest retained suffix when the server's 20-message window
            // advances. Existing TextBlocks survive appends and unchanged snapshots.
            var offset = 0;
            for (var candidate = 0; candidate < _logList.Children.Count; candidate++)
            {
                var overlap = Math.Min(_logList.Children.Count - candidate, lines.Count);
                var matches = overlap > 0;
                for (var i = 0; matches && i < overlap; i++)
                    matches = ((_logList.Children[candidate + i] as TextBlock)?.Text == lines[i]);
                if (matches) { offset = candidate; break; }
            }
            for (var i = 0; i < offset; i++)
            {
                var removed = (TextBlock)_logList.Children[0];
                previousOffset = Math.Max(0, previousOffset - removed.ActualHeight - removed.Margin.Top - removed.Margin.Bottom);
                _logList.Children.RemoveAt(0);
            }
            while (_logList.Children.Count > lines.Count) _logList.Children.RemoveAt(_logList.Children.Count - 1);
            for (var i = 0; i < lines.Count; i++)
            {
                if (i == _logList.Children.Count) _logList.Children.Add(CreateLogLine(lines[i]));
                else if (((TextBlock)_logList.Children[i]).Text != lines[i])
                    ((TextBlock)_logList.Children[i]).Text = lines[i];
            }

            if (_logScroll == null) return;
            _scrollOperation?.Abort();
            // Let WPF finish its normal layout pass; never force it per message.
            _scrollOperation = _logScroll.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _scrollOperation = null;
                if (scrollToEnd) _logScroll.ScrollToEnd();
                else _logScroll.ScrollToVerticalOffset(previousOffset);
            }));
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
