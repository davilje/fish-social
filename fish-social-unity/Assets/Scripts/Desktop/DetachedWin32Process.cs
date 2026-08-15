using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Starts and stops Windows processes without binding Unity's job
    /// object or shutdown sequence to the child.
    /// </summary>
    static class DetachedWin32Process
    {
        const uint CreateBreakawayFromJob = 0x01000000;
        const uint CreateNewProcessGroup = 0x00000200;
        const uint DetachedProcess = 0x00000008;
        const uint CreateNoWindow = 0x08000000;
        const int StartfUseShowWindow = 0x00000001;
        const short SwHide = 0;

        public static Process StartBreakaway(
            string fileName,
            string arguments,
            string workingDirectory)
        {
            var process = StartNative(
                fileName,
                arguments,
                CreateBreakawayFromJob | CreateNewProcessGroup | DetachedProcess,
                workingDirectory,
                false);
            if (process != null)
                return process;

            return Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                WorkingDirectory = workingDirectory,
            });
        }

        public static void TaskkillImage(string imageName)
        {
            StartHidden(
                Path.Combine(Environment.SystemDirectory, "taskkill.exe"),
                "/F /IM " + imageName + " /T");
        }

        public static void TaskkillPidAfterDelay(int processId, int pingCount)
        {
            StartHidden(
                Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                "/c ping 127.0.0.1 -n " + pingCount +
                " >nul & taskkill /PID " + processId + " /F >nul 2>&1");
        }

        static void StartHidden(string fileName, string arguments)
        {
            var process = StartNative(
                fileName,
                arguments,
                CreateBreakawayFromJob | CreateNewProcessGroup | CreateNoWindow,
                Environment.SystemDirectory,
                true);
            process?.Dispose();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateProcess(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref StartupInfo lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        static Process StartNative(
            string fileName,
            string arguments,
            uint creationFlags,
            string workingDirectory,
            bool hideWindow)
        {
            var command = new StringBuilder();
            command.Append('"').Append(fileName).Append('"');
            if (!string.IsNullOrEmpty(arguments))
                command.Append(' ').Append(arguments);

            var startup = new StartupInfo();
            startup.cb = Marshal.SizeOf(typeof(StartupInfo));
            if (hideWindow)
            {
                startup.dwFlags = StartfUseShowWindow;
                startup.wShowWindow = SwHide;
            }

            ProcessInformation information;
            if (!CreateProcess(
                    fileName,
                    command,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    IntPtr.Zero,
                    string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory,
                    ref startup,
                    out information))
                return null;

            try
            {
                return Process.GetProcessById(information.dwProcessId);
            }
            finally
            {
                if (information.hThread != IntPtr.Zero)
                    CloseHandle(information.hThread);
                if (information.hProcess != IntPtr.Zero)
                    CloseHandle(information.hProcess);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct StartupInfo
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }
    }
}
