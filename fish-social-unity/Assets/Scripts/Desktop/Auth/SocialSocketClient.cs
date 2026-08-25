using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FishSocial.Desktop.Auth
{
    public interface ISocialSocketClient
    {
        bool IsConnected { get; }
        SocialSocketState State { get; }
        event Action<SocialSocketState, string> StateChanged;
        event Action<PondSnapshotDto> PondSnapshotReceived;
        event Action<PondUserDto> PondUserJoined;
        event Action<string> PondUserLeft;
        event Action<PondUserDto> PondUserUpdated;
        event Action<SessionTimerTickDto> SessionTimerTick;
        event Action<PendingFishCatchDto> FishBiteReceived;
        event Action<FishCatchSettledDto> FishCatchSettled;
        event Action<PondSessionSummaryDto> PondSessionSummaryReceived;
        event Action<FishInventoryItemDto[]> InventoryUpdated;
        event Action<ChatMessageDto> ChatMessageReceived;
        event Action<CodexUnlockDto> CodexUnlocked;
        event Action<AchievementUnlockDto> AchievementUnlocked;
        event Action<FriendRequestDto> FriendRequestReceived;
        event Action<DirectMessageDto> DmMessageReceived;
        event Action<PostLikedDto> PostLikedReceived;
        event Action<PostCommentedDto> PostCommentedReceived;
        event Action<PostCommentDeletedDto> PostCommentDeletedReceived;
        event Action<PoliceRaidDto> PoliceRaidReceived;
        event Action<string> ErrorReceived;

        void Connect(string accessToken, Action<bool, string> onCompleted);
        void RegisterPlayer(string playerId);
        void JoinPond(JoinPondPayload payload, Action<bool, string> onCompleted);
        void LeaveSpot(string pondId, Action<bool, string> onCompleted);
        void LeavePond(string pondId, string reason, Action<bool, string> onCompleted);
        void TakeSpot(TakeSpotPayload payload, Action<bool, string> onCompleted);
        void StartFishing(StartFishingPayload payload, Action<bool, string> onCompleted);
        void StartGroundbait(GroundbaitStartPayload payload, Action<bool, string> onCompleted);
        void StopFishing(string pondId, Action<bool, string> onCompleted);
        void AcceptCatch(string catchId, Action<bool, string> onCompleted);
        void SendChat(SendChatPayload payload, Action<bool, string> onCompleted);
        void Pump();
        void Disconnect();
    }

    /// <summary>
    /// Minimal Engine.IO v4 / Socket.IO v4 WebSocket adapter for Windows.
    /// Authentication is sent only in the Socket.IO connect payload as auth.token.
    /// It intentionally exposes only the protocol events needed by UNITY-P2.
    /// </summary>
    public sealed class SocketIoSocialSocketClient : ISocialSocketClient
    {
        const int AckTimeoutMs = 10000;
        readonly string _baseUrl;
        readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        readonly ConcurrentQueue<Action> _priorityMainThread =
            new ConcurrentQueue<Action>();
        readonly Dictionary<int, Action<bool, string>> _pendingAcks =
            new Dictionary<int, Action<bool, string>>();
        readonly object _ackLock = new object();
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        ClientWebSocket _socket;
        CancellationTokenSource _cancel;
        string _accessToken;
        int _nextAckId;
        bool _reconnectRequested;

        public bool IsConnected => State == SocialSocketState.Connected;
        public SocialSocketState State { get; private set; } = SocialSocketState.Disconnected;
        public event Action<SocialSocketState, string> StateChanged;
        public event Action<PondSnapshotDto> PondSnapshotReceived;
        public event Action<PondUserDto> PondUserJoined;
        public event Action<string> PondUserLeft;
        public event Action<PondUserDto> PondUserUpdated;
        public event Action<SessionTimerTickDto> SessionTimerTick;
        public event Action<PendingFishCatchDto> FishBiteReceived;
        public event Action<FishCatchSettledDto> FishCatchSettled;
        public event Action<PondSessionSummaryDto> PondSessionSummaryReceived;
        public event Action<FishInventoryItemDto[]> InventoryUpdated;
        public event Action<ChatMessageDto> ChatMessageReceived;
        public event Action<CodexUnlockDto> CodexUnlocked;
        public event Action<AchievementUnlockDto> AchievementUnlocked;
        public event Action<FriendRequestDto> FriendRequestReceived;
        public event Action<DirectMessageDto> DmMessageReceived;
        public event Action<PostLikedDto> PostLikedReceived;
        public event Action<PostCommentedDto> PostCommentedReceived;
        public event Action<PostCommentDeletedDto> PostCommentDeletedReceived;
        public event Action<PoliceRaidDto> PoliceRaidReceived;
        public event Action<string> ErrorReceived;

        public SocketIoSocialSocketClient(string baseUrl)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
        }

        public void Connect(string accessToken, Action<bool, string> onCompleted)
        {
            Debug.Log("[Pond] SocketIoSocialSocketClient.Connect called.");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                Debug.LogWarning("[Pond] Socket connect rejected: access token is empty.");
                Complete(false, "缺少 JWT，无法连接 Socket。", onCompleted);
                return;
            }
            if (IsConnected)
            {
                Complete(true, "Socket 已连接。", onCompleted);
                return;
            }

            _accessToken = accessToken;
            _reconnectRequested = true;
            SetState(SocialSocketState.Connecting, "正在连接实时服务。");
            _cancel?.Cancel();
            _cancel = new CancellationTokenSource();
            _ = ConnectLoop(onCompleted, _cancel.Token);
        }

        public void RegisterPlayer(string playerId)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(playerId))
                return;
            SendEvent("register_player", Quote(playerId), null);
        }

        public void JoinPond(JoinPondPayload payload, Action<bool, string> onCompleted)
        {
            SendEventWithAck("join_pond", JsonUtility.ToJson(payload), onCompleted);
        }

        public void LeaveSpot(string pondId, Action<bool, string> onCompleted)
        {
            SendEventWithAck(
                "leave_spot",
                JsonUtility.ToJson(new LeaveSpotPayload { pondId = pondId }),
                onCompleted);
        }

        public void LeavePond(string pondId, string reason, Action<bool, string> onCompleted)
        {
            SendEventWithAck(
                "leave_pond",
                JsonUtility.ToJson(new LeavePondPayload
                {
                    pondId = pondId,
                    reason = reason,
                }),
                onCompleted);
        }

        public void TakeSpot(TakeSpotPayload payload, Action<bool, string> onCompleted)
        {
            SendEventWithAck("take_spot", JsonUtility.ToJson(payload), onCompleted);
        }

        public void StartFishing(StartFishingPayload payload, Action<bool, string> onCompleted)
        {
            SendEventWithAck("start_fishing", JsonUtility.ToJson(payload), onCompleted);
        }

        public void StartGroundbait(GroundbaitStartPayload payload, Action<bool, string> onCompleted)
        {
            SendEventWithAck("groundbait_start", JsonUtility.ToJson(payload), onCompleted);
        }

        public void StopFishing(string pondId, Action<bool, string> onCompleted)
        {
            SendEventWithAck("stop_fishing", Quote(pondId), onCompleted);
        }

        public void AcceptCatch(string catchId, Action<bool, string> onCompleted)
        {
            SendEventWithAck("accept_catch", Quote(catchId), onCompleted);
        }

        public void SendChat(SendChatPayload payload, Action<bool, string> onCompleted)
        {
            SendEventWithAck("send_chat", JsonUtility.ToJson(payload), onCompleted);
        }

        public void Disconnect()
        {
            _reconnectRequested = false;
            var cancel = _cancel;
            _cancel = null;
            cancel?.Cancel();
            var socket = Interlocked.Exchange(ref _socket, null);
            if (socket != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { socket.Abort(); } catch { }
                    try { socket.Dispose(); } catch { }
                });
            }
            SetState(SocialSocketState.Disconnected, "实时服务已断开。");
        }

        public void Pump()
        {
            // ACKs and connection state must not wait behind a burst of pond
            // snapshots/user updates. Overlay commands are interactive and
            // should be acknowledged on the next available Unity frame.
            while (_priorityMainThread.TryDequeue(out var priorityAction))
                priorityAction?.Invoke();

            // Bound normal event work so a busy pond cannot monopolize the
            // Unity main thread and make the overlay controls feel laggy.
            var processed = 0;
            while (_mainThread.TryDequeue(out var action))
            {
                action?.Invoke();
                if (++processed >= 64)
                    break;
            }
        }

        async Task ConnectLoop(Action<bool, string> onCompleted, CancellationToken token)
        {
            try
            {
                Debug.Log("[Pond] Socket.IO handshake starting: " + BuildUri());
                _socket?.Dispose();
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(BuildUri(), token);
                Debug.Log("[Pond] WebSocket connected; sending Socket.IO auth packet.");
                await SendRaw("40" + JsonUtility.ToJson(new SocketAuthPayload { token = _accessToken }), token);
                await ReadLoop(onCompleted, token);
                if (_reconnectRequested && !token.IsCancellationRequested)
                {
                    Enqueue(() => SetState(SocialSocketState.Reconnecting, "实时服务正在重连。"));
                    await Task.Delay(1000, token);
                    await ConnectLoop(null, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during explicit disconnect or replacement connect.
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Pond] Socket.IO connection failed: " + error.Message);
                Enqueue(() =>
                {
                    SetState(SocialSocketState.Failed, "实时服务连接失败。");
                    ErrorReceived?.Invoke("实时服务连接失败：" + error.Message);
                    onCompleted?.Invoke(false, "实时服务连接失败。");
                });
                if (_reconnectRequested && !token.IsCancellationRequested)
                {
                    Enqueue(() => SetState(SocialSocketState.Reconnecting, "实时服务正在重连。"));
                    await Task.Delay(1000, token);
                    await ConnectLoop(null, token);
                }
            }
        }

        async Task ReadLoop(Action<bool, string> onCompleted, CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            while (_socket != null && _socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var message = new StringBuilder();
                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                while (!result.EndOfMessage)
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                var packet = message.ToString();
                if (packet == "2")
                {
                    await SendRaw("3", token);
                    continue;
                }
                if (packet.StartsWith("40", StringComparison.Ordinal) ||
                    packet.StartsWith("43", StringComparison.Ordinal) ||
                    packet.StartsWith("44", StringComparison.Ordinal))
                    EnqueuePriority(() => HandlePacket(packet, onCompleted));
                else
                    Enqueue(() => HandlePacket(packet, onCompleted));
            }
        }

        void HandlePacket(string packet, Action<bool, string> onCompleted)
        {
            if (packet.StartsWith("40", StringComparison.Ordinal))
            {
                SetState(SocialSocketState.Connected, "实时服务已连接。");
                onCompleted?.Invoke(true, "Socket 连接成功。");
                return;
            }
            if (packet.StartsWith("41", StringComparison.Ordinal))
            {
                SetState(SocialSocketState.Disconnected, "Socket 被服务端断开。");
                ErrorReceived?.Invoke("Socket 被服务端断开。");
                return;
            }
            if (packet.StartsWith("42", StringComparison.Ordinal))
            {
                HandleEventPacket(packet);
                return;
            }
            if (packet.StartsWith("43", StringComparison.Ordinal))
            {
                HandleAckPacket(packet);
                return;
            }
            if (packet.StartsWith("44", StringComparison.Ordinal))
            {
                ErrorReceived?.Invoke("Socket 连接被拒绝。");
                CompletePending(false, "Socket 连接被拒绝。");
            }
        }

        void HandleEventPacket(string packet)
        {
            if (!TryReadArrayPacket(packet.Substring(2), out var eventName, out var payload))
                return;
            try
            {
                DispatchEvent(eventName, payload);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Pond] Socket event '" + eventName + "' failed: " + error);
                ErrorReceived?.Invoke("Socket 事件处理失败：" + eventName);
            }
        }

        void DispatchEvent(string eventName, string payload)
        {
            if (eventName == "pond_snapshot")
                PondSnapshotReceived?.Invoke(JsonUtility.FromJson<PondSnapshotDto>(payload));
            else if (eventName == "pond_user_joined")
                PondUserJoined?.Invoke(JsonUtility.FromJson<PondUserDto>(payload));
            else if (eventName == "pond_user_left")
                PondUserLeft?.Invoke(Unquote(payload));
            else if (eventName == "pond_user_updated")
                PondUserUpdated?.Invoke(JsonUtility.FromJson<PondUserDto>(payload));
            else if (eventName == "session_timer_tick")
                SessionTimerTick?.Invoke(JsonUtility.FromJson<SessionTimerTickDto>(payload));
            else if (eventName == "fish_bite")
                FishBiteReceived?.Invoke(JsonUtility.FromJson<PendingFishCatchDto>(payload));
            else if (eventName == "fish_catch_settled")
                FishCatchSettled?.Invoke(JsonUtility.FromJson<FishCatchSettledDto>(payload));
            else if (eventName == "pond_session_summary")
                PondSessionSummaryReceived?.Invoke(JsonUtility.FromJson<PondSessionSummaryDto>(payload));
            else if (eventName == "inventory_updated")
                InventoryUpdated?.Invoke(ParseInventory(payload));
            else if (eventName == "chat_message")
                ChatMessageReceived?.Invoke(JsonUtility.FromJson<ChatMessageDto>(payload));
            else if (eventName == "codex_unlocked")
                CodexUnlocked?.Invoke(JsonUtility.FromJson<CodexUnlockDto>(payload));
            else if (eventName == "achievement_unlocked")
                AchievementUnlocked?.Invoke(JsonUtility.FromJson<AchievementUnlockDto>(payload));
            else if (eventName == "friend_request")
                FriendRequestReceived?.Invoke(JsonUtility.FromJson<FriendRequestDto>(payload));
            else if (eventName == "dm_message")
                DmMessageReceived?.Invoke(JsonUtility.FromJson<DirectMessageDto>(payload));
            else if (eventName == "post_liked")
                PostLikedReceived?.Invoke(JsonUtility.FromJson<PostLikedDto>(payload));
            else if (eventName == "post_commented")
                PostCommentedReceived?.Invoke(JsonUtility.FromJson<PostCommentedDto>(payload));
            else if (eventName == "post_comment_deleted")
                PostCommentDeletedReceived?.Invoke(JsonUtility.FromJson<PostCommentDeletedDto>(payload));
            else if (eventName == "police_raid")
                PoliceRaidReceived?.Invoke(JsonUtility.FromJson<PoliceRaidDto>(payload));
            else if (eventName == "error")
                ErrorReceived?.Invoke(Unquote(payload));
        }

        void HandleAckPacket(string packet)
        {
            var body = packet.Substring(2);
            var idEnd = 0;
            while (idEnd < body.Length && char.IsDigit(body[idEnd]))
                idEnd++;
            if (idEnd == 0 || !int.TryParse(body.Substring(0, idEnd), out var id))
                return;
            var payload = body.Substring(idEnd).Trim();
            if (payload.StartsWith("[", StringComparison.Ordinal) &&
                payload.EndsWith("]", StringComparison.Ordinal))
            {
                payload = payload.Substring(1, payload.Length - 2).Trim();
            }
            var ack = TakeAck(id);
            if (ack == null)
                return;
            var result = string.IsNullOrEmpty(payload) || payload == "null"
                ? new SocketActionAckDto { ok = true }
                : JsonUtility.FromJson<SocketActionAckDto>(payload);
            Debug.Log(
                "[Latency][Socket] ack_received id=" + id +
                " ok=" + result.ok +
                " atMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var message = BuildAckMessage(result);
            ack(result.ok, message);
        }

        static string BuildAckMessage(SocketActionAckDto result)
        {
            if (!result.ok)
                return result.error;
            if (result.autoReturned && result.gold > 0)
            {
                return "自动回塘 +" + result.gold + " 金币" +
                       (result.playerXp > 0 ? "，玩家XP +" + result.playerXp : "") +
                       (result.pondXp > 0 ? "，塘XP +" + result.pondXp : "");
            }
            return "操作成功。";
        }

        void SendEventWithAck(string name, string payload, Action<bool, string> onCompleted)
        {
            if (!IsConnected)
            {
                Complete(false, "Socket 尚未连接。", onCompleted);
                return;
            }
            var id = Interlocked.Increment(ref _nextAckId);
            lock (_ackLock) _pendingAcks[id] = onCompleted;
            Debug.Log(
                "[Latency][Socket] event_sent id=" + id +
                " event=" + name +
                " atMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            SendEvent(name, payload, id);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(AckTimeoutMs);
                Action<bool, string> callback = null;
                lock (_ackLock)
                {
                    if (_pendingAcks.TryGetValue(id, out callback))
                        _pendingAcks.Remove(id);
                }

                if (callback != null)
                {
                    Debug.LogWarning(
                        "[Latency][Socket] ack_timeout id=" + id +
                        " event=" + name +
                        " atMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    EnqueuePriority(() => callback(false,
                        "服务器未返回 " + name + " 操作结果，请检查服务端日志。"));
                }
            });
        }

        void SendEvent(string name, string payload, int? ackId)
        {
            var packet = "42[" + Quote(name) + ",";
            if (ackId.HasValue) packet = "42" + ackId.Value + "[" + Quote(name) + ",";
            packet += payload + "]";
            _ = SendRaw(packet, _cancel?.Token ?? CancellationToken.None);
        }

        async Task SendRaw(string packet, CancellationToken token)
        {
            var entered = false;
            try
            {
                await _sendLock.WaitAsync(token);
                entered = true;
                if (_socket == null || _socket.State != WebSocketState.Open)
                    return;
                var bytes = Encoding.UTF8.GetBytes(packet);
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Pond] Socket.IO send failed: " + error.Message);
            }
            finally
            {
                if (entered)
                    _sendLock.Release();
            }
        }

        Uri BuildUri()
        {
            var value = _baseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            return new Uri(value + "/socket.io/?EIO=4&transport=websocket");
        }

        void SetState(SocialSocketState state, string message)
        {
            State = state;
            StateChanged?.Invoke(state, message);
        }

        void Enqueue(Action action) => _mainThread.Enqueue(action);

        void EnqueuePriority(Action action) => _priorityMainThread.Enqueue(action);

        Action<bool, string> TakeAck(int id)
        {
            lock (_ackLock)
            {
                if (!_pendingAcks.TryGetValue(id, out var ack))
                    return null;
                _pendingAcks.Remove(id);
                return ack;
            }
        }

        void CompletePending(bool ok, string message)
        {
            Action<bool, string>[] callbacks;
            lock (_ackLock)
            {
                callbacks = new Action<bool, string>[_pendingAcks.Count];
                _pendingAcks.Values.CopyTo(callbacks, 0);
                _pendingAcks.Clear();
            }
            foreach (var callback in callbacks)
                callback?.Invoke(ok, message);
        }

        static void Complete(bool ok, string message, Action<bool, string> callback)
        {
            callback?.Invoke(ok, message);
        }

        static string Quote(string value)
        {
            return JsonUtility.ToJson(new StringValue { value = value ?? "" })
                .Replace("{\"value\":", "")
                .TrimEnd('}');
        }

        static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var text = value.Trim();
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
                return text.Substring(1, text.Length - 2)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            return text;
        }

        static FishInventoryItemDto[] ParseInventory(string payload)
        {
            var wrapper = JsonUtility.FromJson<InventoryWrapper>("{\"items\":" + payload + "}");
            return wrapper?.items ?? new FishInventoryItemDto[0];
        }

        static bool TryReadArrayPacket(string body, out string eventName, out string payload)
        {
            eventName = null;
            payload = null;
            if (!body.StartsWith("[\""))
                return false;
            var end = 2;
            while (end < body.Length)
            {
                if (body[end] == '"' && body[end - 1] != '\\')
                    break;
                end++;
            }
            if (end >= body.Length)
                return false;
            eventName = body.Substring(2, end - 2);
            var comma = body.IndexOf(',', end);
            if (comma < 0 || body[body.Length - 1] != ']')
                return false;
            payload = body.Substring(comma + 1, body.Length - comma - 2);
            return true;
        }

        #pragma warning disable 0649
        [Serializable]
        sealed class SocketAuthPayload { public string token; }
        [Serializable]
        sealed class StringValue { public string value; }
        [Serializable]
        sealed class InventoryWrapper { public FishInventoryItemDto[] items; }
        #pragma warning restore 0649
    }

    #pragma warning disable 0067
    public sealed class UnavailableSocialSocketClient : ISocialSocketClient
    {
        public bool IsConnected => false;
        public SocialSocketState State => SocialSocketState.Failed;
        public event Action<SocialSocketState, string> StateChanged;
        public event Action<PondSnapshotDto> PondSnapshotReceived;
        public event Action<PondUserDto> PondUserJoined;
        public event Action<string> PondUserLeft;
        public event Action<PondUserDto> PondUserUpdated;
        public event Action<SessionTimerTickDto> SessionTimerTick;
        public event Action<PendingFishCatchDto> FishBiteReceived;
        public event Action<FishCatchSettledDto> FishCatchSettled;
        public event Action<PondSessionSummaryDto> PondSessionSummaryReceived;
        public event Action<FishInventoryItemDto[]> InventoryUpdated;
        public event Action<ChatMessageDto> ChatMessageReceived;
        public event Action<CodexUnlockDto> CodexUnlocked;
        public event Action<AchievementUnlockDto> AchievementUnlocked;
        public event Action<FriendRequestDto> FriendRequestReceived;
        public event Action<DirectMessageDto> DmMessageReceived;
        public event Action<PostLikedDto> PostLikedReceived;
        public event Action<PostCommentedDto> PostCommentedReceived;
        public event Action<PostCommentDeletedDto> PostCommentDeletedReceived;
        public event Action<PoliceRaidDto> PoliceRaidReceived;
        public event Action<string> ErrorReceived;

        public void Connect(string accessToken, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void RegisterPlayer(string playerId) { }
        public void JoinPond(JoinPondPayload payload, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void LeaveSpot(string pondId, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void LeavePond(string pondId, string reason, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void TakeSpot(TakeSpotPayload payload, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void StartFishing(StartFishingPayload payload, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void StartGroundbait(GroundbaitStartPayload payload, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void StopFishing(string pondId, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void AcceptCatch(string catchId, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void SendChat(SendChatPayload payload, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void Pump() { }
        public void Disconnect() { }
    }
    #pragma warning restore 0067
}

