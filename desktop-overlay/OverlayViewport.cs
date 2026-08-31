using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;

namespace FishSocialOverlay
{
    /// <summary>
    /// Crop presets for the Overlay window (STEAM-DESKTOP-16). Not scale.
    /// </summary>
    readonly struct OverlayViewportPreset
    {
        public readonly string Id;
        public readonly string Label;
        public readonly double Width;
        public readonly double Height;

        OverlayViewportPreset(string id, string label, double width, double height)
        {
            Id = id;
            Label = label;
            Width = width;
            Height = height;
        }

        public static readonly OverlayViewportPreset Standard =
            new OverlayViewportPreset("960x560", "标准 960×560", 960, 560);

        public static readonly OverlayViewportPreset Medium =
            new OverlayViewportPreset("800x400", "中 800×400", 800, 400);

        public static readonly OverlayViewportPreset Small =
            new OverlayViewportPreset("600x300", "小 600×300", 600, 300);

        public static readonly OverlayViewportPreset[] All =
        {
            Standard,
            Medium,
            Small,
        };

        public bool CollapseMenuRail => Height <= 300.5;

        public static OverlayViewportPreset FromId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return Standard;
            foreach (var preset in All)
            {
                if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
                    return preset;
            }

            return Standard;
        }

        public static OverlayViewportPreset FromSize(double width, double height)
        {
            foreach (var preset in All)
            {
                if (Math.Abs(preset.Width - width) < 0.5 &&
                    Math.Abs(preset.Height - height) < 0.5)
                    return preset;
            }

            return Standard;
        }
    }

    static class OverlayViewportStore
    {
        const string FolderName = "FishSocial";
        const string FileName = "overlay-viewport.json";

        public static OverlayViewportPreset LoadOrDefault()
        {
            try
            {
                var path = SettingsPath();
                if (!File.Exists(path))
                    return OverlayViewportPreset.Standard;

                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(OverlayViewportSettings));
                    var settings = serializer.ReadObject(stream) as OverlayViewportSettings;
                    return OverlayViewportPreset.FromId(settings?.preset);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OverlayViewport] Load failed: " + ex.Message);
                return OverlayViewportPreset.Standard;
            }
        }

        public static void Save(OverlayViewportPreset preset)
        {
            try
            {
                var path = SettingsPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var settings = new OverlayViewportSettings { preset = preset.Id };
                using (var stream = File.Create(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(OverlayViewportSettings));
                    serializer.WriteObject(stream, settings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OverlayViewport] Save failed: " + ex.Message);
            }
        }

        public static void AnchorResize(Window window, double newWidth, double newHeight)
        {
            if (window == null)
                return;

            var oldWidth = window.Width;
            var oldHeight = window.Height;
            var oldLeft = window.Left;
            var oldTop = window.Top;
            window.Width = newWidth;
            window.Height = newHeight;
            if (window.IsLoaded &&
                !double.IsNaN(oldLeft) && !double.IsNaN(oldTop) &&
                oldWidth > 0 && oldHeight > 0)
            {
                window.Top = oldTop + (oldHeight - newHeight);
                window.Left = oldLeft + (oldWidth - newWidth) / 2.0;
                ClampToWorkAreaPreferBottom(window);
            }
        }

        public static void ClampToWorkAreaPreferBottom(Window window)
        {
            if (window == null)
                return;

            var work = SystemParameters.WorkArea;
            var width = window.ActualWidth > 1 ? window.ActualWidth : window.Width;
            var height = window.ActualHeight > 1 ? window.ActualHeight : window.Height;
            if (width < 1)
                width = window.Width;
            if (height < 1)
                height = window.Height;

            if (window.Top + height > work.Bottom)
                window.Top = work.Bottom - height;
            if (window.Top < work.Top)
                window.Top = work.Top;
            if (window.Left + width > work.Right)
                window.Left = work.Right - width;
            if (window.Left < work.Left)
                window.Left = work.Left;
        }

        static string SettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                FolderName,
                FileName);
        }

        [DataContract]
        sealed class OverlayViewportSettings
        {
            [DataMember(Name = "preset")]
            public string preset;
        }
    }
}
