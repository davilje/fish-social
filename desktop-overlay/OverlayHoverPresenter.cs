using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FishSocialOverlay
{
    public interface IOverlayHoverHost
    {
        void ShowHoverCard(string actorKey, string text, FrameworkElement cluster, FrameworkElement petBody);
        void UpdateHoverCard(string actorKey, string text);
        void HideHoverCard(string actorKey);
        void HideAllHoverCards();
        void RemoveActor(string actorKey);
    }

    /// <summary>
    /// One hover card for the whole pond. Re-measuring per actor caused a second
    /// card to appear to the right of the pet (DesiredSize=0 → left=centerX).
    /// The card is centered on the 64×64 pet body, not the nameplate cluster.
    /// </summary>
    public sealed class OverlayHoverPresenter : IOverlayHoverHost
    {
        const double CardWidth = 80;
        const double CardHeight = 28;
        readonly Canvas _layer;
        readonly Border _card;
        readonly TextBlock _text;
        string _visibleActorKey;

        public OverlayHoverPresenter(Canvas layer)
        {
            _layer = layer;
            _layer.Children.Clear();
            _text = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _card = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                Padding = new Thickness(4, 2, 4, 2),
                Background = new SolidColorBrush(Color.FromArgb(230, 15, 24, 32)),
                CornerRadius = new CornerRadius(4),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Child = _text,
            };
            Panel.SetZIndex(_card, 50);
            _layer.Children.Add(_card);
        }

        public void ShowHoverCard(string actorKey, string text, FrameworkElement cluster, FrameworkElement petBody)
        {
            if (string.IsNullOrEmpty(actorKey) || string.IsNullOrEmpty(text) || cluster == null)
                return;

            _visibleActorKey = actorKey;
            _text.Text = text;
            PositionAbove(cluster, petBody);
            _card.Visibility = Visibility.Visible;
        }

        public void UpdateHoverCard(string actorKey, string text)
        {
            if (_visibleActorKey != actorKey || _card.Visibility != Visibility.Visible)
                return;
            if (string.IsNullOrEmpty(text))
            {
                HideHoverCard(actorKey);
                return;
            }

            _text.Text = text;
        }

        public void HideHoverCard(string actorKey)
        {
            if (_visibleActorKey != actorKey)
                return;
            HideAllHoverCards();
        }

        public void HideAllHoverCards()
        {
            _visibleActorKey = null;
            _card.Visibility = Visibility.Collapsed;
        }

        public void RemoveActor(string actorKey)
        {
            HideHoverCard(actorKey);
        }

        void PositionAbove(FrameworkElement cluster, FrameworkElement petBody)
        {
            var pet = petBody ?? cluster;
            Point petTopCenter;
            Point clusterTop;
            try
            {
                petTopCenter = pet.TranslatePoint(
                    new Point(pet.ActualWidth * 0.5, 0),
                    _layer);
                clusterTop = cluster.TranslatePoint(new Point(0, 0), _layer);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            var left = petTopCenter.X - CardWidth * 0.5;
            var top = clusterTop.Y - CardHeight - 4;
            var maxLeft = Math.Max(4, _layer.ActualWidth - CardWidth - 4);
            var maxTop = Math.Max(4, _layer.ActualHeight - CardHeight - 4);
            Canvas.SetLeft(_card, Clamp(left, 4, maxLeft));
            Canvas.SetTop(_card, Clamp(top, 4, maxTop));
        }

        static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
