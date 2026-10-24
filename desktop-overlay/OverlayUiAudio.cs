using System.Windows;

namespace FishSocialOverlay
{
    internal static class OverlayUiAudio
    {
        public static readonly DependencyProperty CueProperty = DependencyProperty.RegisterAttached(
            "Cue", typeof(string), typeof(OverlayUiAudio), new PropertyMetadata(null));
        public static void SetCue(DependencyObject target, string value) => target.SetValue(CueProperty, value);
        public static string GetCue(DependencyObject target) => target.GetValue(CueProperty) as string;
    }
}
