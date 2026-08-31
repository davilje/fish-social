using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace FishSocialOverlay
{
    /// <summary>
    /// WM_NCHITTEST helper: HTCLIENT on pets, spots, HUD, and visible pond pixels;
    /// HTTRANSPARENT on composed alpha=0 (including 13A edge fade to clear).
    /// </summary>
    static class OverlayClickThrough
    {
        // Ignore 1/255 "glass" fills; keep visibly faded pond art hittable for DragMove.
        const double AlphaThreshold = 8;

        static BitmapSource _cachedSource;
        static byte[] _cachedBgra;
        static int _cachedWidth;
        static int _cachedHeight;
        static int _cachedStride;

        public static bool HitsPondArt(MainWindow window, Point windowLocal)
        {
            return IsVisiblePondArt(window, windowLocal);
        }

        public static bool IsClientHit(MainWindow window, Point windowLocal)
        {
            if (window == null)
                return false;

            try
            {
                if (window.HitsSocialPetArt(windowLocal))
                    return true;

                var hit = VisualTreeHelper.HitTest(window, windowLocal);
                if (hit != null && IsInteractiveVisual(hit.VisualHit as DependencyObject, window))
                    return true;

                return IsVisiblePondArt(window, windowLocal);
            }
            catch
            {
                return false;
            }
        }

        static bool IsInteractiveVisual(DependencyObject source, MainWindow window)
        {
            var hitLeaf = source;
            while (source != null)
            {
                if (source is OverlayPetActor petActor &&
                    petActor.IsHitTestVisible &&
                    petActor.Visibility == Visibility.Visible)
                    return true;
                if (source is OverlaySpotMarker spotMarker &&
                    spotMarker.IsHitTestVisible &&
                    spotMarker.Visibility == Visibility.Visible &&
                    spotMarker.IsSeatArtSource(hitLeaf))
                    return true;
                if (source is Button || source is TextBox || source is ScrollViewer || source is ScrollBar)
                    return true;
                if (ReferenceEquals(source, window.ChatDockChrome) &&
                    window.ChatDockChrome.Visibility == Visibility.Visible)
                    return true;
                if (ReferenceEquals(source, window.OverlayPromptChrome) &&
                    window.OverlayPromptChrome.Visibility == Visibility.Visible)
                    return true;
                if (ReferenceEquals(source, window.GameplayDebugModal) &&
                    window.GameplayDebugModal.Visibility == Visibility.Visible)
                    return true;
                if (ReferenceEquals(source, window.MenuPanel) &&
                    window.MenuPanel.Visibility == Visibility.Visible)
                    return true;
                if (ReferenceEquals(source, window.StatusCapsule) &&
                    window.StatusCapsule.IsHitTestVisible &&
                    window.StatusCapsule.Visibility == Visibility.Visible)
                    return true;
                if (source is Ellipse && IsUnder(source, window.SpotLayer))
                    return true;
                if (ReferenceEquals(source, window.OwnCat) &&
                    window.OwnCat.Visibility == Visibility.Visible)
                    return true;
                if (ReferenceEquals(source, window.OwnCatImage) &&
                    window.OwnCatImage.Visibility == Visibility.Visible)
                    return true;

                if (source is Visual || source is Visual3D)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        static bool IsUnder(DependencyObject source, DependencyObject root)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, root))
                    return true;
                if (source is Visual || source is Visual3D)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        static bool IsVisiblePondArt(MainWindow window, Point windowLocal)
        {
            var host = window.SceneFadeHost;
            if (host == null || host.ActualWidth < 1 || host.ActualHeight < 1)
                return false;

            GeneralTransform inverse;
            try
            {
                inverse = host.TransformToAncestor(window).Inverse;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (inverse == null)
                return false;

            var sceneLocal = inverse.Transform(windowLocal);

            if (sceneLocal.X < 0 || sceneLocal.Y < 0 ||
                sceneLocal.X >= host.ActualWidth || sceneLocal.Y >= host.ActualHeight)
                return false;

            var mask = OverlayEdgeVignette.SampleMask(
                sceneLocal.X, sceneLocal.Y, host.ActualWidth, host.ActualHeight);
            if (mask <= 0)
                return false;

            var image = window.PondBackgroundImage;
            if (image != null &&
                image.Visibility == Visibility.Visible &&
                image.Source != null)
            {
                Point imageLocal;
                try
                {
                    var toImage = host.TransformToDescendant(image);
                    imageLocal = toImage.Transform(sceneLocal);
                }
                catch (InvalidOperationException)
                {
                    return false;
                }

                if (!TrySampleImageAlpha(image, imageLocal, out var pixelAlpha))
                    return false;
                return pixelAlpha * mask >= AlphaThreshold;
            }

            Point contentLocal = sceneLocal;
            var content = window.SceneContentCanvas;
            if (content != null)
            {
                try
                {
                    contentLocal = host.TransformToDescendant(content).Transform(sceneLocal);
                }
                catch (InvalidOperationException)
                {
                    contentLocal = sceneLocal;
                }
            }

            return HitsFallbackShape(window, contentLocal) && mask * 255 >= AlphaThreshold;
        }

        static bool HitsFallbackShape(MainWindow window, Point sceneLocal)
        {
            if (IsVisibleFilled(window.GrassLayer) &&
                new Rect(0, 0, window.GrassLayer.ActualWidth, window.GrassLayer.ActualHeight)
                    .Contains(sceneLocal))
                return true;

            if (IsVisibleFilled(window.ShoreLayer) &&
                PointInEllipse(window.ShoreLayer, sceneLocal))
                return true;

            if (IsVisibleFilled(window.WaterLayer) &&
                PointInEllipse(window.WaterLayer, sceneLocal))
                return true;

            return false;
        }

        static bool IsVisibleFilled(Shape shape)
        {
            return shape != null &&
                   shape.Visibility == Visibility.Visible &&
                   shape.Fill != null;
        }

        static bool PointInEllipse(Ellipse ellipse, Point sceneLocal)
        {
            var left = Canvas.GetLeft(ellipse);
            var top = Canvas.GetTop(ellipse);
            if (double.IsNaN(left))
                left = 0;
            if (double.IsNaN(top))
                top = 0;
            var w = ellipse.ActualWidth;
            var h = ellipse.ActualHeight;
            if (w < 1 || h < 1)
                return false;
            var nx = (sceneLocal.X - left) / w - 0.5;
            var ny = (sceneLocal.Y - top) / h - 0.5;
            return nx * nx + ny * ny <= 0.25;
        }

        static bool TrySampleImageAlpha(Image image, Point imageLocal, out double alpha)
        {
            alpha = 0;
            var source = image.Source as BitmapSource;
            if (source == null)
                return false;

            var viewW = image.ActualWidth > 0 ? image.ActualWidth : image.Width;
            var viewH = image.ActualHeight > 0 ? image.ActualHeight : image.Height;
            if (viewW < 1 || viewH < 1)
                return false;

            EnsureAlphaCache(source);
            if (_cachedBgra == null || _cachedWidth < 1 || _cachedHeight < 1)
                return false;

            MapFill(
                imageLocal.X, imageLocal.Y, viewW, viewH,
                _cachedWidth, _cachedHeight,
                out var px, out var py);
            if (px < 0 || py < 0 || px >= _cachedWidth || py >= _cachedHeight)
                return true;

            alpha = _cachedBgra[py * _cachedStride + px * 4 + 3];
            return true;
        }

        static void MapFill(
            double x, double y, double viewW, double viewH,
            int srcW, int srcH,
            out int px, out int py)
        {
            px = (int)Math.Floor(x / viewW * srcW);
            py = (int)Math.Floor(y / viewH * srcH);
        }

        static void EnsureAlphaCache(BitmapSource source)
        {
            if (ReferenceEquals(_cachedSource, source) && _cachedBgra != null)
                return;

            try
            {
                BitmapSource bgra = source;
                if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
                {
                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = source;
                    converted.DestinationFormat = PixelFormats.Bgra32;
                    converted.EndInit();
                    if (converted.CanFreeze)
                        converted.Freeze();
                    bgra = converted;
                }

                _cachedWidth = bgra.PixelWidth;
                _cachedHeight = bgra.PixelHeight;
                _cachedStride = _cachedWidth * 4;
                _cachedBgra = new byte[_cachedStride * _cachedHeight];
                bgra.CopyPixels(_cachedBgra, _cachedStride, 0);
                _cachedSource = source;
            }
            catch
            {
                _cachedSource = null;
                _cachedBgra = null;
                _cachedWidth = 0;
                _cachedHeight = 0;
                _cachedStride = 0;
            }
        }
    }
}
