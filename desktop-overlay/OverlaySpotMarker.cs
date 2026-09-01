using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace FishSocialOverlay
{
    /// <summary>
    /// Fishing seat marker. Hit target is the seat Image / fallback dot only
    /// (actor-seat image bounds), not the larger spot host.
    /// </summary>
    public sealed class OverlaySpotMarker : Grid
    {
        public const double EmptyOpacity = 0.40;

        readonly Image _seatImage;
        readonly Ellipse _fallbackDot;

        public string SpotId { get; }

        public OverlaySpotMarker(string spotId)
        {
            SpotId = spotId ?? string.Empty;
            Tag = SpotId;
            Background = null;
            ClipToBounds = false;
            // Host passes through; only seat art receives clicks.
            IsHitTestVisible = true;

            _seatImage = new Image
            {
                Stretch = Stretch.Fill,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = true,
            };
            _fallbackDot = new Ellipse
            {
                Fill = new SolidColorBrush(Color.FromArgb(220, 243, 201, 105)),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 230)),
                StrokeThickness = 2,
                Visibility = Visibility.Visible,
                IsHitTestVisible = true,
            };

            Children.Add(_seatImage);
            Children.Add(_fallbackDot);
            _seatImage.MouseLeftButtonDown += OnMouseLeftButtonDown;
            _fallbackDot.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        public event EventHandler<string> SpotSelected;

        public void SetSeatArt(ImageSource source, bool usedFallback)
        {
            if (source != null && !usedFallback)
            {
                _seatImage.Source = source;
                _seatImage.Visibility = Visibility.Visible;
                _fallbackDot.Visibility = Visibility.Collapsed;
                return;
            }

            _seatImage.Source = null;
            _seatImage.Visibility = Visibility.Collapsed;
            _fallbackDot.Visibility = Visibility.Visible;
        }

        public void ApplyState(bool ownHasSpot, bool spotOccupied)
        {
            if (ownHasSpot && !spotOccupied)
            {
                Visibility = Visibility.Collapsed;
                IsHitTestVisible = false;
                Opacity = 1.0;
                return;
            }

            Visibility = Visibility.Visible;
            IsHitTestVisible = !spotOccupied;
            Opacity = spotOccupied ? 1.0 : EmptyOpacity;
        }

        /// <summary>
        /// True when the hit came from the seat PNG or fallback dot (not empty host chrome).
        /// </summary>
        public bool IsSeatArtSource(DependencyObject source)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, _seatImage) || ReferenceEquals(source, _fallbackDot))
                    return true;
                if (ReferenceEquals(source, this))
                    return false;
                if (source is Visual || source is Visual3D)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(SpotId) || !IsHitTestVisible)
                return;
            SpotSelected?.Invoke(this, SpotId);
            e.Handled = true;
        }
    }
}
