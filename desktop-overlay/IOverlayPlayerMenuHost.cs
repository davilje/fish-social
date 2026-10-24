namespace FishSocialOverlay
{
    public interface IOverlayPlayerMenuHost
    {
        void SendPlayerCommand(string command, string playerId);
        void PlayPlayerMenuAudio(string cue);
    }
}
