using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FishSocial.Desktop
{
    public enum NativeOverlayLifecycleState
    {
        Stopped,
        Starting,
        Running,
        Hidden,
        Stopping,
    }

    /// <summary>
    /// Main-process controller for FishSocialOverlay.exe. The native process
    /// never owns authentication or game state; it only renders and sends UI
    /// commands over a versioned JSON-lines pipe.
    /// </summary>
    public sealed class NativeOverlayProcessController : MonoBehaviour
    {
        const string OverlayImageName = "FishSocialOverlay";

        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> _commands = new ConcurrentQueue<string>();
        readonly object _pipeLock = new object();
        readonly object _lifecycleLock = new object();
        readonly AutoResetEvent _writeSignal = new AutoResetEvent(false);
        Thread _serverThread;
        Thread _writerThread;
        NamedPipeServerStream _server;
        StreamWriter _writer;
        NativeOverlayStateDto _latestState = new NativeOverlayStateDto();
        long _nextStateSequence;
        Task _startTask;
        Task _shutdownTask;
        bool _stopping;
        bool _stateQueued;
        string _applicationDataPath;

        public string PipeName { get; private set; }
        public bool IsConnected { get; private set; }
        public NativeOverlayLifecycleState LifecycleState { get; private set; } =
            NativeOverlayLifecycleState.Stopped;
        public event Action<NativeOverlayCommandDto> CommandReceived;

        void Awake()
        {
            if (Application.isEditor)
                return;

            _applicationDataPath = Application.dataPath;
            PipeName = "FishSocialOverlay-" + Process.GetCurrentProcess().Id;
        }

        void Update()
        {
            while (_incoming.TryDequeue(out var line))
            {
                NativeOverlayCommandDto message;
                try
                {
                    message = JsonUtility.FromJson<NativeOverlayCommandDto>(line);
                }
                catch (Exception)
                {
                    continue;
                }

                if (message == null || message.version != 1)
                    continue;
                if (message.type == "hello")
                {
                    IsConnected = true;
                    SendLatestState();
                }
                else if (message.type == "command" && !string.IsNullOrEmpty(message.command))
                {
                    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    UnityEngine.Debug.Log(
                        "[Latency][Unity] overlay_command_received id=" +
                        message.commandId +
                        " command=" + message.command +
                        " ipcMs=" + (message.sentAtMs > 0
                            ? nowMs - message.sentAtMs : -1) +
                        " atMs=" + nowMs);
                    CommandReceived?.Invoke(message);
                }
            }
        }

        public void StartOverlay()
        {
            if (Application.isEditor)
                return;

            BackgroundRenderGate.SetOverlayActive(true);
            lock (_lifecycleLock)
            {
                if (LifecycleState == NativeOverlayLifecycleState.Starting ||
                    LifecycleState == NativeOverlayLifecycleState.Stopping)
                    return;

                if ((LifecycleState == NativeOverlayLifecycleState.Running ||
                     LifecycleState == NativeOverlayLifecycleState.Hidden) &&
                    OverlayImageRunning())
                {
                    LifecycleState = NativeOverlayLifecycleState.Running;
                    SendCommand("show_overlay");
                    SendLatestState();
                    return;
                }

                LifecycleState = NativeOverlayLifecycleState.Starting;
                _stopping = false;
                if (_startTask == null || _startTask.IsCompleted)
                    _startTask = Task.Run(StartOverlayWorker);
            }
        }

        void StartOverlayWorker()
        {
            try
            {
                var ipcDisabled = IsIpcDisabled();
                var executable = FindOverlayExecutable(_applicationDataPath);
                if (string.IsNullOrEmpty(executable))
                {
                    UnityEngine.Debug.LogWarning(
                        "[NativeOverlay] FishSocialOverlay.exe was not found.");
                    BackgroundRenderGate.SetOverlayActive(false);
                    SetLifecycleState(NativeOverlayLifecycleState.Stopped);
                    return;
                }

                if (!ipcDisabled)
                {
                    StartPipeServer();
                    StartWriterThread();
                }

                lock (_lifecycleLock)
                {
                    if (LifecycleState != NativeOverlayLifecycleState.Starting)
                        return;
                }

                UnityEngine.Debug.Log(
                    "[NativeOverlay] starting detached process. ipc=" + (!ipcDisabled));
                var process = DetachedWin32Process.StartBreakaway(
                    executable,
                    ipcDisabled ? string.Empty : "--pipe=" + PipeName,
                    Directory.GetParent(executable).FullName);
                if (process != null)
                    process.Dispose();

                lock (_lifecycleLock)
                {
                    if (LifecycleState != NativeOverlayLifecycleState.Starting)
                    {
                        DetachedWin32Process.TaskkillImage(OverlayImageName + ".exe");
                        return;
                    }
                    LifecycleState = NativeOverlayLifecycleState.Running;
                }
                SendLatestState();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[NativeOverlay] start failed: " + exception.Message);
                BackgroundRenderGate.SetOverlayActive(false);
                SetLifecycleState(NativeOverlayLifecycleState.Stopped);
            }
        }

        void SetLifecycleState(NativeOverlayLifecycleState state)
        {
            lock (_lifecycleLock)
                LifecycleState = state;
        }

        public void HideOverlay()
        {
            BackgroundRenderGate.SetOverlayActive(false);
            lock (_lifecycleLock)
            {
                if (LifecycleState == NativeOverlayLifecycleState.Running)
                    LifecycleState = NativeOverlayLifecycleState.Hidden;
            }
            SendCommand("hide_overlay");
        }

        public void ShowOverlay()
        {
            StartOverlay();
        }

        public void CloseOverlay()
        {
            ShutdownOverlayAsync(true);
        }

        public Task ShutdownOverlayAsync(bool forceAfterTimeout)
        {
            BackgroundRenderGate.SetOverlayActive(false);
            lock (_lifecycleLock)
            {
                if (_shutdownTask != null && !_shutdownTask.IsCompleted)
                    return _shutdownTask;
                if (LifecycleState == NativeOverlayLifecycleState.Stopped)
                    return Task.FromResult(0);

                LifecycleState = NativeOverlayLifecycleState.Stopping;
                _shutdownTask = Task.Run(() => ShutdownOverlayWorker(forceAfterTimeout));
                return _shutdownTask;
            }
        }

        public void ForceTerminateForApplicationQuit()
        {
            BackgroundRenderGate.SetOverlayActive(false);
            lock (_lifecycleLock)
            {
                if (LifecycleState == NativeOverlayLifecycleState.Stopped)
                    return;
                LifecycleState = NativeOverlayLifecycleState.Stopping;
            }

            UnityEngine.Debug.Log(
                "[NativeOverlay] aborting pipe and detaching overlay kill for quit.");
            AbortPipeIo();
            DetachedWin32Process.TaskkillImage(OverlayImageName + ".exe");
            SetLifecycleState(NativeOverlayLifecycleState.Stopped);
        }

        void ShutdownOverlayWorker(bool forceAfterTimeout)
        {
            SendCommand("quit_overlay");
            _writeSignal.Set();
            AbortPipeIo();
            if (forceAfterTimeout)
                DetachedWin32Process.TaskkillImage(OverlayImageName + ".exe");
            _serverThread = null;
            _writerThread = null;
            SetLifecycleState(NativeOverlayLifecycleState.Stopped);
        }

        void AbortPipeIo()
        {
            _stopping = true;
            _writeSignal.Set();

            IntPtr handle = IntPtr.Zero;
            lock (_pipeLock)
            {
                _writer = null;
                IsConnected = false;
                if (_server != null)
                {
                    try
                    {
                        handle = _server.SafePipeHandle.DangerousGetHandle();
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            {
                try
                {
                    DetachedWin32Process.CancelIoEx(handle, IntPtr.Zero);
                }
                catch (Exception)
                {
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(PipeName))
                {
                    using (var dummy = new NamedPipeClientStream(
                               ".",
                               PipeName,
                               PipeDirection.InOut,
                               PipeOptions.Asynchronous))
                    {
                        dummy.Connect(50);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public void PublishState(NativeOverlayStateDto state)
        {
            if (state == null)
                return;
            lock (_lifecycleLock)
            {
                if (LifecycleState == NativeOverlayLifecycleState.Stopped ||
                    LifecycleState == NativeOverlayLifecycleState.Stopping)
                    return;
            }
            _latestState = state;
            _latestState.sequence = ++_nextStateSequence;
            UnityEngine.Debug.Log(
                "[Latency][Unity] overlay_state_queued sequence=" +
                _latestState.sequence +
                " atMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            SendLatestState();
        }

        void SendLatestState()
        {
            lock (_lifecycleLock)
            {
                if (LifecycleState == NativeOverlayLifecycleState.Stopped ||
                    LifecycleState == NativeOverlayLifecycleState.Stopping)
                    return;
                _stateQueued = true;
            }
            _writeSignal.Set();
        }

        void SendCommand(string command)
        {
            lock (_lifecycleLock)
            {
                if (LifecycleState == NativeOverlayLifecycleState.Stopped)
                    return;
                _commands.Enqueue(JsonUtility.ToJson(new NativeOverlayCommandDto
                {
                    type = "command",
                    version = 1,
                    command = command,
                }));
            }
            _writeSignal.Set();
        }

        void StartWriterThread()
        {
            if (_writerThread != null && _writerThread.IsAlive)
                return;
            _writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "FishSocialNativeOverlayWriter",
            };
            _writerThread.Start();
        }

        void WriterLoop()
        {
            while (!_stopping)
            {
                // Commands and the latest coalesced state wake the writer
                // immediately; a timed wait added a fixed 250ms tail latency.
                _writeSignal.WaitOne();
                while (!_stopping)
                {
                    string message = null;
                    lock (_lifecycleLock)
                    {
                        if (_commands.TryDequeue(out var command))
                            message = command;
                        else if (_stateQueued)
                        {
                            _stateQueued = false;
                            message = JsonUtility.ToJson(_latestState);
                        }
                    }

                    if (message == null)
                        break;

                    lock (_pipeLock)
                    {
                        if (_writer == null)
                            continue;
                        try
                        {
                            _writer.WriteLine(message);
                            _writer.Flush();
                        }
                        catch (Exception exception) when (
                            exception is IOException ||
                            exception is ObjectDisposedException)
                        {
                            _writer = null;
                            IsConnected = false;
                        }
                    }
                }
            }
        }

        void StartPipeServer()
        {
            if (_serverThread != null && _serverThread.IsAlive)
                return;
            _serverThread = new Thread(PipeServerLoop)
            {
                IsBackground = true,
                Name = "FishSocialNativeOverlayPipe",
            };
            _serverThread.Start();
        }

        static bool IsIpcDisabled()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("FISH_SOCIAL_OVERLAY_NO_IPC"),
                "1",
                StringComparison.OrdinalIgnoreCase);
        }

        static bool OverlayImageRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(OverlayImageName);
                var running = processes != null && processes.Length > 0;
                if (processes != null)
                {
                    for (var i = 0; i < processes.Length; i++)
                        processes[i].Dispose();
                }
                return running;
            }
            catch (Exception)
            {
                return false;
            }
        }

        void PipeServerLoop()
        {
            while (!_stopping)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(
                               PipeName,
                               PipeDirection.InOut,
                               1,
                               PipeTransmissionMode.Byte,
                               PipeOptions.Asynchronous))
                    {
                        lock (_pipeLock)
                            _server = server;
                        server.WaitForConnection();
                        using (var reader = new StreamReader(server))
                        using (var writer = new StreamWriter(server) { AutoFlush = true })
                        {
                            lock (_pipeLock)
                            {
                                _writer = writer;
                                IsConnected = true;
                            }

                            string line;
                            while (!_stopping && server.IsConnected &&
                                   (line = reader.ReadLine()) != null)
                                _incoming.Enqueue(line);
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (!_stopping)
                        UnityEngine.Debug.LogWarning(
                            "[NativeOverlay] pipe stopped: " + exception.Message);
                }
                finally
                {
                    lock (_pipeLock)
                    {
                        _writer = null;
                        _server = null;
                        IsConnected = false;
                    }
                }
            }
        }

        static string FindOverlayExecutable(string applicationDataPath)
        {
            var configured = Environment.GetEnvironmentVariable("FISH_SOCIAL_OVERLAY_PATH");
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
                return configured;

            var root = Directory.GetParent(applicationDataPath)?.FullName;
            if (string.IsNullOrEmpty(root))
                return null;

            var candidates = new[]
            {
                Path.Combine(root, "FishSocialOverlay.exe"),
                Path.Combine(root, "FishSocialOverlay", "FishSocialOverlay.exe"),
                Path.Combine(root, "Overlay", "FishSocialOverlay.exe"),
            };
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        void OnDestroy()
        {
            UnityEngine.Debug.Log("[Shutdown] NativeOverlayProcessController.OnDestroy begin.");
            ForceTerminateForApplicationQuit();
            UnityEngine.Debug.Log("[Shutdown] NativeOverlayProcessController.OnDestroy complete.");
        }
    }
}
