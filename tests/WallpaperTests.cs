using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NoteDesk;

public static class WallpaperTests {
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    [STAThread] public static int Main(string[] args) {
        try {
            var path=typeof(Database).GetField("WallpaperPath");
            Assert(path!=null,"wallpaper settings are missing");
            var data=Store.Json.Deserialize<Database>("{\"Schema\":1,\"Notes\":[],\"Sound\":true}");
            Assert(string.IsNullOrEmpty((string)path.GetValue(data)),"legacy JSON uses embedded wallpaper");
            Assert((double)typeof(Database).GetField("GlassOpacity").GetValue(data)==.86,"reading-first default opacity");
            Assert((double)typeof(Database).GetField("GlassBlur").GetValue(data)==24,"default blur");
            Assert((double)typeof(Database).GetField("WallpaperY").GetValue(data)==1,"wallpaper aligned to bottom");
            using(var resource=typeof(Note).Assembly.GetManifestResourceStream("NoteDesk.CloudsWallpaper.png"))Assert(resource!=null,"portable executable embeds default wallpaper");
            var geometry=typeof(Note).Assembly.GetType("NoteDesk.WallpaperGeometry");
            Assert(geometry!=null,"aspect-fill crop geometry missing");
            var cover=geometry.GetMethod("Cover");
            foreach(var size in new[]{new Size(1040,740),new Size(820,540),new Size(1920,1080)}) {
                var crop=(Rect)cover.Invoke(null,new object[]{new Size(3000,1687),size,.5,1.0});
                Assert(crop.Left<=0&&crop.Top<=0&&crop.Right>=size.Width-.01&&crop.Bottom>=size.Height-.01,"wallpaper fills viewport without bands");
                Assert(Math.Abs(crop.Width/crop.Height-3000.0/1687)<.0001,"wallpaper preserves aspect ratio");
            }
            var root=Path.Combine(Path.GetTempPath(),"NoteDesk-wallpaper-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            Assert(WallpaperFiles.Default.PixelWidth==3000&&WallpaperFiles.Default.PixelHeight==1687,"embedded image keeps exact source dimensions");
            Assert(WallpaperFiles.Resolve(root,"../private.png")==null&&WallpaperFiles.Resolve(root,Path.Combine(root,"private.png"))==null,"out-of-managed-directory paths rejected");
            string warning;var fallback=WallpaperFiles.Load(root,"wallpapers/missing.png",out warning);
            Assert(object.ReferenceEquals(fallback,WallpaperFiles.Default)&&warning!=null,"missing custom wallpaper falls back");
            var source=Path.Combine(root,"source.png");using(var input=typeof(Note).Assembly.GetManifestResourceStream("NoteDesk.CloudsWallpaper.png"))using(var output=File.Create(source))input.CopyTo(output);
            string managed,error;Assert(WallpaperFiles.Import(root,source,out managed,out error),"valid image import: "+error);File.Delete(source);
            Assert(WallpaperFiles.Load(root,managed,out warning).PixelWidth==3000&&warning==null,"managed image survives original removal");
            File.WriteAllText(source,"not an image");Assert(!WallpaperFiles.Import(root,source,out managed,out error)&&managed==null&&!string.IsNullOrEmpty(error),"corrupt import rejected");
            using(var oversized=File.Create(source))oversized.SetLength(32L*1024*1024+1);Assert(!WallpaperFiles.Import(root,source,out managed,out error)&&managed==null,"oversized image rejected before decoding");
            Assert(typeof(Note).Assembly.GetType("NoteDesk.WallpaperScene")!=null,"aligned glass scene is missing");
            var app=new DeskApp(Path.Combine(root,"scene"),true);UI.ApplyAppearance("light");
            Assert(Contrast(((SolidColorBrush)UI.Muted).Color,Color.FromRgb(211,211,213))>=4.5,"secondary text stays readable on default glass over dark wallpaper");
            byte[] pixels=new byte[1024*512*4];for(int yy=0;yy<512;yy++)for(int xx=0;xx<1024;xx++){int index=(yy*1024+xx)*4;pixels[index]=(byte)(((xx/8+yy/8)%2)*255);pixels[index+1]=(byte)(yy/2);pixels[index+2]=(byte)(xx/4);pixels[index+3]=255;}
            var image=BitmapSource.Create(1024,512,96,96,PixelFormats.Bgra32,null,pixels,1024*4);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(source))png.Save(output);
            Assert(WallpaperFiles.Import(app.Store.Root,source,out managed,out error),"synthetic gradient import");
            app.Store.Data.WallpaperPath=managed;app.Store.Data.WallpaperBrightness=1;app.Store.Data.GlassOpacity=.5;
            var scene=new WallpaperScene(app.Store,()=>true);scene.Prepare(new Size(320,240));
            var sharp=Pixel(Render(scene,320,240),80,120);
            Assert(Math.Abs(sharp[2]-85)<3&&Math.Abs(sharp[1]-128)<3,"sharp wallpaper fills and aligns with scene, got "+string.Join(",",sharp));
            var point=Pixel(scene.Blurred,80,120);
            Assert(Math.Abs(point[2]-85)<3,"blur sample must align with sharp wallpaper coordinates, got red="+point[2]);
            Assert(point[0]>100&&point[0]<155,"real blur smooths checker detail without blurring foreground");
            var panel=new GlassSurface(scene) {Width=100,Height=90,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(60,40,0,0)};scene.Children.Add(panel);
            var outputScene=Render(scene,320,240);var composed=Pixel(outputScene,80,70);var under=Pixel(scene.Blurred,80,70);
            Assert(Math.Abs(composed[2]-(under[2]+245)/2)<9,"panel samples its scene location instead of repeating wallpaper");
            var fallbackScene=new WallpaperScene(app.Store,()=>false);fallbackScene.Children.Add(new GlassSurface(fallbackScene));fallbackScene.Prepare(new Size(160,100));var solid=Pixel(Render(fallbackScene,160,100),50,50);
            Assert(solid[2]==245&&solid[1]==245&&solid[0]==247,"transparency-off panel is opaque and readable");
            var scrollingScene=new WallpaperScene(app.Store,()=>true);var cards=new StackPanel();for(int i=0;i<4;i++)cards.Children.Add(new GlassSurface(scrollingScene) {Height=120,Margin=new Thickness(0,0,0,12)});
            var scroller=new ScrollViewer {Content=cards,Width=180,Height=160,Margin=new Thickness(40,40,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden};scrollingScene.Children.Add(scroller);
            scrollingScene.Prepare(new Size(320,240));var before=Pixel(Render(scrollingScene,320,240),80,80);scroller.ScrollToVerticalOffset(132);scroller.UpdateLayout();var after=Pixel(Render(scrollingScene,320,240),80,80);
            Assert(scroller.VerticalOffset==132&&Math.Abs(before[0]-after[0])<3&&Math.Abs(before[1]-after[1])<3&&Math.Abs(before[2]-after[2])<3,"scrolling cards keeps glass aligned to stationary wallpaper");
            scene.Prepare(new Size(3840,2160));Assert(scene.Blurred.PixelWidth<=1280&&scene.Blurred.PixelHeight<=1280,"blur cache remains bounded at large window sizes");
            Assert(typeof(Manager).GetProperty("Wallpaper")!=null,"manager wallpaper islands are not integrated");
            app.Manager=new Manager(app);var managerImage=Render((FrameworkElement)app.Manager.Content,1040,740);var gap=Pixel(managerImage,240,500);
            Assert(gap[2]<130&&gap[1]>140,"manager gutters must expose wallpaper rather than panel spill, got "+string.Join(",",gap));
            var corner=Pixel(managerImage,1035,735);Assert(corner[2]<230&&corner[1]>248,"first layout must fill the entire window, got "+string.Join(",",corner));
            var debug=new PngBitmapEncoder();debug.Frames.Add(BitmapFrame.Create(managerImage));using(var output=File.Create(Path.Combine(Directory.GetCurrentDirectory(),".qa","manager-gradient.png")))debug.Save(output);
            Assert(app.Manager.WindowStyle==WindowStyle.None,"manager caption is integrated with wallpaper");
            app.Store.Data.GlassBlur=33;app.Store.Data.GlassOpacity=.79;app.Store.Data.WallpaperX=.2;app.UpdateWallpaper();app.FlushWallpaperChanges();
            var persisted=new Store(app.Store.Root).Data;Assert(persisted.GlassBlur==33&&persisted.GlassOpacity==.79&&persisted.WallpaperX==.2,"material settings persist");
            app.Store.Data.Appearance="dark";app.Store.Data.HotkeyEnabled=false;app.Store.Data.Notes.Add(new Note {Title="keep me"});
            WallpaperPreferences.ResetDefaults(app.Store.Data);
            Assert(app.Store.Data.WallpaperPath==""&&app.Store.Data.GlassBlur==24&&app.Store.Data.GlassOpacity==.86&&app.Store.Data.WallpaperY==1,"reset restores wallpaper and material defaults");
            Assert(app.Store.Data.Appearance=="dark"&&!app.Store.Data.HotkeyEnabled&&app.Store.Data.Notes.Count==1,"reset preserves notes, theme and shortcut");
            var recoveryRoot=Path.Combine(root,"recovery");string recoverPath;Assert(WallpaperFiles.Import(recoveryRoot,source,out recoverPath,out error),"prepare recovery image");
            File.WriteAllText(WallpaperFiles.Resolve(recoveryRoot,recoverPath),"broken managed image");Assert(WallpaperFiles.Import(recoveryRoot,source,out recoverPath,out error),"reimport original image");
            var recoverFailures=new System.Collections.Generic.List<string>();
            if(WallpaperFiles.Load(recoveryRoot,recoverPath,out warning).PixelWidth!=1024||warning!=null)recoverFailures.Add("reimport must repair corrupt managed copy");
            File.Delete(WallpaperFiles.Resolve(recoveryRoot,recoverPath));var recoveryStore=new Store(recoveryRoot);recoveryStore.Data.WallpaperPath=recoverPath;var recoveryScene=new WallpaperScene(recoveryStore,()=>true);
            Assert(recoveryScene.LoadWarning!=null,"prepare missing-image fallback");Assert(WallpaperFiles.Import(recoveryRoot,source,out recoverPath,out error),"restore missing image");recoveryScene.RefreshSettings();
            if(recoveryScene.Source.PixelWidth!=1024||recoveryScene.LoadWarning!=null)recoverFailures.Add("reselecting missing wallpaper refreshes manager immediately");
            var prune=typeof(WallpaperFiles).GetMethod("Prune");
            if(prune==null)recoverFailures.Add("managed wallpaper storage must stay bounded");
            else {
                var paths=new System.Collections.Generic.List<string>();var bytes=File.ReadAllBytes(source);
                for(byte i=0;i<8;i++){File.WriteAllBytes(source,bytes.Concat(new byte[]{i}).ToArray());Assert(WallpaperFiles.Import(recoveryRoot,source,out recoverPath,out error),"import bounded history");paths.Add(recoverPath);}
                recoveryStore.Data.WallpaperPath=paths[1];recoveryStore.Save();recoveryStore.Data.WallpaperPath=paths[0];recoveryStore.Save();File.WriteAllText(Path.Combine(recoveryRoot,"wallpapers","user-file.txt"),"keep");
                prune.Invoke(null,new object[]{recoveryRoot,paths[0]});
                Assert(File.Exists(WallpaperFiles.Resolve(recoveryRoot,paths[0]))&&File.Exists(WallpaperFiles.Resolve(recoveryRoot,paths[1])),"cleanup preserves current and backup images");
                Assert(Directory.GetFiles(Path.Combine(recoveryRoot,"wallpapers"),"*.img").Length<=5&&File.Exists(Path.Combine(recoveryRoot,"wallpapers","user-file.txt")),"cleanup is bounded and retains unrelated files");
            }
            Assert(recoverFailures.Count==0,string.Join("; ",recoverFailures.ToArray()));
            app.Shutdown();
            Console.WriteLine("PASS: wallpaper defaults, embedding, crop, managed imports/fallback, aligned real blur, solid panels and bounded cache");return 0;
        }catch(Exception ex){Console.Error.WriteLine("FAIL: "+ex);return 1;}
    }
    static byte[] Pixel(BitmapSource bitmap,int x,int y){var bytes=new byte[4];bitmap.CopyPixels(new Int32Rect(x,y,1,1),bytes,4,0);return bytes;}
    static double Luma(Color color){Func<byte,double> linear=value=>{double n=value/255.0;return n<=.04045?n/12.92:Math.Pow((n+.055)/1.055,2.4);};return .2126*linear(color.R)+.7152*linear(color.G)+.0722*linear(color.B);}
    static double Contrast(Color a,Color b){double x=Luma(a),y=Luma(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
    static BitmapSource Render(FrameworkElement element,int width,int height){element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();var target=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);target.Render(element);return target;}
}
