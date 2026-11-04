using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NoteDesk;

public static class UpgradeTests {
    [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,int flags,UIntPtr extra);
    static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
    [STAThread] public static int Main(string[] args) {
        try {
            var appearance=typeof(Database).GetField("Appearance");
            Assert(appearance!=null,"legacy-compatible appearance default is missing");
            Assert((string)appearance.GetValue(new Database())=="system","new database follows system appearance");
            var legacy=Store.Json.Deserialize<Database>("{\"Schema\":1,\"Notes\":[],\"Sound\":true}");
            Assert((string)appearance.GetValue(legacy)=="system" && legacy.Sound,"legacy JSON retains defaults and existing fields");
            Assert((bool)typeof(Database).GetField("HotkeyEnabled").GetValue(legacy),"legacy hotkey enabled");
            Assert((int)typeof(Database).GetField("HotkeyModifiers").GetValue(legacy)==3,"legacy shortcut Ctrl+Alt");
            Assert((int)typeof(Database).GetField("HotkeyKey").GetValue(legacy)==0x4e,"legacy shortcut N");
            var placement=typeof(Note).Assembly.GetType("NoteDesk.NotePlacement");
            Assert(placement!=null,"monitor-aware placement missing");
            var calculate=placement.GetMethod("Calculate");
            var rect=(Rect)calculate.Invoke(null,new object[]{new Rect(-1920,-200,1920,1080),new Size(350,520),0});
            Assert(rect.Left>=-1920 && rect.Right<=0 && rect.Top>=-200 && rect.Bottom<=880,"negative-coordinate monitor bounds");
            var small=(Rect)calculate.Invoke(null,new object[]{new Rect(0,0,320,400),new Size(350,520),6});
            Assert(small.Width<=320 && small.Height<=400 && small.Right<=320 && small.Bottom<=400,"small work area bounds");
            var hotkeyType=typeof(Note).Assembly.GetType("NoteDesk.GlobalHotkey");
            Assert(hotkeyType!=null,"global hotkey lifecycle missing");
            var configure=hotkeyType.GetMethod("TryConfigure");
            var first=Activator.CreateInstance(hotkeyType,new object[]{(Action)(()=>{})});
            var second=Activator.CreateInstance(hotkeyType,new object[]{(Action)(()=>{})});
            try {
                var a=new object[]{true,3,0x77,null}; // Ctrl+Alt+F8, isolated from product default
                Assert((bool)configure.Invoke(first,a),"register test shortcut: "+a[3]);
                var b=new object[]{true,3,0x77,null};
                Assert(!(bool)configure.Invoke(second,b) && !string.IsNullOrEmpty((string)b[3]),"conflict is reported");
                a=new object[]{true,3,0x78,null};
                Assert((bool)configure.Invoke(first,a),"rebind test shortcut");
                b=new object[]{true,3,0x77,null};
                Assert((bool)configure.Invoke(second,b),"rebind releases old shortcut");
                a=new object[]{true,3,0x77,null};
                Assert(!(bool)configure.Invoke(first,a),"conflicting rebind rejected");
                var third=Activator.CreateInstance(hotkeyType,new object[]{(Action)(()=>{})});
                try {var c=new object[]{true,3,0x78,null};Assert(!(bool)configure.Invoke(third,c),"failed rebind keeps working shortcut");}
                finally {((IDisposable)third).Dispose();}
                a=new object[]{false,3,0x78,null};
                Assert((bool)configure.Invoke(first,a),"disable shortcut");
                a=new object[]{true,0,0x4e,null};
                Assert(!(bool)configure.Invoke(first,a),"reject unmodified typing key");
                a=new object[]{true,3,0x7b,null};
                Assert(!(bool)configure.Invoke(first,a),"reject reserved F12");
            } finally {((IDisposable)first).Dispose();((IDisposable)second).Dispose();}
            var after=Activator.CreateInstance(hotkeyType,new object[]{(Action)(()=>{})});
            try {var c=new object[]{true,3,0x77,null};Assert((bool)configure.Invoke(after,c),"dispose releases registration");}
            finally {((IDisposable)after).Dispose();}
            var apply=typeof(UI).GetMethod("ApplyAppearance");
            Assert(apply!=null,"live appearance switching is missing");
            var app=new DeskApp(Path.Combine(Path.GetTempPath(),"NoteDesk-theme-test-"+Guid.NewGuid().ToString("N")),!args.Contains("--desktop"));
            apply.Invoke(null,new object[]{"dark"});
            Assert((bool)typeof(UI).GetProperty("IsDark").GetValue(null,null),"dark appearance resolves");
            var darkInk=((System.Windows.Media.SolidColorBrush)UI.Ink).Color;
            Assert(darkInk.R>220,"dark text stays legible");
            apply.Invoke(null,new object[]{"light"});
            Assert(!(bool)typeof(UI).GetProperty("IsDark").GetValue(null,null),"light appearance resolves");
            Assert(((System.Windows.Media.SolidColorBrush)UI.Ink).Color.R<60,"existing text brush updates live");
            var failures=new System.Collections.Generic.List<string>();
            var tiny=new NoteWindow(app,new Note {W=320,H=336,Visible=false});tiny.ShowActivated=false;tiny.Show();tiny.UpdateLayout();
            if(tiny.ActualHeight>337||tiny.ActualWidth>321)failures.Add("actual small note exceeds work-area bounds");tiny.Flush();tiny.Hide();
            var toastBrush=typeof(AppearancePalette).GetMethod("ToastBrush");
            if(toastBrush==null || ((System.Windows.Media.SolidColorBrush)toastBrush.Invoke(null,new object[]{false})).Color.A!=255)failures.Add("undo toast needs opaque fallback when effects disabled");
            var option=new ComboBoxItem {Content="深色",IsSelected=true};option.Measure(new Size(200,40));option.Arrange(new Rect(0,0,200,40));option.ApplyTemplate();
            if(option.Foreground!=UI.Hex("#344F3D"))failures.Add("selected combo option needs matching highlight foreground");
            Assert(failures.Count==0,string.Join("; ",failures.ToArray()));
            if(args.Contains("--desktop"))return DesktopFlow(app);
            app.Shutdown();
            Console.WriteLine("PASS: legacy settings, native hotkey conflict/rebind/disable/disposal, live theme, actual small-window bounds, opaque toast fallback, selected option contrast");
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine("FAIL: "+ex.Message);return 1; }
    }
    static void Key(byte key,bool up){keybd_event(key,0,up?2:0,UIntPtr.Zero);}
    static void Press(byte key){Key(0x11,false);Key(0x12,false);Key(key,false);Key(key,true);Key(0x12,true);Key(0x11,true);}
    static Button FindButton(DependencyObject parent,string label){var button=parent as Button;if(button!=null&&button.Content as string==label)return button;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++){var found=FindButton(System.Windows.Media.VisualTreeHelper.GetChild(parent,i),label);if(found!=null)return found;}return null;}
    static int DesktopFlow(DeskApp app) {
        app.Start();string error;
        Console.WriteLine("Native backdrop: effects="+DesktopGlass.EffectsAllowed+", background alpha="+((System.Windows.Media.SolidColorBrush)app.Manager.Background).Color.A);
        Assert(app.ConfigureHotkey(true,3,0x77,out error),"configure desktop test shortcut: "+error);
        app.Manager.Hide();
        var unrelated=new Window {Title="NoteDesk isolated shortcut test",Width=320,Height=140,Content=new TextBox {Text="Keyboard target"},WindowStartupLocation=WindowStartupLocation.CenterScreen};
        unrelated.Show();unrelated.Activate();
        int step=0,result=1;Note firstNote=null;NoteWindow firstWindow=null;TextBox firstEditor=null;
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(350)};
        timer.Tick+=(s,e)=>{
            try {
                if(step==0){Press(0x77);}
                else if(step==1){
                    Assert(app.Store.Data.Notes.Count==1,"real global shortcut creates exactly one note");
                    firstNote=app.Store.Data.Notes[0];firstWindow=app.OpenNotes[firstNote.Id];
                    Assert(firstNote.Body==""&&firstNote.Tasks.Count==0,"shortcut creates blank note");
                    firstEditor=Keyboard.FocusedElement as TextBox;
                    Assert(firstEditor!=null&&System.Windows.Automation.AutomationProperties.GetName(firstEditor)=="便签正文","shortcut focuses body editor");
                    Assert(!firstWindow.Topmost&&!app.Manager.IsVisible,"new note not pinned and manager remains hidden");
                    firstEditor.Text="未保存的输入仍在";int caret=firstEditor.CaretIndex=4;
                    app.SetAppearance("dark");
                    Assert(object.ReferenceEquals(firstEditor,Keyboard.FocusedElement)&&firstEditor.Text=="未保存的输入仍在"&&firstEditor.CaretIndex==caret,"theme preserves editor text, caret and focus");
                    Key(0x11,false);Key(0x12,false);Key(0x77,false);Key(0x77,false);
                }
                else if(step==2){
                    Assert(app.Store.Data.Notes.Count==2,"held key does not duplicate notes");
                    Key(0x77,true);Key(0x12,true);Key(0x11,true);
                    Assert(app.ConfigureHotkey(true,3,0x78,out error),"runtime shortcut rebind");Press(0x77);
                }
                else if(step==3){Assert(app.Store.Data.Notes.Count==2,"old shortcut released");Press(0x78);}
                else if(step==4){
                    Assert(app.Store.Data.Notes.Count==3,"new shortcut active");
                    Assert(app.ConfigureHotkey(false,3,0x78,out error),"runtime disable");Press(0x78);
                }
                else if(step==5){
                    Assert(app.Store.Data.Notes.Count==3,"disabled shortcut creates nothing");
                    firstWindow.Flush();
                    Assert(new Store(app.Store.Root).Data.Notes.First(n=>n.Id==firstNote.Id).Body=="未保存的输入仍在","theme switch keeps persisted content");
                    Assert(app.ConfigureHotkey(true,3,0x77,out error),"enable shortcut before recording");
                    app.Manager.Show();app.Manager.Activate();app.Manager.PreviewSection("settings");app.Manager.UpdateLayout();
                    var change=FindButton((DependencyObject)app.Manager.Content,"更改");Assert(change!=null,"shortcut recorder button exists");change.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Press(0x77);
                }
                else if(step==6){
                    Assert(app.Store.Data.Notes.Count==3,"recording existing shortcut must not create an accidental note");
                    Press(0x77);
                }
                else if(step==7){
                    Assert(app.Store.Data.Notes.Count==4,"recorded shortcut restored after capture");
                    Console.WriteLine("PASS: real key input, blank note/body focus, held key, hidden manager, live theme/caret, runtime rebind/disable, shortcut recorder, persistence");
                    result=0;timer.Stop();unrelated.Close();app.Quit();
                }
                step++;
            }catch(Exception ex){Console.Error.WriteLine("FAIL desktop step "+step+": "+ex);timer.Stop();unrelated.Close();app.Quit();}
        };
        timer.Start();app.Run();
        Key(0x77,true);Key(0x78,true);Key(0x12,true);Key(0x11,true);
        return result;
    }
}
