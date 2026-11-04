using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace NoteDesk {
public static class ManagerChrome {
    [StructLayout(LayoutKind.Sequential)] struct PointNative {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo {public PointNative Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [StructLayout(LayoutKind.Sequential)] struct RectNative {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public RectNative Monitor,Work;public int Flags;}
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd,int flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    static IntPtr BoundsHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
        if(message==0x24) {
            var monitor=MonitorFromWindow(hwnd,2);var info=new MonitorInfo {Size=Marshal.SizeOf(typeof(MonitorInfo))};
            if(monitor!=IntPtr.Zero&&GetMonitorInfo(monitor,ref info)) {
                var limits=(MinMaxInfo)Marshal.PtrToStructure(lParam,typeof(MinMaxInfo));
                limits.MaxPosition.X=info.Work.Left-info.Monitor.Left;limits.MaxPosition.Y=info.Work.Top-info.Monitor.Top;
                limits.MaxSize.X=info.Work.Right-info.Work.Left;limits.MaxSize.Y=info.Work.Bottom-info.Work.Top;
                Marshal.StructureToPtr(limits,lParam,false);handled=true;
            }
        }
        return IntPtr.Zero;
    }
    public static UIElement Attach(Window window,WallpaperScene scene) {
        window.WindowStyle=WindowStyle.None;window.ResizeMode=ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(window,new WindowChrome {CaptionHeight=48,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
        window.SourceInitialized+=(s,e)=>{var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);if(source!=null)source.AddHook(BoundsHook);};
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var name=UI.Text("NoteDesk",12,UI.Ink);name.FontWeight=FontWeights.SemiBold;name.Margin=new Thickness(5,0,0,0);grid.Children.Add(name);
        var controls=UI.Row();Grid.SetColumn(controls,1);grid.Children.Add(controls);
        controls.Children.Add(CaptionButton("最小化","M1,6 L11,6",()=>SystemCommands.MinimizeWindow(window)));
        var maximize=CaptionButton("最大化／还原","M1,1 L11,1 11,11 1,11 Z",()=>window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized);
        controls.Children.Add(maximize);controls.Children.Add(CaptionButton("关闭窗口并驻留托盘","M1,1 L11,11 M11,1 L1,11",()=>window.Close()));
        window.StateChanged+=(s,e)=>{maximize.ToolTip=window.WindowState==WindowState.Maximized?"还原":"最大化";};
        return new GlassSurface(scene) {Content=grid,Radius=11,Padding=new Thickness(12,0,4,0),Margin=new Thickness(12,8,12,0),Height=34,VerticalAlignment=VerticalAlignment.Top};
    }
    static Button CaptionButton(string label,string geometry,Action action) {
        var button=UI.Button("",action);button.Width=38;button.Height=28;button.MinHeight=28;button.Padding=new Thickness(12,7,12,7);button.Margin=new Thickness(2,0,0,0);button.Background=Brushes.Transparent;button.ToolTip=label;
        button.Content=new Path {Data=Geometry.Parse(geometry),Stroke=UI.Ink,StrokeThickness=1.2,Stretch=Stretch.Uniform,Width=11,Height=11};
        System.Windows.Automation.AutomationProperties.SetName(button,label);WindowChrome.SetIsHitTestVisibleInChrome(button,true);return button;
    }
}
}
