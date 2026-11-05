using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;

namespace NoteDesk {
public static class AppearancePalette {
    sealed class ColorToken:INotifyPropertyChanged {
        Color value;
        public Color Value {get{return value;}set{this.value=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Value"));}}
        public event PropertyChangedEventHandler PropertyChanged;
    }
    static readonly Dictionary<string,SolidColorBrush> brushes=new Dictionary<string,SolidColorBrush>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string,ColorToken> tokens=new Dictionary<string,ColorToken>(StringComparer.OrdinalIgnoreCase);
    // Keys are the existing palette tokens, so stored note colors and old screens stay compatible.
    static readonly string[,] colors={
        {"#302F2C","#1D1D1F","#F5F5F7"},{"#706D65","#585860","#B4B4BA"},
        {"#596F60","#0066CC","#0A84FF"},{"#E5E1D8","#DCDDE3","#48484E"},
        {"#FAF9F6","#F5F5F7","#1C1C1E"},{"#FFFAF9F6","#F9F9FB","#29292D"},
        {"#EDEAE3","#E8E9EE","#3A3A40"},{"#F0EEE7","#80DDE5F0","#902A303B"},
        {"#DFE7DD","#D6E8FF","#163F68"},{"#344F3D","#0056AE","#A6D4FF"},
        {"#90FFFFFF","#CBFFFFFF","#C934343A"},{"#DCD8CE","#D4D6DE","#55555D"},
        {"#B8C7B8","#8FBDF0","#365F8E"},{"#17302F2C","#161D1D1F","#2AFFFFFF"},
        {"#B9B7AE","#A6A8B1","#74747D"},{"#22596F60","#280066CC","#380A84FF"},
        {"#B8FFFFFF","#A8FFFFFF","#803D4654"},{"#C0FFFFFF","#CBFFFFFF","#675E6674"},
        {"#45FFFFFF","#70FFFFFF","#552F3239"},{"#C66A63","#B63A34","#FF9991"},
        {"#B95656","#B42323","#FF9696"},
        {"#F5F1E7","#F9F6EB","#39362D"},{"#EDF2E9","#EDF5EE","#2B3932"},
        {"#F8EEE5","#FCF0EB","#40312E"},{"#EDF1F5","#EDF3FC","#2B3545"},
        {"#F4EEF4","#F5EFFA","#383043"},
        {"window-tint","#BFF5F5F7","#D41C1C1E"},{"primary-fill","#0066CC","#0066CC"},{"primary-ink","#FFFFFF","#FFFFFF"},
        {"toast-surface","#F2F5F5F7","#F229292D"},{"note-divider","#40585860","#50B4B4BA"},{"note-rim","#A0FFFFFF","#80666F82"}
    };
    public static bool IsDark {get;private set;}
    public static string Mode {get;private set;}
    public static SolidColorBrush Get(string key) {
        SolidColorBrush brush;
        if(brushes.TryGetValue(key,out brush))return brush;
        string value=key;
        for(int i=0;i<colors.GetLength(0);i++)if(string.Equals(colors[i,0],key,StringComparison.OrdinalIgnoreCase)){value=colors[i,IsDark?2:1];break;}
        var token=new ColorToken {Value=(Color)ColorConverter.ConvertFromString(value)};
        brush=new SolidColorBrush();
        // Binding keeps WPF resource sealing from freezing live palette brushes.
        BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding("Value") {Source=token});
        tokens[key]=token;brushes[key]=brush;
        if(Application.Current!=null)Application.Current.Resources[ResourceKey(key)]=brush;
        return brush;
    }
    public static string ResourceKey(string key){return "NoteDeskColor"+key.TrimStart('#');}
    public static SolidColorBrush ToastBrush(bool effectsAllowed){return Get(effectsAllowed?"toast-surface":"#FFFAF9F6");}
    public static void Apply(string mode) {
        Mode=mode=="light"||mode=="dark"?mode:"system";
        bool dark=false;
        if(Mode=="dark")dark=true;
        else if(Mode=="system")try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){dark=key!=null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0;}}catch{}
        IsDark=dark;
        for(int i=0;i<colors.GetLength(0);i++) {
            var brush=Get(colors[i,0]);
            tokens[colors[i,0]].Value=(Color)ColorConverter.ConvertFromString(colors[i,dark?2:1]);
            if(Application.Current!=null)Application.Current.Resources[ResourceKey(colors[i,0])]=brush;
        }
        if(SystemParameters.HighContrast) {
            tokens["#302F2C"].Value=SystemColors.WindowTextColor;
            tokens["#706D65"].Value=SystemColors.WindowTextColor;
            tokens["#FAF9F6"].Value=SystemColors.WindowColor;
            tokens["#596F60"].Value=SystemColors.HighlightColor;
            tokens["#FFFAF9F6"].Value=SystemColors.WindowColor;
            tokens["#90FFFFFF"].Value=SystemColors.WindowColor;
            tokens["#EDEAE3"].Value=SystemColors.ControlColor;
            tokens["#F0EEE7"].Value=SystemColors.WindowColor;
            tokens["#DFE7DD"].Value=SystemColors.HighlightColor;
            tokens["#344F3D"].Value=SystemColors.HighlightTextColor;
            tokens["primary-fill"].Value=SystemColors.HighlightColor;
            tokens["primary-ink"].Value=SystemColors.HighlightTextColor;
            tokens["note-divider"].Value=SystemColors.WindowTextColor;tokens["note-rim"].Value=SystemColors.WindowTextColor;
            for(int i=0;i<5;i++)tokens[colors[21+i,0]].Value=SystemColors.WindowColor;
        }
    }
}
public static class DesktopGlass {
    [StructLayout(LayoutKind.Sequential)] struct Margins {public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
    public static bool EffectsAllowed {
        get {
            if(SystemParameters.HighContrast)return false;
            try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key==null||Convert.ToInt32(key.GetValue("EnableTransparency",1))!=0;}catch{return false;}
        }
    }
    public static void Attach(Window window) {
        window.SourceInitialized+=(s,e)=>Apply(window);
    }
    public static void Apply(Window window) {
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==IntPtr.Zero)return;
        // A note owns its per-pixel surface. Any window tint or Acrylic underneath
        // would mask the opacity slider; never reapply it on theme changes.
        if(window is NoteWindow) {
            window.Background=Brushes.Transparent;
            var noteSource=HwndSource.FromHwnd(handle);
            if(noteSource!=null&&noteSource.CompositionTarget!=null)noteSource.CompositionTarget.BackgroundColor=Colors.Transparent;
            return;
        }
        // Layered toast windows use their own surface and cannot host a DWM backdrop.
        if(window.AllowsTransparency){var toast=window as UndoToast;var surface=window.Content as System.Windows.Controls.Border;if(toast!=null&&surface!=null)surface.Background=AppearancePalette.ToastBrush(EffectsAllowed);return;}
        bool glass=false;
        try {
            int dark=AppearancePalette.IsDark?1:0;
            DwmSetWindowAttribute(handle,20,ref dark,4);
            var caption=AppearancePalette.Get("#FAF9F6").Color;
            int captionColor=caption.R|(caption.G<<8)|(caption.B<<16);
            DwmSetWindowAttribute(handle,35,ref captionColor,4);
            var ink=AppearancePalette.Get("#302F2C").Color;
            int textColor=ink.R|(ink.G<<8)|(ink.B<<16);
            DwmSetWindowAttribute(handle,36,ref textColor,4);
            int rounded=2;DwmSetWindowAttribute(handle,33,ref rounded,4);
            int backdrop=window is Manager?1:EffectsAllowed?3:1;
            glass=DwmSetWindowAttribute(handle,38,ref backdrop,4)==0 && EffectsAllowed && !(window is Manager);
            var margins=new Margins {Left=glass?-1:0,Right=glass?-1:0,Top=glass?-1:0,Bottom=glass?-1:0};
            if(DwmExtendFrameIntoClientArea(handle,ref margins)!=0)glass=false;
            var source=HwndSource.FromHwnd(handle);
            if(source!=null&&source.CompositionTarget!=null)source.CompositionTarget.BackgroundColor=Colors.Transparent;
        }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        window.Background=AppearancePalette.Get(glass?"window-tint":"#FAF9F6");
    }
}
}
