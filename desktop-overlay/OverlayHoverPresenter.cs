using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FishSocialOverlay
{
    public interface IOverlayHoverHost
    {
        void ShowHoverCard(string actorKey, string text, FrameworkElement anchor);
        void UpdateHoverCard(string actorKey, string text);
        void HideHoverCard(string actorKey);
        void HideAllHoverCards();
        void RemoveActor(string actorKey);
    }

    /// <summary>
    /// One hover card for the whole pond. Re-measuring per actor caused a second
    /// card to appear to the right of the pet (DesiredSize=0 → left=centerX).
    /// </summary>
    public sealed class OverlayHoverPresenter : IOverlayHoverHost
    {
        const double CardWidth = 88;
        const double CardHeight = 36;
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
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _card = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                Background = new SolidColorBrush(Color.FromArgb(230, 15, 24, 32)),
                CornerRadius = new CornerRadius(4),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Child = _text,
            };
            Panel.SetZIndex(_card, 50);
            _layer.Children.Add(_card);
        }

        public void ShowHoverCard(string actorKey, string text, FrameworkElement anchor)
        {
            if (string.IsNullOrEmpty(actorKey) || string.IsNullOrEmpty(text) || anchor == null)
                return;

            _visibleActorKey = actorKey;
            _text.Text = text;
            PositionAbove(anchor);
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

        void PositionAbove(FrameworkElement anchor)
        {
            Point topCenter;
            try
            {
                topCenter = anchor.TranslatePoint(
                    new Point(anchor.ActualWidth * 0.5, 0),
                    _layer);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            var left = topCenter.X - CardWidth * 0.5;
            var top = topCenter.Y - CardHeight - 6;
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
