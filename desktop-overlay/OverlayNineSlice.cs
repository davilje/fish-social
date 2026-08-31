using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    /// <summary>
    /// Unity uGUI Image Type = Sliced.
    /// sliceBorder order: left, bottom, right, top (Unity Sprite.border, texture pixels).
    ///
    /// Display borders stay at those texture pixels (1 texel = 1 UI px). They are not
    /// scaled by dest/src. Only shrink when the target is smaller than left+right or top+bottom.
    /// If the source has no stretchable center (T+B == height), borrow 1px from the inner seam.
    /// </summary>
    internal static class OverlayNineSlice
    {
        static readonly Dictionary<string, BitmapSource> RenderCache =
            new Dictionary<string, BitmapSource>(StringComparer.Ordinal);
        static readonly Dictionary<string, ImageBrush> BrushCache =
            new Dictionary<string, ImageBrush>(StringComparer.Ordinal);

        public static bool IsValid(int[] sliceBorder)
        {
            if (sliceBorder == null || sliceBorder.Length != 4)
                return false;
            return sliceBorder[0] > 0 || sliceBorder[1] > 0 || sliceBorder[2] > 0 || sliceBorder[3] > 0;
        }

        public static ImageBrush TryGetBrush(
            BitmapSource source,
            int[] sliceBorder,
            double targetWidth,
            double targetHeight)
        {
            var key = MakeKey(source, sliceBorder, targetWidth, targetHeight);
            if (string.IsNullOrEmpty(key))
                return null;
            if (BrushCache.TryGetValue(key, out var cached))
                return cached;

            var bitmap = TryRender(source, sliceBorder, targetWidth, targetHeight);
            if (bitmap == null)
                return null;

            var brush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.Fill,
                TileMode = TileMode.None,
            };
            brush.Freeze();
            BrushCache[key] = brush;
            return brush;
        }

        public static BitmapSource TryRender(
            BitmapSource source,
            int[] sliceBorder,
            double targetWidth,
            double targetHeight)
        {
            var key = MakeKey(source, sliceBorder, targetWidth, targetHeight);
            if (string.IsNullOrEmpty(key))
                return null;
            if (RenderCache.TryGetValue(key, out var cached))
                return cached;

            try
            {
                var width = Math.Max(1, (int)Math.Round(targetWidth));
                var height = Math.Max(1, (int)Math.Round(targetHeight));
                var grid = Create(source, sliceBorder, width, height);
                grid.Width = width;
                grid.Height = height;
                grid.Measure(new Size(width, height));
                grid.Arrange(new Rect(0, 0, width, height));
                grid.UpdateLayout();

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(grid);
                rtb.Freeze();
                RenderCache[key] = rtb;
                return rtb;
            }
            catch
            {
                return null;
            }
        }

        public static Grid TryCreate(BitmapSource source, int[] sliceBorder, double targetWidth, double targetHeight)
        {
            try
            {
                if (source == null || !IsValid(sliceBorder))
                    return null;
                if (double.IsNaN(targetWidth) || double.IsNaN(targetHeight) ||
                    targetWidth <= 0 || targetHeight <= 0)
                    return null;
                return Create(source, sliceBorder, targetWidth, targetHeight);
            }
            catch
            {
                return null;
            }
        }

        static string MakeKey(BitmapSource source, int[] sliceBorder, double targetWidth, double targetHeight)
        {
            if (source == null || !IsValid(sliceBorder))
                return null;
            if (double.IsNaN(targetWidth) || double.IsNaN(targetHeight) ||
                targetWidth <= 0 || targetHeight <= 0)
                return null;

            var width = Math.Max(1, (int)Math.Round(targetWidth));
            var height = Math.Max(1, (int)Math.Round(targetHeight));
            return RuntimeHelpers.GetHashCode(source) + ":" +
                   source.PixelWidth + "x" + source.PixelHeight + ":" +
                   sliceBorder[0] + "," + sliceBorder[1] + "," + sliceBorder[2] + "," + sliceBorder[3] +
                   ":" + width + "x" + height;
        }

        public static Grid Create(BitmapSource source, int[] sliceBorder, double targetWidth, double targetHeight)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!IsValid(sliceBorder))
                throw new ArgumentException("Invalid slice border.", nameof(sliceBorder));
            if (double.IsNaN(targetWidth) || double.IsNaN(targetHeight) ||
                targetWidth <= 0 || targetHeight <= 0)
                throw new ArgumentException("Target size must be positive.", nameof(targetWidth));

            var imgW = source.PixelWidth;
            var imgH = source.PixelHeight;
            var texLeft = ClampSlice(sliceBorder[0], imgW);
            var texRight = ClampSlice(sliceBorder[2], imgW - texLeft);
            var texTop = ClampSlice(sliceBorder[3], imgH);
            var texBottom = ClampSlice(sliceBorder[1], imgH - texTop);

            // A 200x40 sprite with T22/B18 has no vertical center. Borrow 1px so the bar can grow.
            EnsureStretchableCenter(ref texLeft, ref texRight, imgW);
            EnsureStretchableCenter(ref texTop, ref texBottom, imgH);

            var centerW = Math.Max(1, imgW - texLeft - texRight);
            var centerH = Math.Max(1, imgH - texTop - texBottom);

            var display = DisplayBorder(texLeft, texBottom, texRight, texTop, targetWidth, targetHeight);
            var left = display[0];
            var bottom = display[1];
            var right = display[2];
            var top = display[3];

            var grid = new Grid
            {
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(left, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(right, GridUnitType.Pixel) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(top, GridUnitType.Pixel) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(bottom, GridUnitType.Pixel) });

            AddCell(grid, 0, 0, Crop(source, 0, 0, texLeft, texTop));
            AddCell(grid, 1, 0, Crop(source, texLeft, 0, centerW, texTop));
            AddCell(grid, 2, 0, Crop(source, imgW - texRight, 0, texRight, texTop));

            AddCell(grid, 0, 1, Crop(source, 0, texTop, texLeft, centerH));
            AddCell(grid, 1, 1, Crop(source, texLeft, texTop, centerW, centerH));
            AddCell(grid, 2, 1, Crop(source, imgW - texRight, texTop, texRight, centerH));

            AddCell(grid, 0, 2, Crop(source, 0, imgH - texBottom, texLeft, texBottom));
            AddCell(grid, 1, 2, Crop(source, texLeft, imgH - texBottom, centerW, texBottom));
            AddCell(grid, 2, 2, Crop(source, imgW - texRight, imgH - texBottom, texRight, texBottom));

            return grid;
        }

        static void EnsureStretchableCenter(ref int leading, ref int trailing, int size)
        {
            if (size <= 0)
                return;
            if (leading + trailing < size)
                return;
            if (leading + trailing > size)
            {
                var shrink = size / (double)(leading + trailing);
                leading = Math.Max(0, (int)Math.Floor(leading * shrink));
                trailing = Math.Max(0, size - leading);
            }

            if (leading + trailing >= size && size >= 1)
            {
                if (leading >= trailing && leading > 0)
                    leading -= 1;
                else if (trailing > 0)
                    trailing -= 1;
            }
        }

        static int[] DisplayBorder(
            int left,
            int bottom,
            int right,
            int top,
            double targetWidth,
            double targetHeight)
        {
            var maxH = Math.Max(1, (int)Math.Round(targetWidth));
            var maxV = Math.Max(1, (int)Math.Round(targetHeight));
            if (left + right > maxH)
            {
                var shrink = maxH / (double)(left + right);
                left = Math.Max(0, (int)Math.Round(left * shrink));
                right = Math.Max(0, maxH - left);
            }

            if (top + bottom > maxV)
            {
                var shrink = maxV / (double)(top + bottom);
                top = Math.Max(0, (int)Math.Round(top * shrink));
                bottom = Math.Max(0, maxV - top);
            }

            return new[] { left, bottom, right, top };
        }

        static int ClampSlice(int value, int max)
        {
            if (value < 0)
                return 0;
            if (max <= 0)
                return 0;
            return Math.Min(value, max);
        }

        static BitmapSource Crop(BitmapSource source, int x, int y, int width, int height)
        {
            var maxX = Math.Max(0, source.PixelWidth - 1);
            var maxY = Math.Max(0, source.PixelHeight - 1);
            x = Math.Max(0, Math.Min(x, maxX));
            y = Math.Max(0, Math.Min(y, maxY));
            width = Math.Max(1, Math.Min(width, source.PixelWidth - x));
            height = Math.Max(1, Math.Min(height, source.PixelHeight - y));
            var cropped = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            cropped.Freeze();
            return cropped;
        }

        static void AddCell(Grid grid, int column, int row, BitmapSource source)
        {
            var image = new Image
            {
                Source = source,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Grid.SetColumn(image, column);
            Grid.SetRow(image, row);
            grid.Children.Add(image);
        }
    }
}
