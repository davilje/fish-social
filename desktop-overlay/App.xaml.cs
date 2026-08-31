using System;
using System.Windows;
using System.Threading;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    public partial class App : Application
    {
        Mutex _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
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
