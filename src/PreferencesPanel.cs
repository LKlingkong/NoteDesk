using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NoteDesk {
public sealed class PreferencesPanel:StackPanel {
    public PreferencesPanel(DeskApp app) {
        Margin=new Thickness(0,4,0,24);
        var appearance=UI.Text("外观",17,UI.Ink);appearance.FontWeight=FontWeights.SemiBold;Children.Add(appearance);
        var hint=UI.Text("选择浅色、深色，或跟随 Windows。",12,UI.Muted);hint.Margin=new Thickness(0,6,0,12);Children.Add(hint);
        var theme=new ComboBox {Width=210,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,24)};
        foreach(string label in new[]{"跟随系统","浅色","深色"})theme.Items.Add(label);
        theme.SelectedIndex=app.Store.Data.Appearance=="light"?1:app.Store.Data.Appearance=="dark"?2:0;
        System.Windows.Automation.AutomationProperties.SetName(theme,"外观主题");Children.Add(theme);
        theme.SelectionChanged+=(s,e)=>app.SetAppearance(theme.SelectedIndex==1?"light":theme.SelectedIndex==2?"dark":"system");
        var title=UI.Text("快捷新建",17,UI.Ink);title.FontWeight=FontWeights.SemiBold;Children.Add(title);
        var description=UI.Text("在其他软件中按下快捷键，即可创建便签并直接输入。",12,UI.Muted);description.Margin=new Thickness(0,6,0,12);Children.Add(description);
        var enabled=new CheckBox {Content="启用全局快捷键",IsChecked=app.Store.Data.HotkeyEnabled,Margin=new Thickness(0,0,0,12)};Children.Add(enabled);
        var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});Children.Add(row);
        var capture=new TextBox {IsReadOnly=true,Text=GlobalHotkey.Label(app.Store.Data.HotkeyModifiers,app.Store.Data.HotkeyKey),Margin=new Thickness(0,0,10,0),MinWidth=120};row.Children.Add(capture);
        System.Windows.Automation.AutomationProperties.SetName(capture,"新建便签快捷键");
        bool recording=false;
        var feedback=UI.Text(app.HotkeyError??"主窗口关闭后仍可使用；从托盘退出程序后失效。",12,app.HotkeyError==null?UI.Muted:UI.Hex("#B95656"));feedback.Margin=new Thickness(0,9,0,0);
        Action reset=()=>{recording=false;app.ResumeShortcut();capture.Text=GlobalHotkey.Label(app.Store.Data.HotkeyModifiers,app.Store.Data.HotkeyKey);};
        var change=UI.Button("更改",()=>{app.SuspendShortcut();recording=true;capture.Text="请按下组合键…";capture.Focus();Keyboard.Focus(capture);});change.Margin=new Thickness(0);Grid.SetColumn(change,1);row.Children.Add(change);Children.Add(feedback);
        enabled.Click+=(s,e)=>{string error;if(!app.ConfigureHotkey(enabled.IsChecked==true,app.Store.Data.HotkeyModifiers,app.Store.Data.HotkeyKey,out error)){enabled.IsChecked=app.Store.Data.HotkeyEnabled;feedback.Text=error;feedback.Foreground=UI.Hex("#B95656");}else{feedback.Text=enabled.IsChecked==true?"已启用，可在其他软件中直接新建便签。":"已关闭全局快捷键。";feedback.Foreground=UI.Muted;}};
        capture.LostKeyboardFocus+=(s,e)=>{if(recording)reset();};
        capture.PreviewKeyDown+=(s,e)=>{
            if(!recording)return;
            e.Handled=true;var key=e.Key==Key.System?e.SystemKey:e.Key;
            if(key==Key.Escape){reset();return;}
            if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift||key==Key.LWin||key==Key.RWin)return;
            var modifiers=Keyboard.Modifiers;int value=0;
            if((modifiers&ModifierKeys.Alt)!=0)value|=1;if((modifiers&ModifierKeys.Control)!=0)value|=2;
            if((modifiers&ModifierKeys.Shift)!=0)value|=4;if((modifiers&ModifierKeys.Windows)!=0)value|=8;
            int virtualKey=KeyInterop.VirtualKeyFromKey(key);string error;
            if(!app.ConfigureHotkey(app.Store.Data.HotkeyEnabled,value,virtualKey,out error)){reset();feedback.Text=error;feedback.Foreground=UI.Hex("#B95656");return;}
            reset();feedback.Text="快捷键已保存。";feedback.Foreground=UI.Muted;
        };
    }
}
}
