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
        event Action<PondUserDto> PondUserUpdated;
        event Action<PendingFishCatchDto> FishBiteReceived;
        event Action<FishInventoryItemDto[]> InventoryUpdated;
        event Action<string> ErrorReceived;

        void Connect(string accessToken, Action<bool, string> onCompleted);
        void RegisterPlayer(string playerId);
        void JoinPond(JoinPondPayload payload, Action<bool, string> onCompleted);
        void TakeSpot(TakeSpotPayload payload, Action<bool, string> onCompleted);
        void StartFishing(StartFishingPayload payload, Action<bool, string> onCompleted);
        void StopFishing(string pondId, Action<bool, string> onCompleted);
        void AcceptCatch(string catchId, Action<bool, string> onCompleted);
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
        readonly string _baseUrl;
        readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        readonly Dictionary<int, Action<bool, string>> _pendingAcks =
            new Dictionary<int, Action<bool, string>>();
        readonly object _ackLock = new object();
        ClientWebSocket _socket;
        CancellationTokenSource _cancel;
        string _accessToken;
        int _nextAckId;
        bool _reconnectRequested;

        public bool IsConnected => State == SocialSocketState.Connected;
        public SocialSocketState State { get; private set; } = SocialSocketState.Disconnected;
        public event Action<SocialSocketState, string> StateChanged;
        public event Action<PondSnapshotDto> PondSnapshotReceived;
        public event Action<PondUserDto> PondUserUpdated;
        public event Action<PendingFishCatchDto> FishBiteReceived;
        public event Action<FishInventoryItemDto[]> InventoryUpdated;
        public event Action<string> ErrorReceived;

        public SocketIoSocialSocketClient(string baseUrl)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
        }

        public void Connect(string accessToken, Action<bool, string> onCompleted)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
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

        public void TakeSpot(TakeSpotPayload payload, Action<bool, string> onCompleted)
        {
            SendEventWithAck("take_spot", JsonUtility.ToJson(payload), onCompleted);
        }

        public void StartFishing(StartFishingPayload payload, Action<bool, string> onCompleted)
        {
            SendEventWithAck("start_fishing", JsonUtility.ToJson(payload), onCompleted);
        }

        public void StopFishing(string pondId, Action<bool, string> onCompleted)
        {
            SendEventWithAck("stop_fishing", Quote(pondId), onCompleted);
        }

        public void AcceptCatch(string catchId, Action<bool, string> onCompleted)
        {
            SendEventWithAck("accept_catch", Quote(catchId), onCompleted);
        }

        public void Disconnect()
        {
            _reconnectRequested = false;
            _cancel?.Cancel();
            if (_socket != null)
            {
                try { _socket.Abort(); } catch { }
                _socket.Dispose();
                _socket = null;
            }
            SetState(SocialSocketState.Disconnected, "实时服务已断开。");
        }

        public void Pump()
        {
            while (_mainThread.TryDequeue(out var action))
                action?.Invoke();
        }

        async Task ConnectLoop(Action<bool, string> onCompleted, CancellationToken token)
        {
            try
            {
                _socket?.Dispose();
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(BuildUri(), token);
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

                var packet = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (packet == "2")
                {
                    await SendRaw("3", token);
                    continue;
                }
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
                if (eventName == "pond_snapshot")
                    PondSnapshotReceived?.Invoke(JsonUtility.FromJson<PondSnapshotDto>(payload));
                else if (eventName == "pond_user_updated")
                    PondUserUpdated?.Invoke(JsonUtility.FromJson<PondUserDto>(payload));
                else if (eventName == "fish_bite")
                    FishBiteReceived?.Invoke(JsonUtility.FromJson<PendingFishCatchDto>(payload));
                else if (eventName == "inventory_updated")
                    InventoryUpdated?.Invoke(ParseInventory(payload));
                else if (eventName == "error")
                    ErrorReceived?.Invoke(Unquote(payload));
            }
            catch (Exception error)
            {
                ErrorReceived?.Invoke("Socket 数据解析失败：" + error.Message);
            }
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
            ack(result.ok, result.ok ? "操作成功。" : result.error);
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
            SendEvent(name, payload, id);
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
            if (_socket == null || _socket.State != WebSocketState.Open)
                return;
            var bytes = Encoding.UTF8.GetBytes(packet);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
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

        [Serializable]
        sealed class SocketAuthPayload { public string token; }
        [Serializable]
        sealed class StringValue { public string value; }
        [Serializable]
        sealed class InventoryWrapper { public FishInventoryItemDto[] items; }
    }

    #pragma warning disable 0067
    public sealed class UnavailableSocialSocketClient : ISocialSocketClient
    {
        public bool IsConnected => false;
        public SocialSocketState State => SocialSocketState.Failed;
        public event Action<SocialSocketState, string> StateChanged;
        public event Action<PondSnapshotDto> PondSnapshotReceived;
        public event Action<PondUserDto> PondUserUpdated;
        public event Action<PendingFishCatchDto> FishBiteReceived;
        public event Action<FishInventoryItemDto[]> InventoryUpdated;
        public event Action<string> ErrorReceived;

        public void Connect(string accessToken, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void RegisterPlayer(string playerId) { }
        public void JoinPond(JoinPondPayload payload, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void TakeSpot(TakeSpotPayload payload, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void StartFishing(StartFishingPayload payload, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void StopFishing(string pondId, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void AcceptCatch(string catchId, Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Unity Socket.IO 客户端尚未接入。");
        public void Pump() { }
        public void Disconnect() { }
    }
    #pragma warning restore 0067
}

