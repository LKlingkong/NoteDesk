using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace NoteDesk {
public sealed class WallpaperPreferences:StackPanel {
    readonly DeskApp app;
    public WallpaperPreferences(DeskApp app){this.app=app;Margin=new Thickness(0,0,0,28);Build();}
    public static void ResetDefaults(Database data) {data.WallpaperPath="";data.GlassBlur=24;data.GlassOpacity=.86;data.WallpaperBrightness=.85;data.WallpaperX=.5;data.WallpaperY=1;}
    void Build() {
        Children.Clear();
        var title=UI.Text("壁纸与玻璃",17,UI.Ink);title.FontWeight=FontWeights.SemiBold;Children.Add(title);
        var explanation=UI.Text("管理窗口使用壁纸；桌面便签保持桌面磨砂。",12,UI.Muted);explanation.Margin=new Thickness(0,6,0,12);Children.Add(explanation);
        if(!DesktopGlass.EffectsAllowed){var fallback=UI.Text("系统已关闭透明效果，浮层暂用实色；调节参数仍会保存。",12,UI.Muted);fallback.Margin=new Thickness(0,0,0,12);Children.Add(fallback);}
        string warning;var bitmap=WallpaperFiles.Load(app.Store.Root,app.Store.Data.WallpaperPath,out warning);
        var preview=new Border {Height=105,CornerRadius=new CornerRadius(10),ClipToBounds=true,Background=new ImageBrush(bitmap) {Stretch=Stretch.UniformToFill,AlignmentY=AlignmentY.Bottom},Margin=new Thickness(0,0,0,12)};Children.Add(preview);
        var actions=new WrapPanel();var feedback=UI.Text(warning??"",12,UI.Hex("#B95656"));feedback.Margin=new Thickness(0,6,0,4);
        actions.Children.Add(UI.Button("更换壁纸",()=>{
            var dialog=new OpenFileDialog {Filter="图片 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",Title="选择壁纸"};
            if(dialog.ShowDialog(app.Manager)!=true)return;
            string relative,error;if(!WallpaperFiles.Import(app.Store.Root,dialog.FileName,out relative,out error)){feedback.Text=error;return;}
            app.Store.Data.WallpaperPath=relative;app.UpdateWallpaper();Build();
        }));
        actions.Children.Add(UI.Button("恢复默认",()=>{ResetDefaults(app.Store.Data);app.UpdateWallpaper();Build();}));Children.Add(actions);Children.Add(feedback);
        AddSlider(this,"模糊程度",0,48,app.Store.Data.GlassBlur,1,value=>value.ToString("0")+" px",value=>app.Store.Data.GlassBlur=value);
        AddSlider(this,"浮层不透明度",.5,.96,app.Store.Data.GlassOpacity,.01,value=>value.ToString("P0"),value=>app.Store.Data.GlassOpacity=value);
        AddSlider(this,"壁纸明暗",.4,1.2,app.Store.Data.WallpaperBrightness,.01,value=>value.ToString("P0"),value=>app.Store.Data.WallpaperBrightness=value);
        var position=new StackPanel {Margin=new Thickness(0,8,0,0)};
        AddSlider(position,"左右位置",0,1,app.Store.Data.WallpaperX,.01,value=>value.ToString("P0"),value=>app.Store.Data.WallpaperX=value);
        AddSlider(position,"上下位置",0,1,app.Store.Data.WallpaperY,.01,value=>value.ToString("P0"),value=>app.Store.Data.WallpaperY=value);
        position.Children.Add(UI.Text("画面超出窗口时，可调整露出的区域。",11,UI.Muted));
        Children.Add(new Expander {Header="调整壁纸位置",Content=position,Foreground=UI.Ink,Margin=new Thickness(0,10,0,0)});
        var saved=UI.Text("调整会自动保存。自定义图片保存在本机数据目录。",11,UI.Muted);saved.Margin=new Thickness(0,12,0,0);Children.Add(saved);
    }
    void AddSlider(Panel panel,string label,double minimum,double maximum,double value,double step,Func<double,string> format,Action<double> set) {
        var heading=new Grid {Margin=new Thickness(0,12,0,5)};heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        heading.Children.Add(UI.Text(label,12,UI.Ink));var amount=UI.Text(format(WallpaperGeometry.Limit(value,minimum,maximum,minimum)),12,UI.Muted);Grid.SetColumn(amount,1);heading.Children.Add(amount);panel.Children.Add(heading);
        var slider=new Slider {Minimum=minimum,Maximum=maximum,Value=WallpaperGeometry.Limit(value,minimum,maximum,minimum),TickFrequency=step,SmallChange=step,LargeChange=step*5,IsSnapToTickEnabled=true,Height=22};
        System.Windows.Automation.AutomationProperties.SetName(slider,label);panel.Children.Add(slider);
        slider.ValueChanged+=(s,e)=>{set(slider.Value);amount.Text=format(slider.Value);app.UpdateWallpaper();};
    }
}
}
