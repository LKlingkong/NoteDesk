using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NoteDesk;

public static class NoteTransparencyTests {
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd,int index);
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
 static void Tint(NoteWindow note,double value){note.N.Alpha=value;typeof(NoteWindow).GetMethod("Tint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(note,null);}
 static BitmapSource Render(NoteWindow note){var content=(FrameworkElement)note.Content;var size=new Size(note.Width,note.Height);content.Measure(size);content.Arrange(new Rect(size));content.UpdateLayout();var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(note.Background,null,new Rect(size));var target=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);target.Render(drawing);target.Render(content);return target;}
 static byte Alpha(BitmapSource source,int x,int y){var bytes=new byte[4];source.CopyPixels(new Int32Rect(x,y,1,1),bytes,4,0);return bytes[3];}
 [STAThread] public static int Main(){DeskApp app=null;try {
  string root=Path.Combine(Path.GetTempPath(),"NoteDesk-opacity-"+Guid.NewGuid().ToString("N"));app=new DeskApp(root,true);app.Manager=new Manager(app);var model=new Note {Title="Opacity regression",Body="Text remains opaque",Visible=false};app.Store.Data.Notes.Add(model);var note=new NoteWindow(app,model);app.OpenNotes[model.Id]=note;
  Assert(note.AllowsTransparency&&note.WindowStyle==WindowStyle.None,"native note must support per-pixel desktop transparency");
  IntPtr handle=new WindowInteropHelper(note).EnsureHandle();long flags=IntPtr.Size==8?GetWindowLongPtr(handle,-20).ToInt64():GetWindowLong(handle,-20);Assert((flags&0x80000)!=0,"native HWND must carry WS_EX_LAYERED");
  var probe=new Border {Width=8,Height=8,Background=Brushes.Red,Margin=new Thickness(30,180,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};((Grid)note.Content).Children.Add(probe);
  foreach(string theme in new[]{"light","dark"}){
   UI.ApplyAppearance(theme);DesktopGlass.Apply(note);Assert(((SolidColorBrush)note.Background).Color.A==0,"theme/native material must not add an opaque window underlay");Assert(HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor.A==0,"native composition target remains clear");
   Tint(note,.25);var translucent=Render(note);int expected=SystemParameters.HighContrast?255:63;Assert(Math.Abs(Alpha(translucent,10,200)-expected)<=1,"rendered empty background follows requested opacity, theme="+theme);Assert(Alpha(translucent,33,183)==255,"foreground probe remains fully opaque");Assert(Alpha(translucent,0,0)==0,"outside rounded corners stays transparent");
   Tint(note,1);Assert(Alpha(Render(note),10,200)==255,"100 percent background is fully opaque");Assert(note.Opacity==1&&Field<TextBox>(note,"body").Opacity==1&&probe.Opacity==1,"window and text opacity never decrease");
  }
  var menu=(ContextMenu)typeof(NoteWindow).GetMethod("CreateMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(note,null);var container=menu.Items.OfType<MenuItem>().First(item=>item.Header is StackPanel&&((StackPanel)item.Header).Children.OfType<Slider>().Any());var slider=((StackPanel)container.Header).Children.OfType<Slider>().Single();Assert(container.StaysOpenOnClick,"slider host stays open during interaction");slider.Value=.37;Assert(Math.Abs(model.Alpha-.37)<.001,"slider updates actual note model");using(var input=new HwndSource(new HwndSourceParameters("Note opacity keyboard test") {ParentWindow=new IntPtr(-3),Width=1,Height=1,WindowStyle=0})){slider.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,input,0,Key.Right) {RoutedEvent=UIElement.PreviewKeyDownEvent});Assert(Math.Abs(model.Alpha-.38)<.001,"keyboard adjusts slider without closing menu");}menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));Assert(Math.Abs(new Store(root).Data.Notes[0].Alpha-.38)<.001,"menu close immediately persists opacity");
  Tint(note,double.NaN);Assert(Alpha(Render(note),10,200)>0,"invalid stored opacity has a visible fallback");app.Exiting=true;app.Shutdown();Console.WriteLine("PASS: native layered HWND, clear composition, light/dark pixel alpha, opaque foreground, rounded corners, slider host and persisted opacity");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);if(app!=null){app.Exiting=true;app.Shutdown();}return 1;}}
}
