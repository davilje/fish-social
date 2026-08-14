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
    }

    [Serializable]
    public sealed class NativeOverlayCommandDto
    {
        public string type;
        public int version;
        public string command;
    }
}
