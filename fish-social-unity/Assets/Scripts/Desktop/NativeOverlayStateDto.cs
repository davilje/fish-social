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
        public string pondId = string.Empty;
        public string ownSpotId = string.Empty;
        public float ownX;
        public float ownY;
        public bool hasOwnPosition;
        public NativeOverlaySpotDto[] spots = new NativeOverlaySpotDto[0];
    }

    [Serializable]
    public sealed class NativeOverlaySpotDto
    {
        public string id;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class NativeOverlayCommandDto
    {
        public string type;
        public int version;
        public string command;
    }
}
