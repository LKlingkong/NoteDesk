using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace NoteDesk {
/// <summary>Owns a native message-only window and its one transactional registration.</summary>
public sealed class GlobalHotkey:IDisposable {
    const int WmHotkey=0x0312, NoRepeat=0x4000;
    readonly HwndSource source;
    readonly Action pressed;
    int currentId, nextId=0x4000, modifiers, key;
    bool disposed;
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr h,int id,int modifiers,int key);
    [DllImport("user32.dll",SetLastError=true)] static extern bool UnregisterHotKey(IntPtr h,int id);
    public GlobalHotkey(Action pressed) {
        this.pressed=pressed;
        source=new HwndSource(new HwndSourceParameters("NoteDesk shortcuts") {ParentWindow=new IntPtr(-3),Width=0,Height=0,WindowStyle=0});
        source.AddHook(Receive);
    }
    public static bool Valid(int modifiers,int key) {
        return (modifiers&~15)==0 && (modifiers&11)!=0 && key>=0x08 && key<=0xfe && key!=0x7b
            && key!=0x10 && key!=0x11 && key!=0x12 && key!=0x5b && key!=0x5c && !(key>=0xa0&&key<=0xa5);
    }
    public bool TryConfigure(bool enabled,int requestedModifiers,int requestedKey,out string error) {
        error=null;
        if(disposed){error="快捷键服务已关闭。";return false;}
        if(!enabled){if(currentId!=0)UnregisterHotKey(source.Handle,currentId);currentId=0;return true;}
        if(!Valid(requestedModifiers,requestedKey)){error="请使用 Ctrl、Alt 或 Win 组合键；F12 为系统保留键。";return false;}
        if(currentId!=0 && modifiers==requestedModifiers && key==requestedKey)return true;
        int candidate=nextId++;if(nextId>=0xbfff)nextId=0x4000;
        if(!RegisterHotKey(source.Handle,candidate,requestedModifiers|NoRepeat,requestedKey)) {
            int code=Marshal.GetLastWin32Error();
            error=code==1409?"这组快捷键已被占用，请选择其他组合。":"无法启用这组快捷键，请选择其他组合。（"+code+"）";
            return false;
        }
        if(currentId!=0)UnregisterHotKey(source.Handle,currentId);
        currentId=candidate;modifiers=requestedModifiers;key=requestedKey;return true;
    }
    IntPtr Receive(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
        if(message==WmHotkey && !disposed && currentId!=0 && wParam.ToInt32()==currentId) {
            handled=true;
            if(pressed!=null)pressed();
        }
        return IntPtr.Zero;
    }
    public static string Label(int modifiers,int key) {
        string value="";
        if((modifiers&2)!=0)value+="Ctrl + ";if((modifiers&1)!=0)value+="Alt + ";
        if((modifiers&4)!=0)value+="Shift + ";if((modifiers&8)!=0)value+="Win + ";
        return value+KeyInterop.KeyFromVirtualKey(key).ToString();
    }
    public void Dispose() {
        if(disposed)return;
        if(currentId!=0)UnregisterHotKey(source.Handle,currentId);
        currentId=0;disposed=true;source.RemoveHook(Receive);source.Dispose();
    }
}
public static class NotePlacement {
    public static Rect Calculate(Rect workArea,Size desired,int sequence) {
        double width=Math.Min(desired.Width,workArea.Width),height=Math.Min(desired.Height,workArea.Height);
        int offset=Math.Abs(sequence%7);
        double left=workArea.Left+(workArea.Width-width)/2+offset*24;
        double top=workArea.Top+(workArea.Height-height)/2+offset*20;
        return new Rect(Math.Max(workArea.Left,Math.Min(left,workArea.Right-width)),Math.Max(workArea.Top,Math.Min(top,workArea.Bottom-height)),width,height);
    }
    public static Rect AtPointer(Window reference,Size desired,int sequence) {
        var screen=Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        var transform=System.Windows.Media.Matrix.Identity;
        var source=PresentationSource.FromVisual(reference);
        if(source!=null && source.CompositionTarget!=null)transform=source.CompositionTarget.TransformFromDevice;
        var start=transform.Transform(new Point(screen.Left,screen.Top));
        var end=transform.Transform(new Point(screen.Right,screen.Bottom));
        return Calculate(new Rect(start,end),desired,sequence);
    }
}
}
