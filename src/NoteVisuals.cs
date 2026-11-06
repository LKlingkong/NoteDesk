using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace NoteDesk {
public static class NoteVisuals {
 public const double PaperMargin = 8;
 public static Path Icon(string name,double size=15) {
  string data=name=="pin"?"M6,2 L12,2 M7,2 L7,7 4,10 4,12 14,12 14,10 11,7 11,2 M9,12 L9,17":name=="close"?"M4,4 L14,14 M14,4 L4,14":name=="plus"?"M9,3 L9,15 M3,9 L15,9":name=="check"?"M3,9 L7,13 15,4":"M3,9 L3.1,9 M9,9 L9.1,9 M15,9 L15.1,9";
  var icon=new Path {Data=Geometry.Parse(data),Width=size,Height=size,Stretch=Stretch.Uniform,StrokeThickness=name=="more"?2.5:1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false};
  BindingOperations.SetBinding(icon,Path.StrokeProperty,new Binding("Foreground") {RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Button),1)});return icon;
 }
 public static Button Tool(string name,string label,Action action) {
  var button=UI.Button("",action);button.Content=Icon(name);button.Width=28;button.Height=28;button.MinHeight=28;button.Padding=new Thickness(6);button.Margin=new Thickness(3,0,0,0);button.Background=Brushes.Transparent;button.ToolTip=label;System.Windows.Automation.AutomationProperties.SetName(button,label);return button;
 }
 public static void PinState(Button button,bool pinned) {button.Background=pinned?UI.Hex("#DFE7DD"):Brushes.Transparent;button.Foreground=pinned?UI.Accent:UI.Muted;button.ToolTip=pinned?"取消置顶":"置顶此便签";}
 public static Button Completion(bool done) {
  var button=UI.Button("",()=>{});button.Tag="complete-task";button.Width=30;button.MinHeight=30;button.Height=30;button.Padding=new Thickness(5);button.Margin=new Thickness(0,0,9,0);button.Background=Brushes.Transparent;SetChecked(button,done);return button;
 }
 public static void SetChecked(Button button,bool done) {
  var grid=new Grid {Width=18,Height=18,IsHitTestVisible=false};grid.Children.Add(new Ellipse {Stroke=done?UI.Accent:UI.Muted,StrokeThickness=1.25,Fill=done?UI.Accent:Brushes.Transparent});
  if(done)grid.Children.Add(new Path {Data=Geometry.Parse("M4,9 L7.5,12 13.5,5.5"),Stroke=UI.Hex("primary-ink"),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});button.Content=grid;
 }
 public static void SwatchState(Button button,bool selected){button.BorderBrush=selected?UI.Accent:Brushes.Transparent;button.Content=selected?Icon("check",13):null;}
}
// Shadow is clipped to the space outside the paper. It cannot darken the alpha-controlled body.
public sealed class NoteShadow:Grid {
 public NoteShadow(){IsHitTestVisible=false;Children.Add(new Border {Margin=new Thickness(NoteVisuals.PaperMargin),CornerRadius=new CornerRadius(16),Background=Brushes.Black,Effect=new DropShadowEffect {BlurRadius=14,ShadowDepth=2,Opacity=.14,Color=Color.FromRgb(16,24,40)}});}
 protected override Size ArrangeOverride(Size size) {
  double margin = NoteVisuals.PaperMargin;
  var outside = new RectangleGeometry(new Rect(size), 16, 16);
  var inside = new RectangleGeometry(new Rect(margin, margin, Math.Max(0, size.Width - 2 * margin),
      Math.Max(0, size.Height - 2 * margin)), 16, 16);
  Clip = new CombinedGeometry(GeometryCombineMode.Exclude, outside, inside);
  return base.ArrangeOverride(size);
 }
}
}
