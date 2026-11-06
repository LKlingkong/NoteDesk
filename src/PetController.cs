using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace NoteDesk
{
    internal sealed class PetPreferencesData
    {
        public bool Enabled { get; set; }
        public bool Topmost { get; set; }
        public bool WalkAlongEdge { get; set; }
        public string Selected { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
    }

    internal sealed class InstalledPet
    {
        public string Key { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Origin { get; set; }
        public bool Animated { get; set; }
    }

    internal sealed class PetController : IDisposable
    {
        public const string DefaultRepository = "https://github.com/HanaAyane/remielle-codex-pet";
        public const string DefaultFolder = "output/xiaolemi";
        private const string DefaultId = "xiaolemi";
        private readonly DeskApp app;
        private readonly string directory;
        private readonly string settingsFile;
        private PetPreferencesData settings;
        private PetWindow window;
        private bool importing;
        private bool disposed;
        private bool clickThrough;
        private readonly Queue<Tuple<Note, TaskItem>> reminders = new Queue<Tuple<Note, TaskItem>>();

        public PetController(DeskApp app)
        {
            this.app = app;
            directory = Path.Combine(app.Store.Root, "pets");
            settingsFile = Path.Combine(directory, "settings.json");
            settings = ReadJson<PetPreferencesData>(settingsFile) ?? new PetPreferencesData();
        }

        public bool Enabled { get { return settings.Enabled; } }
        public bool Topmost { get { return settings.Topmost; } }
        public bool WalkAlongEdge { get { return settings.WalkAlongEdge; } }
        public bool ClickThrough { get { return clickThrough; } }
        public event Action Changed;

        public List<InstalledPet> List()
        {
            if (!Directory.Exists(directory))
                return new List<InstalledPet>();
            return Directory.GetFiles(directory, "*.pet.json").Select(file => ReadJson<InstalledPet>(file))
                .Where(pet => pet != null && ValidKey(pet.Key) && File.Exists(ImagePath(pet.Key)))
                .OrderBy(pet => pet.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public InstalledPet Selected()
        {
            var pets = List();
            return pets.FirstOrDefault(pet => pet.Key == settings.Selected) ??
                   pets.FirstOrDefault(pet => pet.Id == DefaultId);
        }

        public void Start()
        {
            if (!settings.Enabled)
                return;
            try { ShowSelected(); }
            catch (Exception ex) { app.Status("桌宠未能启动：" + ex.Message); }
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
                ShowSelected();
            else if (window != null)
                window.Hide();
            bool previous = settings.Enabled;
            settings.Enabled = enabled;
            try { SaveSettings(); }
            catch
            {
                settings.Enabled = previous;
                if (window != null)
                {
                    if (previous) window.Show();
                    else window.Hide();
                }
                throw;
            }
            RaiseChanged();
        }

        public void SetWalkAlongEdge(bool enabled)
        {
            bool previous = settings.WalkAlongEdge;
            settings.WalkAlongEdge = enabled;
            try { SaveSettings(); }
            catch { settings.WalkAlongEdge = previous; throw; }
            if (window != null)
                window.SetWalking(enabled);
            RaiseChanged();
        }

        public void ToggleClickThrough()
        {
            if (window != null)
                window.SetClickThrough(!clickThrough);
            clickThrough = !clickThrough;
            RaiseChanged();
        }

        public void SetTopmost(bool topmost)
        {
            bool previous = settings.Topmost;
            settings.Topmost = topmost;
            try { SaveSettings(); }
            catch { settings.Topmost = previous; throw; }
            if (window != null)
                window.Topmost = topmost;
            RaiseChanged();
        }

        public void ToggleVisibility()
        {
            try { SetEnabled(!settings.Enabled); }
            catch (Exception ex)
            {
                app.Status(ex.Message);
                app.ShowManager();
            }
        }

        public void Select(InstalledPet pet)
        {
            if (pet == null || !ValidKey(pet.Key))
                throw new InvalidDataException("请先导入桌宠资源。");
            var image = PetPackage.Decode(PetPackage.ReadFile(ImagePath(pet.Key), PetPackage.MaxImageBytes), pet.Animated);
            Select(pet, image);
        }

        public async Task Import(IPetSource source)
        {
            if (importing)
                throw new InvalidOperationException("已有桌宠正在导入。");
            importing = true;
            try
            {
                bool firstPet = List().Count == 0;
                var imported = await Task.Run(() =>
                {
                    var payload = source.Load();
                    var image = PetPackage.Decode(payload.Image, payload.Animated);
                    return Tuple.Create(Install(payload), image);
                });
                if (!disposed)
                    Select(imported.Item1, imported.Item2, firstPet);
            }
            finally
            {
                importing = false;
            }
        }

        public void Celebrate()
        {
            if (window != null && window.IsVisible)
                window.Celebrate();
        }

        public bool TryRemind(IEnumerable<Tuple<Note, TaskItem>> items)
        {
            if (window == null || !window.IsVisible)
                return false;
            foreach (var item in items)
                reminders.Enqueue(item);
            ShowNextReminder();
            return true;
        }

        private void ShowNextReminder()
        {
            if (disposed || reminders.Count == 0)
                return;
            if (window != null && window.IsVisible)
            {
                while (reminders.Count != 0 && !window.HasReminder)
                {
                    var item = reminders.Peek();
                    if (Pending(item) &&
                        !window.TryRemind(item.Item1, item.Item2))
                        break;
                    reminders.Dequeue();
                }
                if (window.HasReminder)
                    return;
            }
            var pending = reminders.Where(Pending).ToList();
            reminders.Clear();
            if (pending.Count != 0)
                new ReminderWindow(app, pending).Show();
        }

        private static bool Pending(Tuple<Note, TaskItem> item)
        {
            return item.Item1.State == "active" && item.Item1.Tasks.Contains(item.Item2) &&
                item.Item2.Reminded && !item.Item2.Done;
        }

        private void ShowSelected()
        {
            var pet = Selected();
            if (pet == null)
                throw new InvalidOperationException("请在「设置与备份 → 桌宠」导入小蕾米或其他桌宠，再启用。");
            if (window == null)
            {
                var image = PetPackage.Decode(PetPackage.ReadFile(ImagePath(pet.Key), PetPackage.MaxImageBytes), pet.Animated);
                window = CreateWindow(pet, image);
            }
            window.Show();
        }

        private void Select(InstalledPet pet, BitmapSource image, bool enableOnImport = false)
        {
            string previousSelection = settings.Selected;
            bool previousEnabled = settings.Enabled;
            bool show = previousEnabled || enableOnImport;
            var replacement = show ? CreateWindow(pet, image) : null;
            try
            {
                if (replacement != null)
                    replacement.Show();
                settings.Selected = pet.Key;
                settings.Enabled = show;
                SaveSettings();
            }
            catch
            {
                settings.Selected = previousSelection;
                settings.Enabled = previousEnabled;
                if (replacement != null)
                    replacement.CloseForController();
                throw;
            }
            var previousWindow = window;
            window = replacement;
            if (previousWindow != null)
                previousWindow.CloseForController();
            RaiseChanged();
        }

        private PetWindow CreateWindow(InstalledPet pet, BitmapSource image)
        {
            var created = new PetWindow(app, this, pet, image, settings);
            created.SourceInitialized += (sender, args) => created.SetClickThrough(clickThrough);
            created.ReminderDismissed += () => app.Dispatcher.BeginInvoke(new Action(ShowNextReminder));
            created.Closed += (sender, args) =>
            {
                if (ReferenceEquals(window, created))
                    window = null;
            };
            return created;
        }

        private void RaiseChanged()
        {
            if (Changed != null)
                Changed();
        }

        public void RememberPosition(double left, double top)
        {
            settings.X = left;
            settings.Y = top;
            try { SaveSettings(); }
            catch (IOException) { app.Status("无法保存桌宠位置。"); }
            catch (UnauthorizedAccessException) { app.Status("无法保存桌宠位置。"); }
        }

        private InstalledPet Install(PetPayload payload)
        {
            string id = string.IsNullOrWhiteSpace(payload.Id) ? "custom" : payload.Id.Trim();
            string name = string.IsNullOrWhiteSpace(payload.Name) ? "自定义桌宠" : payload.Name.Trim();
            if (id.Length > 100 || name.Length > 100)
                throw new InvalidDataException("桌宠名称或 ID 过长。");
            string key;
            using (var sha = SHA256.Create())
            {
                string digest = Convert.ToBase64String(sha.ComputeHash(payload.Image));
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id + "\n" + digest)))
                    .Replace("-", "").ToLowerInvariant();
            }
            var pet = new InstalledPet
            {
                Key = key,
                Id = id,
                Name = name,
                Origin = payload.Origin,
                Animated = payload.Animated
            };
            Directory.CreateDirectory(directory);
            WriteAtomically(ImagePath(key), payload.Image);
            WriteAtomically(Path.Combine(directory, key + ".pet.json"),
                Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(pet)));
            return pet;
        }

        private static bool ValidKey(string key)
        {
            return Regex.IsMatch(key ?? "", @"\A[a-f0-9]{64}\z");
        }

        private string ImagePath(string key)
        {
            return Path.Combine(directory, key + ".image");
        }

        private static T ReadJson<T>(string file) where T : class
        {
            try
            {
                if (File.Exists(file))
                    return new JavaScriptSerializer().Deserialize<T>(
                        Encoding.UTF8.GetString(PetPackage.ReadFile(file, PetPackage.MaxManifestBytes)));
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException ||
                    ex is ArgumentException || ex is InvalidOperationException))
                    throw;
            }
            return null;
        }

        private void SaveSettings()
        {
            Directory.CreateDirectory(directory);
            WriteAtomically(settingsFile, Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(settings)));
        }

        private static void WriteAtomically(string destination, byte[] bytes)
        {
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(destination))
                    File.Replace(temporary, destination, null);
                else
                    File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        public void Dispose()
        {
            disposed = true;
            if (window != null)
            {
                window.CloseForController();
                window = null;
            }
        }
    }
}
