using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    /// <summary>
    /// Speech bubbles above pond pets. Pop 0.5→1.1→1.0 in 0.3s, hold 5s, fade out.
    /// </summary>
    internal sealed class OverlayChatBubblePresenter
    {
        const double BubbleMaxWidth = 132;
        const double PopSeconds = 0.3;
        const double HoldSeconds = 5.0;
        const double FadeOutSeconds = 0.3;

        readonly Canvas _layer;
        readonly Func<string, FrameworkElement> _resolveActor;
        readonly HashSet<string> _seenMessageIds = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, Border> _activeByPlayer =
            new Dictionary<string, Border>(StringComparer.Ordinal);

        public OverlayChatBubblePresenter(Canvas layer, Func<string, FrameworkElement> resolveActor)
        {
            _layer = layer;
            _resolveActor = resolveActor;
        }

        public void ResetPond()
        {
            _seenMessageIds.Clear();
            HideAll();
        }

        public void ProcessMessages(OverlayChatDto[] chats, bool replayHistory)
        {
            if (chats == null || chats.Length == 0)
                return;

            if (replayHistory)
            {
                for (var i = 0; i < chats.Length; i++)
                {
                    var chat = chats[i];
                    if (chat != null && !string.IsNullOrEmpty(chat.MessageId))
                        _seenMessageIds.Add(chat.MessageId);
                }

                return;
            }

            for (var i = 0; i < chats.Length; i++)
            {
                var chat = chats[i];
                if (chat == null || string.IsNullOrEmpty(chat.MessageId))
                    continue;
                if (!_seenMessageIds.Add(chat.MessageId))
                    continue;
                ShowBubble(chat);
            }
        }

        public void HideAll()
        {
            _activeByPlayer.Clear();
            _layer.Children.Clear();
        }

        void ShowBubble(OverlayChatDto chat)
        {
            var actorKey = !string.IsNullOrEmpty(chat.UserId)
                ? chat.UserId
                : chat.PlayerId ?? string.Empty;
            if (string.IsNullOrEmpty(actorKey))
                return;

            var actor = _resolveActor(actorKey);
            if (actor == null)
                return;

            if (_activeByPlayer.TryGetValue(actorKey, out var previous))
            {
                _layer.Children.Remove(previous);
                _activeByPlayer.Remove(actorKey);
            }

            var text = string.IsNullOrWhiteSpace(chat.Text) ? "…" : chat.Text.Trim();
            if (text.Length > 80)
                text = text.Substring(0, 77) + "…";

            var label = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = BubbleMaxWidth,
            };

            var bubble = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 15, 24, 32)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 5, 8, 5),
                Child = label,
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform = new ScaleTransform(0.5, 0.5),
                IsHitTestVisible = false,
            };

            _layer.Children.Add(bubble);
            _activeByPlayer[actorKey] = bubble;
            PositionBubble(bubble, actor);
            Panel.SetZIndex(bubble, 10);

            RunPopIn(bubble);
            ScheduleFadeOut(bubble, actorKey);
        }

        void PositionBubble(Border bubble, FrameworkElement actor)
        {
            bubble.Measure(new Size(BubbleMaxWidth + 32, double.PositiveInfinity));
            var size = bubble.DesiredSize;
            if (size.Width < 40)
                size = new Size(40, size.Height);

            Point anchor;
            try
            {
                anchor = actor.TranslatePoint(
                    new Point(actor.ActualWidth * 0.5, 0),
                    _layer);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            var left = anchor.X - size.Width * 0.5;
            var top = anchor.Y - size.Height - 8;
            var maxLeft = Math.Max(4, _layer.ActualWidth - size.Width - 4);
            var maxTop = Math.Max(4, _layer.ActualHeight - size.Height - 4);
            Canvas.SetLeft(bubble, Clamp(left, 4, maxLeft));
            Canvas.SetTop(bubble, Clamp(top, 4, maxTop));
        }

        static void RunPopIn(Border bubble)
        {
            var transform = bubble.RenderTransform as ScaleTransform;
            if (transform == null)
                return;

            var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };

            var scaleX = CreateScaleAnimation(bubble, true);
            var scaleY = CreateScaleAnimation(bubble, false);
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(PopSeconds))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(fade, bubble);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));

            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);
            storyboard.Children.Add(fade);
            storyboard.Completed += (_, __) =>
            {
                bubble.Opacity = 1;
                transform.ScaleX = 1;
                transform.ScaleY = 1;
            };
            storyboard.Begin();
        }

        static DoubleAnimationUsingKeyFrames CreateScaleAnimation(Border bubble, bool scaleX)
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.1,
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(PopSeconds * 0.5))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.0,
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(PopSeconds))));

            var property = scaleX
                ? "(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                : "(UIElement.RenderTransform).(ScaleTransform.ScaleY)";
            Storyboard.SetTarget(animation, bubble);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            return animation;
        }

        void ScheduleFadeOut(Border bubble, string playerId)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(HoldSeconds),
            };
            timer.Tick += (_, __) =>
            {
                timer.Stop();
                if (!_layer.Children.Contains(bubble))
                    return;

                var fade = new DoubleAnimation(bubble.Opacity, 0, TimeSpan.FromSeconds(FadeOutSeconds));
                fade.Completed += (_, __) =>
                {
                    _layer.Children.Remove(bubble);
                    if (_activeByPlayer.TryGetValue(playerId, out var active) &&
                        ReferenceEquals(active, bubble))
                        _activeByPlayer.Remove(playerId);
                };
                bubble.BeginAnimation(UIElement.OpacityProperty, fade);
            };
            timer.Start();
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
