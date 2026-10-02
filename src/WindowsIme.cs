using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GWYF.TextChat
{
    // Unity's new-input-only player can leave the native IME context detached.
    // Restore it only for this game's focused window, never for another application.
    internal static class WindowsIme
    {
        private static IntPtr window;
        private static bool savedOpen, saved;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        [DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr hwnd);
        [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr context);
        [DllImport("imm32.dll")] private static extern bool ImmAssociateContextEx(IntPtr hwnd, IntPtr context, uint flags);
        [DllImport("imm32.dll")] private static extern bool ImmGetOpenStatus(IntPtr context);
        [DllImport("imm32.dll")] private static extern bool ImmSetOpenStatus(IntPtr context, bool open);

        internal static void Begin()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            IntPtr candidate = GetForegroundWindow();
            GetWindowThreadProcessId(candidate, out uint process);
            if (candidate == IntPtr.Zero || process != GetCurrentProcessId()) return;
            window = candidate;
            IntPtr context = ImmGetContext(window);
            if (context == IntPtr.Zero)
            {
                ImmAssociateContextEx(window, IntPtr.Zero, 0x10); // IACE_DEFAULT
                context = ImmGetContext(window);
            }
            if (context == IntPtr.Zero) return;
            if (!saved) { savedOpen = ImmGetOpenStatus(context); saved = true; }
            ImmReleaseContext(window, context);
        }

        internal static void End()
        {
            if (!saved || window == IntPtr.Zero) return;
            IntPtr context = ImmGetContext(window);
            if (context != IntPtr.Zero)
            {
                ImmSetOpenStatus(context, savedOpen);
                ImmReleaseContext(window, context);
            }
            saved = false;
            window = IntPtr.Zero;
        }
    }
}
