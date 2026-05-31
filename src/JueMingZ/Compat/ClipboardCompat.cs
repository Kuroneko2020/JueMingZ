using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace JueMingZ.Compat
{
    public static class ClipboardCompat
    {
        private const uint GmemMoveable = 0x0002;
        private const uint CfUnicodeText = 13;

        public static bool TrySetText(string text, out string detail)
        {
            detail = string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                detail = "empty text";
                return false;
            }

            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (TrySetTextOnce(text, out detail))
                {
                    return true;
                }

                Thread.Sleep(20);
            }

            return false;
        }

        private static bool TrySetTextOnce(string text, out string detail)
        {
            detail = string.Empty;
            var clipboardOpened = false;
            var handle = IntPtr.Zero;
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    detail = "OpenClipboard failed: " + Marshal.GetLastWin32Error().ToString();
                    return false;
                }

                clipboardOpened = true;
                if (!EmptyClipboard())
                {
                    detail = "EmptyClipboard failed: " + Marshal.GetLastWin32Error().ToString();
                    return false;
                }

                var bytes = Encoding.Unicode.GetBytes(text + "\0");
                handle = GlobalAlloc(GmemMoveable, new UIntPtr((uint)bytes.Length));
                if (handle == IntPtr.Zero)
                {
                    detail = "GlobalAlloc failed: " + Marshal.GetLastWin32Error().ToString();
                    return false;
                }

                var target = GlobalLock(handle);
                if (target == IntPtr.Zero)
                {
                    detail = "GlobalLock failed: " + Marshal.GetLastWin32Error().ToString();
                    GlobalFree(handle);
                    handle = IntPtr.Zero;
                    return false;
                }

                try
                {
                    Marshal.Copy(bytes, 0, target, bytes.Length);
                }
                finally
                {
                    GlobalUnlock(handle);
                }

                if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
                {
                    detail = "SetClipboardData failed: " + Marshal.GetLastWin32Error().ToString();
                    GlobalFree(handle);
                    handle = IntPtr.Zero;
                    return false;
                }

                handle = IntPtr.Zero;
                detail = "copied";
                return true;
            }
            catch (Exception error)
            {
                detail = error.GetType().Name + ": " + error.Message;
                if (handle != IntPtr.Zero)
                {
                    GlobalFree(handle);
                    handle = IntPtr.Zero;
                }

                return false;
            }
            finally
            {
                if (clipboardOpened)
                {
                    CloseClipboard();
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);
    }
}
