// Minimal DP/Loaded/dispatcher model for cross-platform tests of production
// localization and chat code. These tests do not validate WPF rendering or layout.
using System;
using System.Collections.Generic;
namespace System.Windows
{
    public sealed class DependencyProperty
    {
        internal PropertyMetadata Metadata;
        public static DependencyProperty RegisterAttached(string name, Type type, Type owner, PropertyMetadata metadata)
            => new DependencyProperty { Metadata = metadata };
    }
    public sealed class PropertyMetadata
    {
        public object Default;
        public Action<DependencyObject, DependencyPropertyChangedEventArgs> Changed;
        public PropertyMetadata(object value, Action<DependencyObject, DependencyPropertyChangedEventArgs> changed = null)
        { Default = value; Changed = changed; }
    }
    public struct DependencyPropertyChangedEventArgs { public object NewValue; }
    public class DependencyObject
    {
        readonly Dictionary<DependencyProperty, object> values = new Dictionary<DependencyProperty, object>();
        public Threading.Dispatcher Dispatcher = new Threading.Dispatcher();
        public object GetValue(DependencyProperty key) => values.TryGetValue(key, out var value) ? value : key.Metadata?.Default;
        public void SetValue(DependencyProperty key, object value)
        {
            var before = GetValue(key); values[key] = value;
            if (!Equals(before, value)) key.Metadata?.Changed?.Invoke(this, new DependencyPropertyChangedEventArgs { NewValue = value });
        }
    }
    public class RoutedEventArgs : EventArgs { }
    public struct Thickness
    {
        public double Left, Top, Right, Bottom;
        public Thickness(double l, double t, double r, double b) { Left=l; Top=t; Right=r; Bottom=b; }
    }
    public class FrameworkElement : DependencyObject
    {
        public static readonly DependencyProperty ToolTipProperty = new DependencyProperty();
        public object ToolTip { get => GetValue(ToolTipProperty); set => SetValue(ToolTipProperty, value); }
        public event EventHandler<RoutedEventArgs> Loaded;
        public void RaiseLoaded() => Loaded?.Invoke(this, new RoutedEventArgs());
        public double ActualHeight { get; set; }
        public Thickness Margin { get; set; }
    }
    public enum TextWrapping { Wrap }
}
namespace System.Windows.Threading
{
    public enum DispatcherPriority { Loaded }
    public sealed class DispatcherOperation { public void Abort() { } }
    public sealed class Dispatcher
    {
        public bool CheckAccess() => true;
        public DispatcherOperation BeginInvoke(Action action) { action(); return new DispatcherOperation(); }
        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Action action) => BeginInvoke(action);
    }
}
namespace System.Windows.Media
{
    public class Brush { }
    public class FontFamily { public FontFamily(string name) { } }
}
namespace System.Windows.Controls
{
    public class TextBlock : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = new DependencyProperty();
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public TextWrapping TextWrapping { get; set; }
        public Media.FontFamily FontFamily { get; set; }
        public double FontSize { get; set; }
        public Media.Brush Foreground { get; set; }
    }
    public class TextBox : FrameworkElement { public string Text { get; set; } }
    public class ContentControl : FrameworkElement
    {
        public static readonly DependencyProperty ContentProperty = new DependencyProperty();
        public object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    }
    public class HeaderedItemsControl : FrameworkElement
    {
        public static readonly DependencyProperty HeaderProperty = new DependencyProperty();
        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    }
    public class HeaderedContentControl : ContentControl
    {
        public static readonly DependencyProperty HeaderProperty = new DependencyProperty();
        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    }
    public class StackPanel : FrameworkElement { public List<FrameworkElement> Children = new List<FrameworkElement>(); }
    public class ScrollViewer : FrameworkElement
    {
        public double VerticalOffset { get; set; }
        public double ScrollableHeight { get; set; }
        public void ScrollToEnd() { }
        public void ScrollToVerticalOffset(double value) { VerticalOffset = value; }
    }
}
namespace System.Windows.Documents
{
    public class Run : DependencyObject { public static readonly DependencyProperty TextProperty = new DependencyProperty(); }
}
namespace FishSocialOverlay { static class CozyTheme { public static System.Windows.Media.Brush Ink = new System.Windows.Media.Brush(); } }
