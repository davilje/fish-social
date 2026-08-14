using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Main-process controller for FishSocialOverlay.exe. The native process
    /// never owns authentication or game state; it only renders and sends UI
    /// commands over a versioned JSON-lines pipe.
    /// </summary>
    public sealed class NativeOverlayProcessController : MonoBehaviour
    {
        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        readonly object _pipeLock = new object();
        Thread _serverThread;
        NamedPipeServerStream _server;
        StreamWriter _writer;
        Process _overlayProcess;
        NativeOverlayStateDto _latestState = new NativeOverlayStateDto();
        bool _stopping;

        public string PipeName { get; private set; }
        public bool IsConnected { get; private set; }
        public event Action<string> CommandReceived;

        void Awake()
        {
            if (Application.isEditor)
                return;

            PipeName = "FishSocialOverlay-" + Process.GetCurrentProcess().Id;
            StartPipeServer();
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
                    CommandReceived?.Invoke(message.command);
            }

            if (_overlayProcess != null && _overlayProcess.HasExited)
            {
                _overlayProcess.Dispose();
                _overlayProcess = null;
                IsConnected = false;
            }
        }

        public void StartOverlay()
        {
            if (Application.isEditor)
                return;

            if (_overlayProcess != null && !_overlayProcess.HasExited)
            {
                SendLatestState();
                return;
            }

            var executable = FindOverlayExecutable();
            if (string.IsNullOrEmpty(executable))
            {
                UnityEngine.Debug.LogWarning(
                    "[NativeOverlay] FishSocialOverlay.exe was not found; main window remains usable.");
                return;
            }

            try
            {
                _overlayProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--pipe=" + PipeName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetParent(executable).FullName,
                });
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[NativeOverlay] start failed: " + exception.Message);
            }
        }

        public void HideOverlay()
        {
            SendCommand("hide_overlay");
        }

        public void ShowOverlay()
        {
            StartOverlay();
            SendCommand("show_overlay");
        }

        public void CloseOverlay()
        {
            var process = Interlocked.Exchange(ref _overlayProcess, null);
            if (process == null)
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning(
                        "[NativeOverlay] cleanup failed: " + exception.Message);
                }
                finally
                {
                    process.Dispose();
                }
            });
        }

        public void PublishState(NativeOverlayStateDto state)
        {
            if (state == null)
                return;
            _latestState = state;
            _latestState.sequence++;
            SendLatestState();
        }

        void SendLatestState()
        {
            Send(JsonUtility.ToJson(_latestState));
        }

        void SendCommand(string command)
        {
            Send(JsonUtility.ToJson(new NativeOverlayCommandDto
            {
                type = "command",
                version = 1,
                command = command,
            }));
        }

        void Send(string line)
        {
            lock (_pipeLock)
            {
                if (_writer == null)
                    return;

                try
                {
                    _writer.WriteLine(line);
                    _writer.Flush();
                }
                catch (IOException)
                {
                    _writer = null;
                    IsConnected = false;
                }
            }
        }

        void StartPipeServer()
        {
            _serverThread = new Thread(PipeServerLoop)
            {
                IsBackground = true,
                Name = "FishSocialNativeOverlayPipe",
            };
            _serverThread.Start();
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
                               PipeOptions.None))
                    {
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
                        IsConnected = false;
                    }
                }
            }
        }

        static string FindOverlayExecutable()
        {
            var configured = Environment.GetEnvironmentVariable("FISH_SOCIAL_OVERLAY_PATH");
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
                return configured;

            var root = Directory.GetParent(Application.dataPath)?.FullName;
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
            _stopping = true;
            NamedPipeServerStream server;
            lock (_pipeLock)
            {
                _writer = null;
                server = _server;
                _server = null;
            }
            if (server != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        server.Dispose();
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogWarning(
                            "[NativeOverlay] pipe cleanup failed: " + exception.Message);
                    }
                });
            }
            CloseOverlay();
            UnityEngine.Debug.Log("[Shutdown] NativeOverlayProcessController.OnDestroy complete.");
        }
    }
}
