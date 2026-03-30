using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Aion2DPSViewer.Core;

public class ForegroundWatcher
{
    private readonly string _processName;
    private readonly int _selfPid;
    private Timer? _timer;
    private bool _lastActive;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public event Action<bool>? ActiveChanged;

    public ForegroundWatcher(string processName)
    {
        _processName = processName.ToLower();
        _selfPid = Process.GetCurrentProcess().Id;
    }

    public void Start()
    {
        _timer = new Timer() { Interval = 500 };
        _timer.Tick += (EventHandler)((_1, _2) => Check());
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void Check()
    {
        bool flag = false;
        try
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow != IntPtr.Zero)
            {
                GetWindowThreadProcessId(foregroundWindow, out uint processId);
                if (processId != 0U)
                {
                    if ((int)processId == _selfPid)
                        return;
                    flag = Process.GetProcessById((int)processId).ProcessName.ToLower().Contains(_processName);
                }
            }
        }
        catch { }
        if (flag == _lastActive)
            return;
        _lastActive = flag;
        ActiveChanged?.Invoke(flag);
    }

    public bool IsActive => _lastActive;

    public bool CheckNow()
    {
        Check();
        return _lastActive;
    }
}
