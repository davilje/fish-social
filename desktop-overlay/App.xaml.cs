using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Threading;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    public partial class App : Application
    {
        Mutex _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Run before actions can close their popup or raise the Unity window.
            // Button excludes the repeating controls inside scrollbars.
            EventManager.RegisterClassHandler(typeof(Button), ButtonBase.ClickEvent,
                new RoutedEventHandler(OnButtonActivated), true);
            EventManager.RegisterClassHandler(typeof(MenuItem), MenuItem.ClickEvent,
                new RoutedEventHandler(OnMenuItemActivated), true);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            bool created;
            _instanceMutex = new Mutex(true, "Local\\FishSocialOverlay-2713340", out created);
            if (!created)
            {
                _instanceMutex.Dispose();
                _instanceMutex = null;
                Shutdown();
                return;
            }
            base.OnStartup(e);
        }

        static void OnButtonActivated(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, e.Source) && sender is Button button && button.IsEnabled)
                (Current.MainWindow as MainWindow)?.PlayButtonAudio(button);
        }

        static void OnMenuItemActivated(object sender, RoutedEventArgs e)
        {
            // Leaf activation also bubbles through its parent MenuItems. Emit once,
            // including context menus hosted in a separate WPF popup source.
            if (ReferenceEquals(sender, e.Source) && sender is MenuItem item &&
                item.IsEnabled && !item.HasItems)
                (Current.MainWindow as MainWindow)?.PlayMenuItemAudio(item);
        }

        static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[Overlay] Unhandled UI exception: " + e.Exception);
            e.Handled = true;
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[Overlay] Unhandled exception: " + e.ExceptionObject);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
            base.OnExit(e);
        }
    }
}
