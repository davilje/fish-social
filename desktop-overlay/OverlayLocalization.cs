using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using FishSocial.Localization;

namespace FishSocialOverlay
{
    /// <summary>
    /// Explicit localization bindings. Ordinary player strings have no render plan
    /// and are never looked up merely because they resemble a translated label.
    /// </summary>
    public static class OverlayLocalization
    {
        sealed class ValueBinding
        {
            public WeakReference<DependencyObject> Target;
            public DependencyProperty Property;
            public string Original;
            public object LastApplied;
        }

        static bool initialized;
        static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, ValueBinding>> bindings =
            new ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, ValueBinding>>();
        static readonly List<WeakReference<ValueBinding>> liveBindings = new List<WeakReference<ValueBinding>>();

        public static string Language { get { EnsureInitialized(); return LocalizationCatalog.Language; } }
        public static event Action LanguageChanged;

        public static void EnsureInitialized()
        {
            if (initialized) return;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "OverlayResources", "Localization", "text-catalog.json");
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    LocalizationCatalog.Load(OverlayLocalizationCatalogLoader.Read(stream));
                }
            }
            catch (IOException error)
            {
                System.Diagnostics.Debug.WriteLine("[Localization] Catalog unavailable: " + error.Message);
                LocalizationCatalog.Load(Array.Empty<LocalizationEntry>());
            }
            catch (SerializationException error)
            {
                System.Diagnostics.Debug.WriteLine("[Localization] Catalog malformed: " + error.Message);
                LocalizationCatalog.Load(Array.Empty<LocalizationEntry>());
            }
            initialized = true;
            LocalizationCatalog.LanguageChanged += RefreshBindings;
        }

        public static bool SetLanguage(string language)
        {
            EnsureInitialized();
            var previous = LocalizationCatalog.Language;
            LocalizationCatalog.SetLanguage(language);
            return !string.Equals(previous, LocalizationCatalog.Language, StringComparison.Ordinal);
        }

        public static string Text(string key, string fallback = null)
        {
            EnsureInitialized();
            return LocalizationCatalog.Text(key, fallback);
        }

        public static string Format(string key, params object[] arguments)
        {
            EnsureInitialized();
            return LocalizationCatalog.Format(key, arguments);
        }

        /// <summary>Use only for server errors, system announcements and private observation hints.</summary>
        public static string Message(string value)
        {
            EnsureInitialized();
            return LocalizationCatalog.TranslateMessage(value);
        }

        public static string Data(string value)
        {
            EnsureInitialized();
            return LocalizationCatalog.BySource(value);
        }

        /// <summary>Retain translation plans assigned by an object initializer.</summary>
        public static T Track<T>(T element) where T : FrameworkElement
        {
            EnsureInitialized();
            if (element == null) return element;
            if (element is TextBlock text && LocalizationCatalog.IsLocalized(text.Text))
                SetText(text, text.Text);
            // TextBox.Text is editable player input and deliberately has no binding.
            if (element is HeaderedItemsControl items &&
                items.Header is string itemsHeader && LocalizationCatalog.IsLocalized(itemsHeader))
                SetHeader(items, itemsHeader);
            if (element is HeaderedContentControl header &&
                header.Header is string headerText && LocalizationCatalog.IsLocalized(headerText))
                SetHeader(header, headerText);
            if (element is ContentControl content &&
                content.Content is string contentText && LocalizationCatalog.IsLocalized(contentText))
                SetContent(content, contentText);
            if (element.ToolTip is string toolTip && LocalizationCatalog.IsLocalized(toolTip))
                SetToolTip(element, toolTip);
            return element;
        }

        public static string SetText(TextBlock target, string value)
        {
            return Bind(target, TextBlock.TextProperty, value);
        }

        public static string SetText(Run target, string value)
        {
            return Bind(target, Run.TextProperty, value);
        }

        // Editable content belongs to the player, including strings equal to UI copy.
        public static string SetText(TextBox target, string value)
        {
            if (target != null) target.Text = value ?? string.Empty;
            return value;
        }

        public static string SetContent(ContentControl target, string value)
        {
            return Bind(target, ContentControl.ContentProperty, value);
        }

        public static object SetContent(ContentControl target, object value)
        {
            if (value is string text) return SetContent(target, text);
            Forget(target, ContentControl.ContentProperty);
            if (target != null) target.Content = value;
            return value;
        }

        public static string SetHeader(HeaderedItemsControl target, string value)
        {
            return Bind(target, HeaderedItemsControl.HeaderProperty, value);
        }

        public static object SetHeader(HeaderedItemsControl target, object value)
        {
            if (value is string text) return SetHeader(target, text);
            Forget(target, HeaderedItemsControl.HeaderProperty);
            if (target != null) target.Header = value;
            return value;
        }

        public static string SetHeader(HeaderedContentControl target, string value)
        {
            return Bind(target, HeaderedContentControl.HeaderProperty, value);
        }

        public static object SetHeader(HeaderedContentControl target, object value)
        {
            if (value is string text) return SetHeader(target, text);
            Forget(target, HeaderedContentControl.HeaderProperty);
            if (target != null) target.Header = value;
            return value;
        }

        public static string SetToolTip(FrameworkElement target, string value)
        {
            return Bind(target, FrameworkElement.ToolTipProperty, value);
        }

        public static object SetToolTip(FrameworkElement target, object value)
        {
            if (value is string text) return SetToolTip(target, text);
            Forget(target, FrameworkElement.ToolTipProperty);
            if (target != null) target.ToolTip = value;
            return value;
        }

        static string Bind(DependencyObject target, DependencyProperty property, string value)
        {
            EnsureInitialized();
            if (target == null) return value;
            var perTarget = bindings.GetOrCreateValue(target);
            if (!perTarget.TryGetValue(property, out var binding))
            {
                binding = new ValueBinding { Target = new WeakReference<DependencyObject>(target), Property = property };
                perTarget.Add(property, binding);
                liveBindings.Add(new WeakReference<ValueBinding>(binding));
                if (liveBindings.Count > 2048) RemoveDeadBindings();
            }
            binding.Original = value;
            var rendered = LocalizationCatalog.Render(value);
            target.SetValue(property, rendered);
            binding.LastApplied = target.GetValue(property);
            return rendered;
        }

        static void Forget(DependencyObject target, DependencyProperty property)
        {
            if (target != null && bindings.TryGetValue(target, out var perTarget)) perTarget.Remove(property);
        }

        static void RemoveDeadBindings()
        {
            liveBindings.RemoveAll(reference => !reference.TryGetTarget(out var binding) ||
                !binding.Target.TryGetTarget(out var _));
        }

        static void RefreshBindings()
        {
            RemoveDeadBindings();
            foreach (var reference in liveBindings.ToArray())
            {
                if (!reference.TryGetTarget(out var binding) || !binding.Target.TryGetTarget(out var target)) continue;
                if (!target.Dispatcher.CheckAccess())
                {
                    target.Dispatcher.BeginInvoke(new Action(() => Refresh(binding)));
                    continue;
                }
                Refresh(binding);
            }
            LanguageChanged?.Invoke();
        }

        static void Refresh(ValueBinding binding)
        {
            if (!binding.Target.TryGetTarget(out var target)) return;
            // A direct user/external assignment supersedes an older localization binding.
            if (!Equals(target.GetValue(binding.Property), binding.LastApplied))
            {
                Forget(target, binding.Property);
                return;
            }
            target.SetValue(binding.Property, LocalizationCatalog.Render(binding.Original));
            binding.LastApplied = target.GetValue(binding.Property);
        }

        public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
            "Key", typeof(string), typeof(OverlayLocalization), new PropertyMetadata(null, OnKeyChanged));
        public static string GetKey(DependencyObject target) { return (string)target.GetValue(KeyProperty); }
        public static void SetKey(DependencyObject target, string value) { target.SetValue(KeyProperty, value); }

        public static readonly DependencyProperty ToolTipKeyProperty = DependencyProperty.RegisterAttached(
            "ToolTipKey", typeof(string), typeof(OverlayLocalization), new PropertyMetadata(null, OnToolTipKeyChanged));
        public static string GetToolTipKey(DependencyObject target) { return (string)target.GetValue(ToolTipKeyProperty); }
        public static void SetToolTipKey(DependencyObject target, string value) { target.SetValue(ToolTipKeyProperty, value); }

        static void OnKeyChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            ApplyKey(target);
            if (target is FrameworkElement element) element.Loaded += OnKeyTargetLoaded;
        }

        static void OnKeyTargetLoaded(object sender, RoutedEventArgs args) { ApplyKey((DependencyObject)sender); }

        static void ApplyKey(DependencyObject target)
        {
            var key = GetKey(target);
            if (string.IsNullOrEmpty(key)) return;
            if (target is TextBlock text) SetText(text, Text(key, text.Text));
            else if (target is HeaderedItemsControl header) SetHeader(header, Text(key, header.Header as string));
            else if (target is HeaderedContentControl headerContent) SetHeader(headerContent, Text(key, headerContent.Header as string));
            else if (target is ContentControl content) SetContent(content, Text(key, content.Content as string));
        }

        static void OnToolTipKeyChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (target is FrameworkElement element)
            {
                SetToolTip(element, Text((string)args.NewValue, element.ToolTip as string));
                element.Loaded += OnToolTipTargetLoaded;
            }
        }

        static void OnToolTipTargetLoaded(object sender, RoutedEventArgs args)
        {
            var target = (FrameworkElement)sender;
            SetToolTip(target, Text(GetToolTipKey(target), target.ToolTip as string));
        }
    }
}
