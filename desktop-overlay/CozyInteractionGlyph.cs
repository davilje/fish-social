using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FishSocialOverlay
{
    /// <summary>Native vector counterparts of the approved preview's outline icons.</summary>
    public sealed class CozyInteractionGlyph : Control
    {
        public static readonly DependencyProperty KindProperty = DependencyProperty.RegisterAttached(
            "Kind", typeof(string), typeof(CozyInteractionGlyph), new FrameworkPropertyMetadata("bait", FrameworkPropertyMetadataOptions.AffectsRender));
        public static string GetKind(DependencyObject o) => (string)o.GetValue(KindProperty);
        public static void SetKind(DependencyObject o, string value) => o.SetValue(KindProperty, value);
        public string Kind { get => GetKind(this); set => SetKind(this, value); }
        static readonly Dictionary<string, Geometry> Shapes = new Dictionary<string, Geometry>
        {
            ["left"] = Geometry.Parse("M14,6 L8,12 14,18"),
            ["right"] = Geometry.Parse("M10,6 L16,12 10,18"),
            ["leave"] = Geometry.Parse("M10,4 H4 V20 H10 M9,12 H21 M17,8 L21,12 17,16"),
            ["bait"] = Geometry.Parse("M8,3 H16 L15,7 Q20,10 19,15 Q18,18 12,18 Q5,18 5,14 Q5,10 9,7 M8,7 H16 M9,11 h0.1 M12,13 h0.1 M15,11 h0.1 M4,21 Q12,24 20,21 M5,18 h0.1 M19,18 h0.1")
        };
        internal static Brush Ink = Brush("#78866A");
        internal static Brush Paper = Brush("#FFFAF0");
        internal static Brush Stroke = Brush("#DCCCAF");
        internal static Brush Clay = Brush("#B87453");
        internal static Brush Brush(string color)
        { var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color); brush.Freeze(); return brush; }
        internal static void Draw(DrawingContext dc, string kind, Brush foreground)
        {
            if (!Shapes.TryGetValue(kind ?? "", out var geometry)) return;
            var pen = new Pen(foreground, 1.65) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(null, pen, geometry);
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
            Draw(dc, Kind, Foreground ?? Ink); dc.Pop();
        }
        internal static ImageSource GroundbaitMarker(int count, bool working, bool active)
        {
            var group = new DrawingGroup();
            using (var dc = group.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0,0,44,44));
                var color = working ? Clay : active ? Ink : Brushes.Gray;
                dc.DrawEllipse(Paper, new Pen(Stroke,1), new Point(22,22),19,19);
                dc.PushTransform(new TranslateTransform(10,9)); Draw(dc,"bait",color); dc.Pop();
                dc.DrawEllipse(color,null,new Point(35,9),9,9);
                var text = new FormattedText(Math.Max(0,count).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,Brushes.White,1);
                dc.DrawText(text,new Point(35-text.Width/2,9-text.Height/2));
                // The gap differentiates an in-progress pour from a completed active stack.
                if (working) dc.DrawGeometry(null, new Pen(Clay,2), Geometry.Parse("M7,14 A17,17 0 1 0 33,7"));
            }
            group.Freeze(); return new DrawingImage(group);
        }
    }

    static class OverlayInteractionControls
    {
        public static void Apply(Button button, string role)
        {
            if (button == null) return;
            button.Style = (Style)Application.Current.FindResource("CozyInteractionButton");
            CozyInteractionGlyph.SetKind(button, role);
            button.Foreground = CozyInteractionGlyph.Ink;
            button.Padding = new Thickness(0);
            button.MinHeight = 44;
            if (role == "left" || role == "right")
            {
                button.Content = "";
                button.ToolTip = OverlayLocalization.Text(role == "left" ? "ui.fishing.overlay.fallback.button.pan_left" : "ui.fishing.overlay.fallback.button.pan_right", role == "left" ? "向左" : "向右");
            }
        }
        public static string GroundbaitDetails(IpcMessage message)
        {
            var max = message.GroundbaitMaxStack > 0 ? message.GroundbaitMaxStack : 50;
            var remaining = Math.Max(0, message.GroundbaitExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return OverlayLocalization.Format("ui.fishing.overlay.groundbait.details", Math.Max(0,message.GroundbaitStack), max,
                TimeSpan.FromMilliseconds(remaining).ToString(@"mm\:ss", CultureInfo.InvariantCulture),
                Math.Max(0,message.GroundbaitBitesLeft), message.GroundbaitBiteBonus, message.GroundbaitSizeBonus);
        }
    }
}
