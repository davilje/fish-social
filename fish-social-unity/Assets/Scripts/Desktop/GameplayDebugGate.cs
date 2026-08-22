using System;

namespace FishSocial.Desktop
{
    /// <summary>
    /// STEAM-DESKTOP-12: client-side Debug menu gate.
    /// Editor / Development Build always on; Release only with FISH_SOCIAL_GAMEPLAY_DEBUG=1.
    /// </summary>
    public static class GameplayDebugGate
    {
        public static bool IsClientEnabled()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return string.Equals(
                Environment.GetEnvironmentVariable("FISH_SOCIAL_GAMEPLAY_DEBUG"),
                "1",
                StringComparison.OrdinalIgnoreCase);
#endif
        }
    }
}
