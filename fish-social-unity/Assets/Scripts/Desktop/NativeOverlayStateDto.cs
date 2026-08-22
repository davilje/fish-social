using System;

namespace FishSocial.Desktop
{
    [Serializable]
    public sealed class NativeOverlayStateDto
    {
        public string type = "state";
        public int version = 1;
        public long sequence;
        public string loginState = "SignedOut";
        public string connectionState = "Disconnected";
        public string pondName = string.Empty;
        public string fishingPhase = "idle";
        public string petVisualState = "offline";
        public string ownNickname = string.Empty;
        public string ownPlayerId = string.Empty;
        public string ownUserId = string.Empty;
        public long sessionFishingMs;
        public long hookDeadlineMs;
        public long ownFishingStartedAt;
        public string pondId = string.Empty;
        public string ownSpotId = string.Empty;
        public float ownX;
        public float ownY;
        public bool hasOwnPosition;
        public bool mainWindowRaised;
        public bool hasPendingCatch;
        public string errorMessage = string.Empty;
        public string guideTip = string.Empty;
        public bool lockFeatureNav;
        public string overlayPromptKind = string.Empty;
        public string overlayPromptTitle = string.Empty;
        public string overlayPromptBody = string.Empty;
        public string overlayPromptButton = string.Empty;
        public long overlayPromptDeadlineMs;
        public string[] availableActions = new string[0];
        public NativeOverlaySpotDto[] spots = new NativeOverlaySpotDto[0];
        public NativeOverlayActorDto[] users = new NativeOverlayActorDto[0];
        public NativeOverlayChatDto[] recentChats = new NativeOverlayChatDto[0];
        public NativeOverlayChatDto observation;
    }

    [Serializable]
    public sealed class NativeOverlaySpotDto
    {
        public string id;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class NativeOverlayActorDto
    {
        public string playerId;
        public string userId;
        public string nickname;
        public string spotId;
        public float x;
        public float y;
        public bool hasPosition;
        public string petVisualState;
        public string fishingPhase;
        public long sessionFishingMs;
        public long hookDeadlineMs;
        public long fishingStartedAt;
        public bool isBot;
    }

    [Serializable]
    public sealed class NativeOverlayCommandDto
    {
        public string type;
        public int version;
        public string command;
        public string spotId;
        public string playerId;
        public string text;
        public long commandId;
        public long sentAtMs;
    }

    [Serializable]
    public sealed class NativeOverlayChatDto
    {
        public string messageId;
        public string userId;
        public string playerId;
        public string nickname;
        public string text;
        public long sentAtMs;
    }
}
