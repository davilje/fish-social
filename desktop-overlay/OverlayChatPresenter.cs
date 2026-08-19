using System.Windows;
using System.Windows.Controls;

namespace FishSocialOverlay
{
    /// <summary>
    /// Collapsed chat dock: latest message preview line.
    /// </summary>
    internal sealed class OverlayChatPresenter
    {
        readonly TextBlock _preview;

        public OverlayChatPresenter(TextBlock preview)
        {
            _preview = preview;
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

            var nick = string.IsNullOrWhiteSpace(latest.Nickname) ? "钓友" : latest.Nickname;
            _preview.Text = nick + "：" + (latest.Text ?? string.Empty).Trim();
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
