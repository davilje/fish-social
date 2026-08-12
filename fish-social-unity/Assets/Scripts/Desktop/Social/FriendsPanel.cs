using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Social
{
    /// <summary>
    /// Replaceable friends view. It only renders controller state.
    /// </summary>
    public sealed class FriendsPanel : MonoBehaviour
    {
        Text _content;
        SocialLobbyController _controller;

        public void Bind(SocialLobbyController controller, Text content)
        {
            _controller = controller;
            _content = content;
            _controller.FriendsChanged += Render;
            Render(_controller.Friends);
        }

        void Render(IReadOnlyList<SteamFriendInfo> friends)
        {
            if (_content == null)
                return;
            if (friends == null || friends.Count == 0)
            {
                _content.text = "好友列表为空，或 Steam 尚未返回好友。\n请点击“刷新好友”。";
                return;
            }
            var text = "Steam 好友：" + friends.Count;
            for (var i = 0; i < friends.Count; i++)
            {
                var friend = friends[i];
                text += "\n" + (i + 1) + ". " + friend.name + " · " +
                        (friend.online ? "在线" : "离线");
            }
            _content.text = text;
        }

        void OnDestroy()
        {
            if (_controller != null)
                _controller.FriendsChanged -= Render;
        }
    }
}
