using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NoteDesk;

public static class PetAndMagnetTests
{
    [STAThread]
    public static int Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "NoteDesk-PetTest-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            TestLocalSources(folder);
            TestSourceBoundaries();
            TestSnapping();
            TestPetReminders(folder);
            Console.WriteLine("PASS: pet import, reminder queue and fallback, native click-through, DPI-independent magnet geometry");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            File.Delete(Path.Combine(folder, "plain.png"));
            File.Delete(Path.Combine(folder, "spritesheet.png"));
            File.Delete(Path.Combine(folder, "pet.json"));
            File.Delete(Path.Combine(folder, "notes.json"));
            File.Delete(Path.Combine(folder, "notes.json.bak"));
            string pets = Path.Combine(folder, "pets");
            if (Directory.Exists(pets))
            {
                foreach (var file in Directory.GetFiles(pets))
                    File.Delete(file);
                Directory.Delete(pets);
            }
            if (Directory.Exists(folder))
                Directory.Delete(folder);
        }
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);

    private static void TestPetReminders(string folder)
    {
        string pets = Path.Combine(folder, "pets");
        Directory.CreateDirectory(pets);
        string key = new string('a', 64);
        File.Copy(Path.Combine(folder, "plain.png"), Path.Combine(pets, key + ".image"));
        File.WriteAllText(Path.Combine(pets, key + ".pet.json"),
            "{\"Key\":\"" + key + "\",\"Id\":\"fixture\",\"Name\":\"Fixture\",\"Animated\":false}");
        File.WriteAllText(Path.Combine(pets, "settings.json"),
            "{\"Enabled\":true,\"Selected\":\"" + key + "\"}");
        var app = new DeskApp(folder, true);
        app.Pets.Start();
        var window = app.Windows.OfType<PetWindow>().Single();
        try
        {
            var note = new Note { Visible = false };
            var first = new TaskItem { Text = "First", Due = DateTime.Now.AddMinutes(-1).ToString("o") };
            var second = new TaskItem { Text = "Second", Due = first.Due };
            note.Tasks.Add(first);
            note.Tasks.Add(second);
            app.Store.Data.Notes.Add(note);
            app.CheckReminders();
            Assert(first.Reminded && second.Reminded && window.HasReminder &&
                   !app.Windows.OfType<ReminderWindow>().Any(), "Visible pet replaces the standalone reminder");

            app.Pets.ToggleClickThrough();
            var body = new WindowInteropHelper(window).Handle;
            Assert((GetWindowLong(body, -20) & 0x20) != 0, "Body uses native mouse pass-through");
            var popup = (Popup)typeof(PetWindow).GetField("bubble",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
            var popupSource = (HwndSource)PresentationSource.FromVisual(popup.Child);
            Assert(popupSource != null && popupSource.Handle != body &&
                   (GetWindowLong(popupSource.Handle, -20) & 0x20) == 0, "Popup has a separate clickable HWND");

            var buttons = ((StackPanel)((Border)popup.Child).Child).Children.OfType<StackPanel>().Single();
            var snooze = buttons.Children.OfType<Button>().Single(b => Equals(b.Content, "稍后提醒"));
            snooze.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var menu = (ContextMenu)typeof(PetWindow).GetField("snoozeMenu",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
            Assert(menu != null && menu.Items.Count == 3, "Snooze uses the three existing durations");
            var choice = (MenuItem)menu.Items[0];
            choice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.IsOpen = false;
            window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            Assert(!first.Reminded && Store.Parse(first.Snooze).HasValue && window.HasReminder,
                "Snoozing dismisses the first bubble and advances to the second");
            var close = buttons.Children.OfType<Button>().Single(b => Equals(b.Content, "×"));
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(!window.HasReminder && second.Reminded && !second.Done && second.Snooze == null,
                "Dismissing does not change completion or schedule");
            app.Pets.ToggleClickThrough();
            Assert((GetWindowLong(body, -20) & 0x20) == 0, "Click-through is reversible");
            app.Pets.SetEnabled(false);
            note.Tasks.Add(new TaskItem { Text = "Fallback", Due = first.Due });
            app.CheckReminders();
            Assert(app.Windows.OfType<ReminderWindow>().Count() == 1, "Hidden pet falls back to the reminder window");
        }
        finally
        {
            app.Pets.Dispose();
            foreach (var dialog in app.Windows.OfType<ReminderWindow>().ToList())
                dialog.Close();
            app.Shutdown();
        }
    }

    private static void TestLocalSources(string folder)
    {
        string png = Path.Combine(folder, "plain.png");
        WritePng(png, 2, 2);
        var single = new LocalPetSource(png).Load();
        var plain = PetPackage.Decode(single.Image, single.Animated);
        Assert(!single.Animated && plain.PixelWidth == 2 && plain.IsFrozen, "Static PNG imports as a thread-safe image");

        string manifest = Path.Combine(folder, "pet.json");
        File.WriteAllText(manifest, "{\"id\":\"fixture\",\"displayName\":\"Fixture\",\"spritesheetPath\":\"spritesheet.png\"}", Encoding.UTF8);
        string sheet = Path.Combine(folder, "spritesheet.png");
        WritePng(sheet, 1536, 2288);
        var pet = new LocalPetSource(manifest).Load();
        var sprite = Task.Run(() => PetPackage.Decode(pet.Image, pet.Animated)).GetAwaiter().GetResult();
        var frame = new CroppedBitmap(sprite, new Int32Rect(0, 0, PetPackage.FrameWidth, PetPackage.FrameHeight));
        frame.Freeze();
        Assert(pet.Animated && pet.Id == "fixture" && pet.Name == "Fixture" && sprite.PixelHeight == 2288,
            "Codex v2 manifest and same-folder PNG atlas import together");
        Expect<InvalidDataException>(() => PetPackage.Decode(single.Image, true), "Incorrect atlas size is rejected");

        File.WriteAllText(manifest, "{\"id\":\"fixture\",\"spritesheetPath\":\"missing.webp\"}");
        Expect<FileNotFoundException>(() => new LocalPetSource(manifest).Load(), "A missing companion is reported");
        var recovered = new LocalPetSource(manifest, sheet).Load();
        Assert(recovered.Animated && recovered.Id == "fixture" &&
            PetPackage.Decode(recovered.Image, true).PixelWidth == 1536,
            "Selecting the companion atlas recovers a package without changing its manifest");

        File.WriteAllText(manifest, "{\"spritesheetPath\":\"../plain.png\"}");
        Expect<InvalidDataException>(() => new LocalPetSource(manifest).Load(), "Manifest cannot read an image outside its folder");
    }

    private static void TestSourceBoundaries()
    {
        var source = new GitHubPetSource("https://github.com/HanaAyane/remielle-codex-pet", "output/xiaolemi");
        Assert(source is IPetSource, "GitHub adapter implements the shared source contract");
        Expect<ArgumentException>(() => new GitHubPetSource("https://github.com.example.org/a/b", ""),
            "Only the GitHub hostname is accepted");
        Expect<ArgumentException>(() => new GitHubPetSource("https://github.com/a/b", "../pet"),
            "GitHub resource directory cannot traverse paths");
        using (var bytes = new MemoryStream(new byte[128]))
            Expect<InvalidDataException>(() => PetPackage.ReadBounded(bytes, 64), "Oversized resources are rejected");
    }

    private static void TestSnapping()
    {
        var empty = new NoteMagnet.NativeRect[0];
        var leftMonitor = Bounds(-1920, 0, 0, 1040);
        Assert(NoteMagnet.FindOffset(Bounds(-1913, 6, -1613, 386), leftMonitor, empty, 12, 12) == new Vector(-7, -6),
            "Snaps to both edges on a monitor with negative coordinates");

        var desktop = Bounds(0, 0, 1920, 1040);
        var note = Bounds(192, 180, 492, 550);
        var adjacent = new[] { Bounds(500, 50, 1000, 600) };
        Assert(NoteMagnet.FindOffset(note, desktop, adjacent, 12, 12) == new Vector(8, 0),
            "Snaps to an adjacent window with overlapping vertical range");
        var separated = new[] { Bounds(500, 700, 1000, 1000) };
        Assert(NoteMagnet.FindOffset(note, desktop, separated, 12, 12) == new Vector(0, 0),
            "A far-away window does not attract the note");
        Assert(NoteMagnet.FindOffset(Bounds(13, 100, 313, 480), desktop, empty, 12, 12) == new Vector(0, 0),
            "Moving beyond the threshold releases the note");
    }

    private static NoteMagnet.NativeRect Bounds(int left, int top, int right, int bottom)
    {
        return new NoteMagnet.NativeRect { Left = left, Top = top, Right = right, Bottom = bottom };
    }

    private static void WritePng(string path, int width, int height)
    {
        int stride = width * 4;
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null,
            new byte[stride * height], stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var output = File.Create(path))
            encoder.Save(output);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void Expect<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception(message);
    }
}
