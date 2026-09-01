using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FishSocialOverlay
{
    public interface IOverlayHoverHost
    {
        void ShowHoverCard(string actorKey, string text, FrameworkElement cluster, FrameworkElement hintSlot);
        void UpdateHoverCard(string actorKey, string text);
        void HideHoverCard(string actorKey);
        void HideAllHoverCards();
        void RemoveActor(string actorKey);
    }

    /// <summary>
    /// One hover card for the whole pond. Anchors to OverlayPondActor actor-hint:
    /// centered in the slot, expanding up from the slot bottom (same as guide hints).
    /// </summary>
    public sealed class OverlayHoverPresenter : IOverlayHoverHost
    {
        const double CardMinWidth = 88;
        const double CardMinHeight = 28;
        readonly Canvas _layer;
        readonly Border _card;
        readonly TextBlock _text;
        string _visibleActorKey;
        FrameworkElement _cluster;
        FrameworkElement _hintSlot;

        public OverlayHoverPresenter(Canvas layer)
        {
            _layer = layer;
            _layer.Children.Clear();
            _text = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _card = new Border
            {
                MinWidth = CardMinWidth,
                MinHeight = CardMinHeight,
                Padding = new Thickness(6, 3, 6, 3),
                Background = new SolidColorBrush(Color.FromArgb(230, 15, 24, 32)),
                CornerRadius = new CornerRadius(4),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Child = _text,
            };
            Panel.SetZIndex(_card, 50);
            _layer.Children.Add(_card);
        }

        public void ShowHoverCard(string actorKey, string text, FrameworkElement cluster, FrameworkElement hintSlot)
        {
            if (string.IsNullOrEmpty(actorKey) || string.IsNullOrEmpty(text) || cluster == null)
                return;

            _visibleActorKey = actorKey;
            _cluster = cluster;
            _hintSlot = hintSlot;
            _text.Text = text;
            _card.Visibility = Visibility.Visible;
            PositionAtHint();
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
            PositionAtHint();
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
            _cluster = null;
            _hintSlot = null;
            _card.Visibility = Visibility.Collapsed;
        }

        public void RemoveActor(string actorKey)
        {
            HideHoverCard(actorKey);
        }

        void PositionAtHint()
        {
            if (_cluster == null)
                return;

            var slot = _hintSlot ?? _cluster;
            Point slotOrigin;
            try
            {
                slotOrigin = slot.TranslatePoint(new Point(0, 0), _layer);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            var slotW = slot.ActualWidth > 1 ? slot.ActualWidth : slot.Width;
            var slotH = slot.ActualHeight > 1 ? slot.ActualHeight : slot.Height;
            if (slotW < 1)
                slotW = CardMinWidth;
            if (slotH < 1)
                slotH = CardMinHeight;

            _card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var cardWidth = Math.Max(CardMinWidth, _card.DesiredSize.Width);
            var cardHeight = Math.Max(CardMinHeight, _card.DesiredSize.Height);
            var left = slotOrigin.X + (slotW - cardWidth) * 0.5;
            var top = slotOrigin.Y + slotH - cardHeight;
            var maxLeft = Math.Max(4, _layer.ActualWidth - cardWidth - 4);
            var maxTop = Math.Max(4, _layer.ActualHeight - cardHeight - 4);
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
