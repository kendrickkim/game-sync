using System.Diagnostics;
using System.Runtime.InteropServices;
using GameSync.Forms;

namespace GameSync.Services;

internal static class SingleInstance
{
    private const string MutexName = @"Local\GameSync.WinForms.SingleInstance";
    private const string ActivateEventName = @"Local\GameSync.WinForms.Activate";
    private const int WmActivateInstance = 0x0400; // WM_USER

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static ActivateSink? _sink;
    private static CancellationTokenSource? _cts;
    private static Thread? _listener;

    public static bool TryAcquireOrNotifyExisting()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            NotifyExisting();
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return true;
    }

    public static void BindUi()
    {
        if (_activateEvent is null || _listener is not null)
        {
            return;
        }

        _sink = new ActivateSink();
        _cts = new CancellationTokenSource();
        _listener = new Thread(ListenForActivation)
        {
            IsBackground = true,
            Name = "GameSync.SingleInstance",
        };
        _listener.Start();
    }

    public static void Release()
    {
        _cts?.Cancel();
        try
        {
            _activateEvent?.Set();
        }
        catch
        {
            // ignored
        }

        if (_listener is { IsAlive: true } && !_listener.Join(500))
        {
            // Background thread exits with the process.
        }

        _sink?.DestroyHandle();
        _sink = null;
        _activateEvent?.Dispose();
        _activateEvent = null;
        _cts?.Dispose();
        _cts = null;

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // This thread did not own the mutex.
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }

    private static void NotifyExisting()
    {
        NativeMethods.AllowSetForegroundWindow(NativeMethods.AsfwAny);
        foreach (var process in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
        {
            using (process)
            {
                if (process.Id != Environment.ProcessId)
                {
                    NativeMethods.AllowSetForegroundWindow(process.Id);
                }
            }
        }

        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(ActivateEventName);
            activateEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            MessageBox.Show(
                "Game Sync가 이미 실행 중입니다.",
                "Game Sync",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private static void ListenForActivation()
    {
        var token = _cts?.Token ?? CancellationToken.None;
        while (!token.IsCancellationRequested)
        {
            try
            {
                _activateEvent?.WaitOne();
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            var hwnd = _sink?.Handle ?? IntPtr.Zero;
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.PostMessage(hwnd, WmActivateInstance, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }

    private static void RestoreForegroundWindow()
    {
        Form? login = null;
        MainForm? main = null;
        Form? fallback = null;

        foreach (Form form in Application.OpenForms)
        {
            fallback ??= form;
            if (form is LoginForm)
            {
                login = form;
            }
            else if (form is MainForm mainForm)
            {
                main = mainForm;
            }
        }

        if (login is not null)
        {
            FocusForm(login);
            return;
        }

        if (main is not null)
        {
            main.RestoreFromTray();
            NativeMethods.FocusWindow(main.Handle);
            return;
        }

        if (fallback is not null)
        {
            FocusForm(fallback);
        }
    }

    private static void FocusForm(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.Show();
        form.Activate();
        form.BringToFront();
        form.TopMost = true;
        form.TopMost = false;
        NativeMethods.FocusWindow(form.Handle);
    }

    private sealed class ActivateSink : NativeWindow
    {
        public ActivateSink()
        {
            CreateHandle(new CreateParams
            {
                Caption = "GameSync.SingleInstance.Sink",
                Parent = NativeMethods.HwndMessage,
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmActivateInstance)
            {
                RestoreForegroundWindow();
                return;
            }

            base.WndProc(ref m);
        }
    }

    private static class NativeMethods
    {
        public const int AsfwAny = -1;
        public static readonly IntPtr HwndMessage = new(-3);
        private const int SwShow = 5;
        private const int SwRestore = 9;

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public static void FocusWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(hwnd, IsIconic(hwnd) ? SwRestore : SwShow);
            SetForegroundWindow(hwnd);

            var foreground = GetForegroundWindow();
            if (foreground == hwnd)
            {
                return;
            }

            var currentThread = GetCurrentThreadId();
            var foregroundThread = GetWindowThreadProcessId(foreground, out _);
            if (foregroundThread == 0 || foregroundThread == currentThread)
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
                return;
            }

            if (!AttachThreadInput(currentThread, foregroundThread, true))
            {
                return;
            }

            try
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }
}
