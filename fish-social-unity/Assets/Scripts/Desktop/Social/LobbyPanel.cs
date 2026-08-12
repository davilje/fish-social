using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Social
{
    /// <summary>
    /// Replaceable Lobby view. It does not know Steamworks or REST details.
    /// </summary>
    public sealed class LobbyPanel : MonoBehaviour
    {
        Text _content;
        SocialLobbyController _controller;

        public void Bind(SocialLobbyController controller, Text content)
        {
            _controller = controller;
            _content = content;
            _controller.StateChanged += OnStateChanged;
            _controller.LobbyMembersChanged += OnMembersChanged;
            OnStateChanged(_controller.State, null);
        }

        void OnStateChanged(SocialLobbyState state, string message)
        {
            if (_content == null)
                return;
            var text = "Lobby 状态：" + state +
                       "\nLobby ID：" + (_controller.CurrentLobbyId ?? "—") +
                       "\n鱼塘：" + (_controller.CurrentPondId ?? "—");
            if (!string.IsNullOrEmpty(message))
                text += "\n" + message;
            _content.text = text;
        }

        void OnMembersChanged(IReadOnlyList<string> members)
        {
            if (_content == null || members == null)
                return;
            _content.text += "\nLobby 成员：" + members.Count;
        }

        void OnDestroy()
        {
            if (_controller == null)
                return;
            _controller.StateChanged -= OnStateChanged;
            _controller.LobbyMembersChanged -= OnMembersChanged;
        }
    }
}
