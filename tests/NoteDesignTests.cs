using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NoteDesk;

public static class NoteDesignTests {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
 static T Find<T>(DependencyObject root,string name) where T:DependencyObject {if(root is T&&System.Windows.Automation.AutomationProperties.GetName(root)==name)return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=Find<T>(VisualTreeHelper.GetChild(root,i),name);if(found!=null)return found;}return null;}
 static void Layout(NoteWindow note){var content=(FrameworkElement)note.Content;content.Measure(new Size(note.Width,note.Height));content.Arrange(new Rect(0,0,note.Width,note.Height));content.UpdateLayout();}
 static ContextMenu Menu(NoteWindow note){return (ContextMenu)typeof(NoteWindow).GetMethod("CreateMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(note,null);}
 [STAThread] public static int Main(){DeskApp app=null;try {
  var root=Path.Combine(Path.GetTempPath(),"NoteDesk-paper-test-"+Guid.NewGuid().ToString("N"));app=new DeskApp(root,true);typeof(DeskApp).GetField("Headless",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(app,true);app.Manager=new Manager(app);
  var model=new Note {Title="Paper test",Body="Preserve selected writing",Visible=false,Alpha=.4};model.Tasks.Add(new TaskItem {Text="Keyboard task"});model.Tasks.Add(new TaskItem {Text="Remaining task"});app.Store.Data.Notes.Add(model);var note=new NoteWindow(app,model);app.OpenNotes[model.Id]=note;Layout(note);
  var editor=Find<TextBox>((DependencyObject)note.Content,"便签正文");editor.Select(3,5);var pin=Find<Button>((DependencyObject)note.Content,"置顶便签");pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(model.Pinned&&note.Topmost,"pin changes actual window state");Assert(ReferenceEquals(editor,Find<TextBox>((DependencyObject)note.Content,"便签正文"))&&editor.SelectionStart==3&&editor.SelectionLength==5,"pin preserves editor and selection");
  var menu=Menu(note);var palette=menu.Items.OfType<MenuItem>().Select(item=>item.Header).OfType<StackPanel>().First(panel=>panel.Children.OfType<StackPanel>().Any());var chips=palette.Children.OfType<StackPanel>().First().Children.OfType<Button>().ToArray();Assert(chips.Length==5,"five named color choices");chips[3].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(model.Color==3&&Math.Abs(model.Alpha-.4)<.001,"color changes preserve opacity");Assert(ReferenceEquals(editor,Find<TextBox>((DependencyObject)note.Content,"便签正文"))&&editor.SelectionLength==5,"color changes preserve editing selection");
  var tasks=Field<StackPanel>(note,"tasks");var row=(Border)tasks.Children[0];Assert(row.Background==Brushes.Transparent&&row.BorderThickness.Bottom==1,"tasks use transparent rows and separators");var check=((Grid)row.Child).Children.OfType<Button>().First();Assert(check.Content is Grid,"completion is a vector control");
  using(var input=new HwndSource(new HwndSourceParameters("Note design keyboard test") {ParentWindow=new IntPtr(-3),Width=1,Height=1,WindowStyle=0})) {
   check.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,input,0,Key.Space) {RoutedEvent=UIElement.PreviewKeyDownEvent});check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(model.Tasks[0].Done&&tasks.Children.Count==1,"keyboard completion updates immediately");Assert(!row.HasAnimatedProperties,"keyboard completion has no fade animation");
   var add=Field<TextBox>(note,"addInput");Assert(!Find<Button>((DependencyObject)note.Content,"添加待办").IsEnabled,"blank add action disabled");add.Text="New task";Assert(Find<Button>((DependencyObject)note.Content,"添加待办").IsEnabled,"typed task enables add action");add.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,input,0,Key.Enter) {RoutedEvent=UIElement.KeyDownEvent});Assert(model.Tasks.Count==3&&model.Tasks[2].Text=="New task","Enter adds task");Assert(Field<TextBox>(note,"addInput").Text=="","composer clears after successful add");
  }
  var empty=new NoteWindow(app,new Note {Body="",Visible=false});Layout(empty);Assert(Field<ProgressBar>(empty,"progress").Visibility==Visibility.Collapsed&&Field<TextBlock>(empty,"taskHeading").Visibility==Visibility.Collapsed,"plain notes hide task heading and progress");
  app.Exiting=true;app.Shutdown();Console.WriteLine("PASS: pinned state, editor/caret preservation, color selection with opacity, vector tasks, immediate keyboard completion, add action and plain-note layout");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);if(app!=null){app.Exiting=true;app.Shutdown();}return 1;}}
}
