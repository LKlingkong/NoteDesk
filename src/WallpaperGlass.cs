using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace NoteDesk {
public static class WallpaperGeometry {
    public static double Limit(double value,double min,double max,double fallback) {
        return double.IsNaN(value)||double.IsInfinity(value)?fallback:Math.Max(min,Math.Min(max,value));
    }
    public static Rect Cover(Size image,Size viewport,double x,double y) {
        if(image.Width<=0||image.Height<=0||viewport.Width<=0||viewport.Height<=0)return Rect.Empty;
        double scale=Math.Max(viewport.Width/image.Width,viewport.Height/image.Height);
        double width=image.Width*scale,height=image.Height*scale;
        return new Rect((viewport.Width-width)*Limit(x,0,1,.5),(viewport.Height-height)*Limit(y,0,1,1),width,height);
    }
}
public static class WallpaperFiles {
    const int MaxBytes=32*1024*1024;
    static BitmapSource embedded;
    static bool ImageError(Exception ex){return ex is InvalidDataException||ex is IOException||ex is UnauthorizedAccessException||ex is NotSupportedException||ex is FormatException||ex is ArgumentException||ex is System.Runtime.InteropServices.COMException;}
    static byte[] ReadImage(string path) {
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
            if(input.Length>MaxBytes)throw new InvalidDataException("图片文件过大，请选择 32 MB 以内的图片。");
            var bytes=new byte[(int)input.Length];int offset=0;
            while(offset<bytes.Length){int count=input.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
            return bytes;
        }
    }
    static BitmapSource Decode(byte[] bytes) {
        using(var stream=new MemoryStream(bytes,false)) {
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
            if(decoder.Frames.Count==0)throw new InvalidDataException("图片无法读取。");
            var frame=decoder.Frames[0];
            if(frame.PixelWidth<=0||frame.PixelHeight<=0||frame.PixelWidth>32768||frame.PixelHeight>32768||(long)frame.PixelWidth*frame.PixelHeight>40000000)throw new InvalidDataException("图片尺寸过大，请选择 4000 万像素以内、边长不超过 32768 的图片。");
            stream.Position=0;
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
            image.StreamSource=stream;image.DecodePixelWidth=Math.Min(4096,frame.PixelWidth);image.EndInit();image.Freeze();return image;
        }
    }
    public static BitmapSource Default {
        get {
            if(embedded==null)using(var stream=typeof(WallpaperFiles).Assembly.GetManifestResourceStream("NoteDesk.CloudsWallpaper.png")) {
                if(stream==null)throw new InvalidDataException("默认壁纸资源缺失，请重新下载完整程序。");
                using(var buffer=new MemoryStream()){stream.CopyTo(buffer);embedded=Decode(buffer.ToArray());}
            }
            return embedded;
        }
    }
    public static string Resolve(string root,string relative) {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))return null;
        try {
            string directory=Path.GetFullPath(Path.Combine(root,"wallpapers"))+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(root,relative));
            return path.StartsWith(directory,StringComparison.OrdinalIgnoreCase)?path:null;
        }catch(ArgumentException){return null;}catch(NotSupportedException){return null;}catch(PathTooLongException){return null;}
    }
    public static BitmapSource Load(string root,string relative,out string warning) {
        warning=null;
        if(!string.IsNullOrWhiteSpace(relative)) {
            try {
                string path=Resolve(root,relative);
                if(path==null||!File.Exists(path))throw new FileNotFoundException();
                if(new FileInfo(path).Length>MaxBytes)throw new InvalidDataException();
                return Decode(ReadImage(path));
            }catch(Exception ex) {
                if(!ImageError(ex))throw;
                warning="自定义壁纸无法读取，已使用默认云朵壁纸。";
            }
        }
        return Default;
    }
    public static bool Import(string root,string source,out string relative,out string error) {
        relative=null;error=null;
        try {
            var info=new FileInfo(source);if(!info.Exists)throw new InvalidDataException("找不到这张图片。");
            if(info.Length>MaxBytes)throw new InvalidDataException("图片文件过大，请选择 32 MB 以内的图片。");
            var bytes=ReadImage(source);Decode(bytes);
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            string directory=Path.Combine(root,"wallpapers");Directory.CreateDirectory(directory);
            relative=Path.Combine("wallpapers",hash+".img");string target=Path.Combine(root,relative);
            string temporary=target+".tmp-"+Guid.NewGuid().ToString("N");
            try {File.WriteAllBytes(temporary,bytes);if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);}
            finally {if(File.Exists(temporary))try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
            return true;
        }catch(Exception ex) {
            if(!ImageError(ex))throw;
            error=ex is InvalidDataException?ex.Message:"无法使用这张图片，请选择 PNG、JPG 或 BMP 图片。";relative=null;return false;
        }
    }
    public static void Prune(string root,string current) {
        // Only app-owned hash filenames. Retain the active/backup images and three recent extras.
        try {
            var keep=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var active=Resolve(root,current);if(active!=null)keep.Add(active);
            try {string backup=Path.Combine(root,"notes.json.bak");if(File.Exists(backup)){var data=Store.Json.Deserialize<Database>(File.ReadAllText(backup));var previous=data==null?null:Resolve(root,data.WallpaperPath);if(previous!=null)keep.Add(previous);}}catch(ArgumentException){}catch(IOException){}catch(UnauthorizedAccessException){}catch(InvalidOperationException){}
            string directory=Path.Combine(root,"wallpapers");if(!Directory.Exists(directory))return;
            var owned=new DirectoryInfo(directory).GetFiles("*.img").Where(file=>System.Text.RegularExpressions.Regex.IsMatch(file.Name,@"\A[0-9a-f]{64}\.img\z",System.Text.RegularExpressions.RegexOptions.IgnoreCase)).OrderByDescending(file=>file.LastWriteTimeUtc).ToList();
            foreach(var recent in owned.Where(file=>!keep.Contains(file.FullName)).Take(3))keep.Add(recent.FullName);
            foreach(var file in owned.Where(file=>!keep.Contains(file.FullName)))try{file.Delete();}catch(IOException){}catch(UnauthorizedAccessException){}
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
}
/// <summary>One wallpaper and one bounded blurred cache, sampled by every panel.</summary>
public sealed class WallpaperScene:Grid {
    readonly Store store;
    readonly Func<bool> effects;
    readonly Canvas paper=new Canvas {IsHitTestVisible=false};
    readonly Image picture=new Image {Stretch=Stretch.Fill};
    readonly Border shade=new Border {IsHitTestVisible=false};
    readonly List<WeakReference> surfaces=new List<WeakReference>();
    readonly DispatcherTimer pending;
    string loadedPath;
    string loadedStamp;
    BitmapSource source,blurred;
    Size cacheSize;
    double cacheBlur=-1,cacheX=-1,cacheY=-1;
    bool useEffects;
    public string LoadWarning {get;private set;}
    public BitmapSource Source {get{return source;}}
    public BitmapSource Blurred {get{return blurred;}}
    public Size SceneSize {get;private set;}
    public bool UseEffects {get{return useEffects;}}
    public double PanelOpacity {get{return UseEffects?WallpaperGeometry.Limit(store.Data.GlassOpacity,.5,.96,.60):1;}}
    public double Brightness {get{return WallpaperGeometry.Limit(store.Data.WallpaperBrightness,.4,1.2,.80);}}
    public WallpaperScene(Store store):this(store,()=>DesktopGlass.EffectsAllowed){}
    public WallpaperScene(Store store,Func<bool> effects) {
        this.store=store;this.effects=effects;ClipToBounds=true;Background=UI.Hex("#FAF9F6");
        paper.Children.Add(picture);Children.Add(paper);Children.Add(shade);
        pending=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(90)};
        pending.Tick+=(s,e)=>{pending.Stop();BuildBlur();RefreshSurfaces();};
        Unloaded+=(s,e)=>pending.Stop();
        RefreshSettings();
    }
    internal void Register(GlassSurface surface){surfaces.Add(new WeakReference(surface));}
    void RefreshSurfaces() {
        for(int i=surfaces.Count-1;i>=0;i--){var surface=surfaces[i].Target as GlassSurface;if(surface==null)surfaces.RemoveAt(i);else surface.Refresh();}
    }
    string FileStamp() {try{var path=WallpaperFiles.Resolve(store.Root,store.Data.WallpaperPath);var info=path==null?null:new FileInfo(path);return info!=null&&info.Exists?info.LastWriteTimeUtc.Ticks+":"+info.Length:"missing";}catch(IOException){return "missing";}catch(UnauthorizedAccessException){return "missing";}}
    public void RefreshSettings() {
        useEffects=effects()&&!SystemParameters.HighContrast;
        if(source==null||loadedPath!=store.Data.WallpaperPath||(LoadWarning!=null&&loadedStamp!=FileStamp())) {
            string warning;source=WallpaperFiles.Load(store.Root,store.Data.WallpaperPath,out warning);LoadWarning=warning;
            loadedPath=store.Data.WallpaperPath;loadedStamp=FileStamp();picture.Source=source;cacheBlur=-1;
        }
        paper.Visibility=shade.Visibility=SystemParameters.HighContrast?Visibility.Hidden:Visibility.Visible;
        SetShade(shade,Brightness);PositionImage();
        RefreshSurfaces();
        if(SceneSize.Width>0)pending.Start();
    }
    internal static void SetShade(Border target,double brightness) {
        target.Background=brightness<=1?Brushes.Black:Brushes.White;
        target.Opacity=brightness<=1?1-brightness:(brightness-1)*2;
    }
    void PositionImage() {
        if(source==null||SceneSize.Width<=0||SceneSize.Height<=0)return;
        var crop=WallpaperGeometry.Cover(new Size(source.PixelWidth,source.PixelHeight),SceneSize,store.Data.WallpaperX,store.Data.WallpaperY);
        picture.Width=crop.Width;picture.Height=crop.Height;Canvas.SetLeft(picture,crop.X);Canvas.SetTop(picture,crop.Y);
    }
    public void Prepare(Size size) {
        if(size.Width<=0||size.Height<=0)return;
        SceneSize=size;PositionImage();pending.Stop();BuildBlur();RefreshSurfaces();
    }
    protected override Size MeasureOverride(Size availableSize) {
        if(!double.IsInfinity(availableSize.Width)&&!double.IsInfinity(availableSize.Height)&&availableSize.Width>0&&availableSize.Height>0) {
            SceneSize=availableSize;PositionImage();
        }
        return base.MeasureOverride(availableSize);
    }
    protected override Size ArrangeOverride(Size finalSize) {
        bool changed=cacheSize!=finalSize;SceneSize=finalSize;PositionImage();
        var result=base.ArrangeOverride(finalSize);
        if(blurred==null)BuildBlur();else if(changed)pending.Start();
        RefreshSurfaces();return result;
    }
    void BuildBlur() {
        if(!UseEffects){blurred=null;return;}
        if(source==null||SceneSize.Width<=0||SceneSize.Height<=0)return;
        double radius=WallpaperGeometry.Limit(store.Data.GlassBlur,0,48,25);
        double x=WallpaperGeometry.Limit(store.Data.WallpaperX,0,1,.5),y=WallpaperGeometry.Limit(store.Data.WallpaperY,0,1,1);
        if(blurred!=null&&cacheSize==SceneSize&&cacheBlur==radius&&cacheX==x&&cacheY==y)return;
        double scale=Math.Min(1,1280/Math.Max(SceneSize.Width,SceneSize.Height));
        int width=Math.Max(1,(int)Math.Ceiling(SceneSize.Width*scale)),height=Math.Max(1,(int)Math.Ceiling(SceneSize.Height*scale));
        int pad=(int)Math.Ceiling(radius*scale*2)+2;
        var crop=WallpaperGeometry.Cover(new Size(source.PixelWidth,source.PixelHeight),new Size(width,height),x,y);
        var drawing=new DrawingVisual();
        using(var dc=drawing.RenderOpen()) {
            // Mirror only pixels beyond the actual wallpaper edges; keep the interior crop exact.
            var brush=new ImageBrush(source) {Stretch=Stretch.Fill,ViewportUnits=BrushMappingMode.Absolute,
                Viewport=new Rect(crop.X+pad,crop.Y+pad,crop.Width,crop.Height),TileMode=TileMode.FlipXY};
            dc.DrawRectangle(brush,null,new Rect(0,0,width+pad*2,height+pad*2));
        }
        var raw=new RenderTargetBitmap(width+pad*2,height+pad*2,96,96,PixelFormats.Pbgra32);raw.Render(drawing);raw.Freeze();
        var image=new Image {Source=raw,Width=raw.PixelWidth,Height=raw.PixelHeight,Stretch=Stretch.Fill};
        if(radius>0)image.Effect=new BlurEffect {Radius=radius*scale,KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Performance};
        image.Measure(new Size(raw.PixelWidth,raw.PixelHeight));image.Arrange(new Rect(0,0,raw.PixelWidth,raw.PixelHeight));image.UpdateLayout();
        var bitmap=new RenderTargetBitmap(raw.PixelWidth,raw.PixelHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(image);
        var clipped=new CroppedBitmap(bitmap,new Int32Rect(pad,pad,width,height));clipped.Freeze();blurred=clipped;
        cacheSize=SceneSize;cacheBlur=radius;cacheX=x;cacheY=y;
    }
}
/// <summary>Sharp content above a clipped sample of the shared blurred wallpaper.</summary>
public sealed class GlassSurface:Grid {
    readonly WallpaperScene scene;
    readonly Canvas sample=new Canvas {IsHitTestVisible=false};
    readonly Image backdrop=new Image {Stretch=Stretch.Fill,IsHitTestVisible=false};
    readonly TranslateTransform offset=new TranslateTransform();
    readonly Border shade=new Border {IsHitTestVisible=false};
    readonly Border tint=new Border {IsHitTestVisible=false};
    readonly Border rim=new Border {BorderThickness=new Thickness(1),IsHitTestVisible=false};
    readonly Border host=new Border();
    readonly SolidColorBrush tintBrush=new SolidColorBrush();
    readonly DropShadowEffect shadow=new DropShadowEffect {Color=Color.FromRgb(19,29,54),BlurRadius=18,ShadowDepth=4};
    bool? rimDark,rimContrast;
    double radius=18;
    public UIElement Content {get{return host.Child;}set{host.Child=value;}}
    public Thickness Padding {get{return host.Padding;}set{host.Padding=value;}}
    public double Radius {get{return radius;}set{radius=value;Refresh();}}
    public GlassSurface(WallpaperScene scene) {
        this.scene=scene;
        backdrop.RenderTransform=offset;sample.Children.Add(backdrop);
        Children.Add(sample);Children.Add(shade);Children.Add(tint);Children.Add(rim);Children.Add(host);
        scene.Register(this);LayoutUpdated+=(s,e)=>PositionSample();
        Refresh();
    }
    protected override Size ArrangeOverride(Size finalSize) {
        sample.Clip=new RectangleGeometry(new Rect(finalSize),radius,radius);
        var result=base.ArrangeOverride(finalSize);PositionSample();return result;
    }
    void PositionSample() {
        if(!scene.IsAncestorOf(this))return;
        var point=TransformToAncestor(scene).Transform(new Point(0,0));
        if(offset.X!=-point.X)offset.X=-point.X;if(offset.Y!=-point.Y)offset.Y=-point.Y;
    }
    public void Refresh() {
        var corners=new CornerRadius(radius);tint.CornerRadius=shade.CornerRadius=rim.CornerRadius=host.CornerRadius=corners;
        var color=((SolidColorBrush)UI.Hex("#FAF9F6")).Color;color.A=(byte)Math.Round(scene.PanelOpacity*255);
        if(tintBrush.Color!=color)tintBrush.Color=color;
        if(tint.Background!=tintBrush)tint.Background=tintBrush;
        shadow.Opacity=UI.IsDark?.22:.12;
        if(tint.Effect!=(scene.UseEffects?shadow:null))tint.Effect=scene.UseEffects?shadow:null;
        WallpaperScene.SetShade(shade,scene.Brightness);
        backdrop.Source=scene.Blurred;backdrop.Width=Math.Max(1,scene.SceneSize.Width);backdrop.Height=Math.Max(1,scene.SceneSize.Height);
        sample.Visibility=shade.Visibility=scene.UseEffects?Visibility.Visible:Visibility.Hidden;
        if(rimDark!=UI.IsDark||rimContrast!=SystemParameters.HighContrast) {
        rimDark=UI.IsDark;rimContrast=SystemParameters.HighContrast;
        rim.BorderBrush=SystemParameters.HighContrast?UI.Line:new LinearGradientBrush(new GradientStopCollection {
            new GradientStop(UI.IsDark?Color.FromArgb(68,238,211,244):Color.FromArgb(194,255,255,255),0),
            new GradientStop(UI.IsDark?Color.FromArgb(40,151,176,226):Color.FromArgb(100,198,200,229),.55),
            new GradientStop(Color.FromArgb(UI.IsDark?(byte)58:(byte)138,234,178,218),1)},45);
        }
        PositionSample();
    }
}
}
