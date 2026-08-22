using System;
using System.Collections;
using UnityEngine;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// STEAM-DESKTOP-12: F8 toggles a floating Debug action list. Overlay has a matching panel.
    /// </summary>
    public sealed class DesktopGameplayDebugMenu : MonoBehaviour
    {
        public static readonly string[] Actions =
        {
            "level_up",
            "level_max",
            "pond_level_up",
            "pond_level_max",
            "add_gold",
            "police_raid",
            "grant_fish",
            "advance_fee_2h",
        };

        static readonly string[] Labels =
        {
            "升级",
            "升至满级",
            "升级当前鱼塘熟练度",
            "升级当前鱼塘熟练度到满级",
            "获得 1000000 金钱",
            "一键出警",
            "获得鱼获",
            "当前鱼塘钓鱼时长 +2 小时",
        };

        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        Action<string> _toast;
        Action _onProgressChanged;
        bool _open;
        bool _busy;
        Coroutine _routine;
        Vector2 _scroll;

        public void Configure(
            IAuthenticatedApiClient api,
            SocialPondSessionController pond,
            Action<string> toast,
            Action onProgressChanged)
        {
            _api = api;
            _pond = pond;
            _toast = toast;
            _onProgressChanged = onProgressChanged;
        }

        public void Toggle()
        {
            if (!GameplayDebugGate.IsClientEnabled())
                return;
            _open = !_open;
        }

        public bool IsOpen => _open;

        public void RunAction(string action)
        {
            if (!GameplayDebugGate.IsClientEnabled() || _busy)
                return;
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(RunActionRoutine(action));
        }

        void Update()
        {
            if (!GameplayDebugGate.IsClientEnabled())
                return;
            if (Input.GetKeyDown(KeyCode.F8))
                Toggle();
            if (_open && Input.GetKeyDown(KeyCode.Escape))
                _open = false;
        }

        void OnGUI()
        {
            if (!GameplayDebugGate.IsClientEnabled() || !_open)
                return;

            const float width = 360f;
            const float height = 420f;
            var rect = new Rect(Screen.width - width - 24f, 72f, width, height);
            GUI.Box(rect, "玩法 Debug（F8 关闭）");
            var inner = new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, rect.height - 44f);
            GUILayout.BeginArea(inner);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUI.enabled = !_busy;
            for (var i = 0; i < Actions.Length; i++)
            {
                if (GUILayout.Button(Labels[i], GUILayout.Height(32)))
                    RunAction(Actions[i]);
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        IEnumerator RunActionRoutine(string action)
        {
            _busy = true;
            if (_api == null || !_api.CanUse)
            {
                _toast?.Invoke("请先登录后再使用 Debug。");
                _busy = false;
                _routine = null;
                yield break;
            }

            _toast?.Invoke("正在执行 Debug…");
            yield return _api.PostGameplayDebug(action, (ok, message) =>
            {
                _toast?.Invoke(ok
                    ? (string.IsNullOrEmpty(message) ? "Debug 完成" : message)
                    : (string.IsNullOrEmpty(message) ? "Debug 失败" : message));
            });
            _onProgressChanged?.Invoke();
            _busy = false;
            _routine = null;
        }
    }
}
