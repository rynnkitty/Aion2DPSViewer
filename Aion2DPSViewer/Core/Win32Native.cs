using System;
using System.Runtime.InteropServices;

namespace Aion2DPSViewer.Core;

internal static class Win32Native
{
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_LAYERED = 0x080000;
    internal const int WS_EX_TOOLWINDOW = 0x80;
    internal const int WS_EX_NOACTIVATE = 0x08000000;
    internal const int WS_EX_TRANSPARENT = 0x20;
    internal const int WS_EX_COMPOSITED = 0x02000000;
    internal const uint LWA_ALPHA = 2;
    internal const uint SWP_NOZORDER = 4;
    internal const uint SWP_NOACTIVATE = 0x10;
    internal const uint SWP_NOREDRAW = 8;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    internal static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    internal static int GetWindowLong(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size != 8 ? GetWindowLong32(hWnd, nIndex) : (int)GetWindowLongPtr64(hWnd, nIndex);
    }

    internal static void SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hWnd, nIndex, (IntPtr)dwNewLong);
        else
            SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    internal static void ApplyLayered(IntPtr hWnd)
    {
        int dwNewLong = GetWindowLong(hWnd, -20) | 0x080000;
        SetWindowLong(hWnd, -20, dwNewLong);
        SetLayeredWindowAttributes(hWnd, 0U, byte.MaxValue, 2U);
    }

    internal static void SetExStyle(IntPtr hWnd, int style)
    {
        style |= 0x080000;
        SetWindowLong(hWnd, -20, style);
        SetLayeredWindowAttributes(hWnd, 0U, byte.MaxValue, 2U);
    }

    internal static void SetClickThrough(IntPtr hWnd, bool transparent)
    {
        int windowLong = GetWindowLong(hWnd, -20);
        bool flag = (windowLong & 0x20) != 0;
        if (transparent == flag)
            return;
        int style = !transparent ? windowLong & ~0x20 : windowLong | 0x20;
        SetExStyle(hWnd, style);
    }
}
