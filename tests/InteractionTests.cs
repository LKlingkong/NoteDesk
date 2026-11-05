using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NoteDesk;

public static class InteractionTests {
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
 static T Find<T>(DependencyObject parent,string name) where T:DependencyObject {if(parent is T&&System.Windows.Automation.AutomationProperties.GetName(parent)==name)return (T)parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var result=Find<T>(VisualTreeHelper.GetChild(parent,i),name);if(result!=null)return result;}return null;}
 static void Layout(Manager manager){var scene=(WallpaperScene)manager.Content;scene.Prepare(new Size(manager.Width,manager.Height));scene.Measure(new Size(manager.Width,manager.Height));scene.Arrange(new Rect(0,0,manager.Width,manager.Height));scene.UpdateLayout();}
 static bool ContainsText(DependencyObject parent,string text){var block=parent as TextBlock;if(block!=null&&block.Text==text)return true;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)if(ContainsText(VisualTreeHelper.GetChild(parent,i),text))return true;return false;}
 static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 [STAThread] public static int Main(){DeskApp app=null;try {
  var ease=new ResponseEase();Assert(ease.Ease(0)==0&&ease.Ease(1)==1,"easing exact endpoints");double previous=0;for(int i=1;i<=100;i++){double value=ease.Ease(i/100.0);Assert(value>=previous&&value<=1,"easing is monotonic without overshoot");previous=value;}Assert(ease.Ease(.5)>.9,"responsive ease-out has prompt initial response");
  var root=Path.Combine(Path.GetTempPath(),"NoteDesk-interaction-"+Guid.NewGuid().ToString("N"));app=new DeskApp(root,true);app.Store.Data.Notes.Add(new Note {Title="alpha",Visible=false});app.Store.Data.Notes.Add(new Note {Title="beta",Visible=false});app.Manager=new Manager(app);var manager=app.Manager;Layout(manager);
  var list=Field<StackPanel>(manager,"list");var first=list.Children[0];var navigation=Field<System.Collections.Generic.Dictionary<string,Button>>(manager,"navigation");navigation["active"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(ReferenceEquals(first,list.Children[0]),"selected navigation does not rebuild content");
  manager.PreviewSection("settings");Layout(manager);var theme=Find<ComboBox>((DependencyObject)manager.Content,"外观主题");Assert(theme!=null,"theme selector exists");theme.SelectedIndex=2;Layout(manager);Assert(ReferenceEquals(theme,Find<ComboBox>((DependencyObject)manager.Content,"外观主题")),"theme switch preserves settings control identity");Assert(app.Store.Data.Appearance=="dark","theme choice persisted");
  ApplicationCommands.Find.Execute(null,manager);Assert(Field<string>(manager,"section")=="active","Ctrl+F command opens searchable section");Layout(manager);var search=Field<TextBox>(manager,"search");search.Text="alpha";Assert(list.Children.Count==2,"search coalesces changes before rebuilding");search.Text="no-match";search.Text="beta";Pump(220);Assert(list.Children.Count==1&&ContainsText(list,"beta"),"search renders only the most recent query");
  using(var inputSource=new HwndSource(new HwndSourceParameters("NoteDesk interaction tests") {ParentWindow=new IntPtr(-3),Width=1,Height=1,WindowStyle=0})) {
   search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,inputSource,0,Key.Escape) {RoutedEvent=UIElement.PreviewKeyDownEvent});Assert(search.Text==""&&list.Children.Count==2,"Escape clears search immediately");Assert(!UI.MotionAllowed,"keyboard input suppresses movement");
   var button=UI.Button("feedback",()=>{});button.Style=(Style)app.FindResource(typeof(Button));button.ApplyTemplate();button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseDownEvent});InteractionFeedback.Press(button);button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.LostMouseCaptureEvent});Pump(160);var released=button.RenderTransform as ScaleTransform;Assert(released==null||Math.Abs(released.ScaleX-1)<.001,"lost capture restores button scale");InteractionFeedback.Press(button);button.IsEnabled=false;var scale=button.RenderTransform as ScaleTransform;Assert(scale==null||(scale.ScaleX==1&&scale.ScaleY==1&&!scale.HasAnimatedProperties),"disabling during press resets scale and animations");
  }
  var dialog=new TaskDialog(new TaskItem {Text="Reminder"},null);var content=(FrameworkElement)dialog.Content;content.Measure(new Size(460,570));content.Arrange(new Rect(0,0,460,570));content.UpdateLayout();var date=Find<DatePicker>(content,"截止日期");Assert(date!=null&&!date.IsEnabled,"reminder date is disabled without reminder toggle");var toggle=((Panel)dialog.Content).Children.OfType<CheckBox>().First();toggle.IsChecked=true;Assert(date.IsEnabled,"reminder date responds to toggle");Assert(Find<Button>(content,"保存设置").IsDefault&&Find<Button>(content,"取消").IsCancel,"reminder form supports Enter and Escape");
  app.Shutdown();Console.WriteLine("PASS: easing, same-page navigation, preserved theme controls, search debounce, find command, Escape, keyboard motion suppression capture/disable reset and reminder keyboard actions");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);if(app!=null)app.Shutdown();return 1;}}
}
