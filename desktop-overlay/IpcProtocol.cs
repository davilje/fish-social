using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System;

namespace FishSocialOverlay
{
    internal static class LatencyTrace
    {
        static readonly object Sync = new object();
        static readonly string Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "FishSocialOverlay-latency.log");

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(
                        Path,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                        " " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Diagnostics must never affect Overlay interaction.
            }
        }
    }

    [DataContract]
    public sealed class OverlaySpotDto
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "x")]
        public float X { get; set; }

        [DataMember(Name = "y")]
        public float Y { get; set; }
    }

    [DataContract]
    public sealed class OverlayUserDto
    {
        [DataMember(Name = "playerId")]
        public string PlayerId { get; set; }

        [DataMember(Name = "userId")]
        public string UserId { get; set; }

        [DataMember(Name = "nickname")]
        public string Nickname { get; set; }

        [DataMember(Name = "spotId")]
        public string SpotId { get; set; }

        [DataMember(Name = "x")]
        public float X { get; set; }

        [DataMember(Name = "y")]
        public float Y { get; set; }

        [DataMember(Name = "hasPosition")]
        public bool HasPosition { get; set; }

        [DataMember(Name = "petVisualState")]
        public string PetVisualState { get; set; }

        [DataMember(Name = "fishingPhase")]
        public string FishingPhase { get; set; }

        [DataMember(Name = "sessionFishingMs")]
        public long SessionFishingMs { get; set; }

        [DataMember(Name = "hookDeadlineMs")]
        public long HookDeadlineMs { get; set; }

        [DataMember(Name = "fishingStartedAt")]
        public long FishingStartedAt { get; set; }

        [DataMember(Name = "isBot")]
        public bool IsBot { get; set; }
    }

    [DataContract]
    public sealed class OverlayChatDto
    {
        [DataMember(Name = "messageId")]
        public string MessageId { get; set; }

        [DataMember(Name = "userId")]
        public string UserId { get; set; }

        [DataMember(Name = "playerId")]
        public string PlayerId { get; set; }

        [DataMember(Name = "nickname")]
        public string Nickname { get; set; }

        [DataMember(Name = "text")]
        public string Text { get; set; }

        [DataMember(Name = "sentAtMs")]
        public long SentAtMs { get; set; }
    }

    [DataContract]
    public sealed class IpcMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "version")]
        public int Version { get; set; } = 1;

        [DataMember(Name = "sequence")]
        public long Sequence { get; set; }

        [DataMember(Name = "loginState")]
        public string LoginState { get; set; }

        [DataMember(Name = "connectionState")]
        public string ConnectionState { get; set; }

        [DataMember(Name = "pondName")]
        public string PondName { get; set; }

        [DataMember(Name = "fishingPhase")]
        public string FishingPhase { get; set; }

        [DataMember(Name = "petVisualState")]
        public string PetVisualState { get; set; }

        [DataMember(Name = "ownNickname")]
        public string OwnNickname { get; set; }

        [DataMember(Name = "ownPlayerId")]
        public string OwnPlayerId { get; set; }

        [DataMember(Name = "ownUserId")]
        public string OwnUserId { get; set; }

        [DataMember(Name = "sessionFishingMs")]
        public long SessionFishingMs { get; set; }

        [DataMember(Name = "hookDeadlineMs")]
        public long HookDeadlineMs { get; set; }

        [DataMember(Name = "ownFishingStartedAt")]
        public long OwnFishingStartedAt { get; set; }

        [DataMember(Name = "pondId")]
        public string PondId { get; set; }

        [DataMember(Name = "ownSpotId")]
        public string OwnSpotId { get; set; }

        [DataMember(Name = "ownX")]
        public float OwnX { get; set; }

        [DataMember(Name = "ownY")]
        public float OwnY { get; set; }

        [DataMember(Name = "hasOwnPosition")]
        public bool HasOwnPosition { get; set; }

        [DataMember(Name = "mainWindowRaised")]
        public bool MainWindowRaised { get; set; }

        [DataMember(Name = "hasPendingCatch")]
        public bool HasPendingCatch { get; set; }

        [DataMember(Name = "errorMessage")]
        public string ErrorMessage { get; set; }

        [DataMember(Name = "guideTip")]
        public string GuideTip { get; set; }

        [DataMember(Name = "lockFeatureNav")]
        public bool LockFeatureNav { get; set; }

        [DataMember(Name = "overlayPromptKind")]
        public string OverlayPromptKind { get; set; }

        [DataMember(Name = "overlayPromptTitle")]
        public string OverlayPromptTitle { get; set; }

        [DataMember(Name = "overlayPromptBody")]
        public string OverlayPromptBody { get; set; }

        [DataMember(Name = "overlayPromptButton")]
        public string OverlayPromptButton { get; set; }

        [DataMember(Name = "overlayPromptDeadlineMs")]
        public long OverlayPromptDeadlineMs { get; set; }

        [DataMember(Name = "availableActions")]
        public string[] AvailableActions { get; set; }

        [DataMember(Name = "spots")]
        public OverlaySpotDto[] Spots { get; set; }

        [DataMember(Name = "users")]
        public OverlayUserDto[] Users { get; set; }

        [DataMember(Name = "recentChats")]
        public OverlayChatDto[] RecentChats { get; set; }

        [DataMember(Name = "observation")]
        public OverlayChatDto Observation { get; set; }

        [DataMember(Name = "command")]
        public string Command { get; set; }

        [DataMember(Name = "text")]
        public string Text { get; set; }

        [DataMember(Name = "spotId")]
        public string SpotId { get; set; }

        [DataMember(Name = "playerId")]
        public string PlayerId { get; set; }

        [DataMember(Name = "commandId")]
        public long CommandId { get; set; }

        [DataMember(Name = "sentAtMs")]
        public long SentAtMs { get; set; }

        public static IpcMessage Parse(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(IpcMessage));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return serializer.ReadObject(stream) as IpcMessage;
        }

        public string ToJson()
        {
            var serializer = new DataContractJsonSerializer(typeof(IpcMessage));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, this);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
