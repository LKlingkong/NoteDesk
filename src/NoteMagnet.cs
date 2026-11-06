using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NoteDesk
{
    internal sealed class NoteMagnet
    {
        private const int Moving = 0x0216;
        private const int EnterMove = 0x0231;
        private const int ExitMove = 0x0232;
        private readonly HwndSource source;
        private readonly List<NativeRect> windows = new List<NativeRect>();

        private NoteMagnet(HwndSource source)
        {
            this.source = source;
            source.AddHook(OnWindowMessage);
        }

        public static void Attach(Window window)
        {
            window.SourceInitialized += (sender, args) =>
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                if (source != null)
                    new NoteMagnet(source);
            };
        }

        private IntPtr OnWindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == EnterMove)
                CaptureWindows(handle);
            if (message == ExitMove)
                windows.Clear();
            if (message != Moving || lParam == IntPtr.Zero || (GetKeyState(0x12) & 0x8000) != 0)
                return IntPtr.Zero;

            var proposed = (NativeRect)Marshal.PtrToStructure(lParam, typeof(NativeRect));
            var monitor = MonitorFromRect(ref proposed, 2);
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
                return IntPtr.Zero;

            var dpi = source.CompositionTarget.TransformToDevice;
            int insetX = (int)Math.Round(NoteVisuals.PaperMargin * dpi.M11);
            int insetY = (int)Math.Round(NoteVisuals.PaperMargin * dpi.M22);
            int limitX = Math.Max(1, (int)Math.Round(12 * dpi.M11));
            int limitY = Math.Max(1, (int)Math.Round(12 * dpi.M22));
            var paper = new NativeRect
            {
                Left = proposed.Left + insetX,
                Right = proposed.Right - insetX,
                Top = proposed.Top + insetY,
                Bottom = proposed.Bottom - insetY
            };

            Vector offset = FindOffset(paper, info.Work, windows, limitX, limitY);
            int dx = (int)offset.X;
            int dy = (int)offset.Y;
            if (dx == 0 && dy == 0)
                return IntPtr.Zero;

            proposed.Left += dx;
            proposed.Right += dx;
            proposed.Top += dy;
            proposed.Bottom += dy;
            Marshal.StructureToPtr(proposed, lParam, false);
            handled = true;
            return new IntPtr(1);
        }

        internal static Vector FindOffset(NativeRect paper, NativeRect work,
            IEnumerable<NativeRect> visibleWindows, int limitX, int limitY)
        {
            int dx = limitX + 1;
            int dy = limitY + 1;
            Consider(work.Left - paper.Left, ref dx);
            Consider(work.Right - paper.Right, ref dx);
            Consider(work.Top - paper.Top, ref dy);
            Consider(work.Bottom - paper.Bottom, ref dy);
            foreach (var bounds in visibleWindows)
            {
                if (paper.Top < bounds.Bottom && paper.Bottom > bounds.Top)
                {
                    Consider(bounds.Left - paper.Right, ref dx);
                    Consider(bounds.Right - paper.Left, ref dx);
                    Consider(bounds.Left - paper.Left, ref dx);
                    Consider(bounds.Right - paper.Right, ref dx);
                }
                if (paper.Left < bounds.Right && paper.Right > bounds.Left)
                {
                    Consider(bounds.Top - paper.Bottom, ref dy);
                    Consider(bounds.Bottom - paper.Top, ref dy);
                    Consider(bounds.Top - paper.Top, ref dy);
                    Consider(bounds.Bottom - paper.Bottom, ref dy);
                }
            }

            if (Math.Abs(dx) > limitX) dx = 0;
            if (Math.Abs(dy) > limitY) dy = 0;
            return new Vector(dx, dy);
        }

        private static void Consider(int delta, ref int best)
        {
            if (Math.Abs(delta) < Math.Abs(best))
                best = delta;
        }

        private void CaptureWindows(IntPtr movingWindow)
        {
            windows.Clear();
            IntPtr shell = GetShellWindow();
            var noteInsets = new Dictionary<IntPtr, Vector>();
            var app = Application.Current as DeskApp;
            if (app != null)
            {
                foreach (var note in app.OpenNotes.Values)
                {
                    if (!note.IsVisible)
                        continue;
                    var handle = new WindowInteropHelper(note).Handle;
                    var target = HwndSource.FromHwnd(handle);
                    if (target != null && target.CompositionTarget != null)
                    {
                        var dpi = target.CompositionTarget.TransformToDevice;
                        noteInsets[handle] = new Vector(NoteVisuals.PaperMargin * dpi.M11,
                            NoteVisuals.PaperMargin * dpi.M22);
                    }
                }
            }
            EnumWindows((handle, data) =>
            {
                if (handle == movingWindow || handle == shell || !IsWindowVisible(handle) || IsIconic(handle))
                    return true;

                NativeRect bounds;
                if (!GetWindowRect(handle, out bounds))
                    return true;

                try
                {
                    int cloaked;
                    if (GetDwmInt(handle, 14, out cloaked, sizeof(int)) == 0 && cloaked != 0)
                        return true;
                    NativeRect visible;
                    if (GetDwmRect(handle, 9, out visible, Marshal.SizeOf(typeof(NativeRect))) == 0)
                        bounds = visible;
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }

                Vector inset;
                if (noteInsets.TryGetValue(handle, out inset))
                {
                    bounds.Left += (int)Math.Round(inset.X);
                    bounds.Right -= (int)Math.Round(inset.X);
                    bounds.Top += (int)Math.Round(inset.Y);
                    bounds.Bottom -= (int)Math.Round(inset.Y);
                }
                if (bounds.Right - bounds.Left >= 80 && bounds.Bottom - bounds.Top >= 60)
                    windows.Add(bounds);
                return true;
            }, IntPtr.Zero);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor, Work;
            public int Flags;
        }

        private delegate bool WindowCallback(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] private static extern short GetKeyState(int key);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int GetDwmRect(IntPtr window, int attribute, out NativeRect value, int size);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int GetDwmInt(IntPtr window, int attribute, out int value, int size);
    }
}
