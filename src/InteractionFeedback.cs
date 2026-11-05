using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;

namespace NoteDesk {
// The established (0.23, 1, 0.32, 1) ease-out curve, evaluated without layout work.
public sealed class ResponseEase:IEasingFunction {
    public double Ease(double time) {
        if(time<=0)return 0;if(time>=1)return 1;
        double low=0,high=1,t=time;
        for(int i=0;i<22;i++) {
            double inverse=1-t;
            double x=3*inverse*inverse*t*.23+3*inverse*t*t*.32+t*t*t;
            if(x<time)low=t;else high=t;t=(low+high)/2;
        }
        return 1-Math.Pow(1-t,3);
    }
}
public static class InteractionFeedback {
    static bool installed;
    public static void Install() {
        if(installed)return;installed=true;
        EventManager.RegisterClassHandler(typeof(Button),UIElement.MouseEnterEvent,new MouseEventHandler((o,e)=>Hover((Button)o)),true);
        EventManager.RegisterClassHandler(typeof(Button),UIElement.LostMouseCaptureEvent,new MouseEventHandler((o,e)=>Release((Button)o)),true);
        EventManager.RegisterClassHandler(typeof(ContextMenu),ContextMenu.OpenedEvent,new RoutedEventHandler(MenuOpened));
    }
    static void Overlay(Button button,double target,bool animate) {
        if(VisualTreeHelper.GetChildrenCount(button)==0)return;
        var overlay=button.Template==null?null:button.Template.FindName("hover",button) as Border;
        if(overlay==null)return;
        if(animate&&UI.MotionAllowed)overlay.BeginAnimation(UIElement.OpacityProperty,UI.Motion(target,100),HandoffBehavior.SnapshotAndReplace);
        else {overlay.BeginAnimation(UIElement.OpacityProperty,null);overlay.Opacity=target;}
    }
    static void Hover(Button button) {if(Stylus.CurrentStylusDevice==null)Overlay(button,button.IsEnabled?(button.IsPressed?1:.55):0,true);}
    public static void Press(Button button) {
        Overlay(button,1,false);
        if(!UI.MotionAllowed||!button.IsEnabled)return;
        var scale=button.RenderTransform as ScaleTransform;
        if(scale==null){scale=new ScaleTransform(1,1);button.RenderTransform=scale;button.RenderTransformOrigin=new Point(.5,.5);}
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,UI.Motion(.97,100),HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,UI.Motion(.97,100),HandoffBehavior.SnapshotAndReplace);
    }
    public static void Release(Button button) {
        Overlay(button,button.IsMouseOver&&button.IsEnabled?.55:0,true);
        var scale=button.RenderTransform as ScaleTransform;if(scale==null)return;
        if(!UI.MotionAllowed||!button.IsEnabled){Reset(button);return;}
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,UI.Motion(1,120),HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,UI.Motion(1,120),HandoffBehavior.SnapshotAndReplace);
    }
    public static void Reset(Button button) {
        var scale=button.RenderTransform as ScaleTransform;
        if(scale!=null){scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);scale.ScaleX=scale.ScaleY=1;}
        Overlay(button,0,false);
    }
    public static void OpenMenu(ContextMenu menu,FrameworkElement anchor) {
        menu.PlacementTarget=anchor;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;
    }
    static void MenuOpened(object sender,RoutedEventArgs e) {
        var menu=(ContextMenu)sender;
        if(!UI.MotionAllowed||Stylus.CurrentStylusDevice!=null)return;
        bool above=false;
        try {if(menu.PlacementTarget!=null)above=menu.PointToScreen(new Point()).Y<menu.PlacementTarget.PointToScreen(new Point()).Y;}
        catch(InvalidOperationException){}
        menu.RenderTransformOrigin=new Point(0,above?1:0);
        var scale=new ScaleTransform(1,1);menu.RenderTransform=scale;
        var expand=UI.Motion(1,140);expand.From=.98;expand.FillBehavior=FillBehavior.Stop;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,expand);scale.BeginAnimation(ScaleTransform.ScaleYProperty,expand);
        var fade=UI.Motion(1,140);fade.From=.6;fade.FillBehavior=FillBehavior.Stop;menu.BeginAnimation(UIElement.OpacityProperty,fade);
    }
}
}
