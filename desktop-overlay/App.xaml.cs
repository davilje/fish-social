using System.Windows;
using System.Threading;

namespace FishSocialOverlay
{
    public partial class App : Application
    {
        Mutex _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
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
