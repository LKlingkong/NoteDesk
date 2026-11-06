using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace NoteDesk
{
    internal sealed class PetWindow : Window
    {
        private static readonly int[] FrameCounts = { 6, 8, 8, 4, 5, 8, 6, 6, 6 };
        private const int ExStyle = -20;
        private const int TransparentStyle = 0x20;
        private readonly Image sprite;
        private readonly Popup bubble;
        private readonly TextBlock message;
        private readonly StackPanel reminderButtons;
        private readonly BitmapSource[][] frames;
        private readonly DispatcherTimer timer;
        private readonly DeskApp app;
        private Action openReminder;
        private ContextMenu snoozeMenu;
        private Note reminderNote;
        private TaskItem reminderTask;
        private DateTime actionUntil;
        private DateTime bubbleUntil;
        private DateTime nextFrame;
        private DateTime nextWalk;
        private int state;
        private int frame;
        private int steps;
        private int direction = -1;
        private bool walking;
        private bool dragging;
        private bool controllerClose;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        public bool HasReminder { get { return openReminder != null; } }
        public event Action ReminderDismissed;

        public PetWindow(DeskApp app, PetController controller, InstalledPet pet, BitmapSource image,
            PetPreferencesData settings)
        {
            this.app = app;
            UI.WindowTheme(this);
            Title = pet.Name + " · NoteDesk";
            Width = 176;
            Height = 206;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = settings.Topmost;
            WindowStartupLocation = WindowStartupLocation.Manual;
            var work = SystemParameters.WorkArea;
            var virtualDesktop = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            var saved = settings.X.HasValue && settings.Y.HasValue
                ? new Point(settings.X.Value + Width / 2, settings.Y.Value + Height - 20)
                : new Point(double.NaN, double.NaN);
            bool onDesktop = virtualDesktop.Contains(saved);
            Left = onDesktop ? settings.X.Value : work.Right - Width - 18;
            Top = onDesktop ? settings.Y.Value : work.Bottom - Height - 18;

            sprite = new Image
            {
                Source = image,
                Width = 124,
                Height = 134,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = Cursors.SizeAll,
                ToolTip = pet.Name + " · 拖动移动，右键操作"
            };
            System.Windows.Automation.AutomationProperties.SetName(sprite, pet.Name);
            Content = new Grid { Children = { sprite } };

            message = UI.Text("", 12, UI.Ink);
            message.TextWrapping = TextWrapping.Wrap;
            message.TextTrimming = TextTrimming.CharacterEllipsis;
            message.MaxHeight = 68;
            var panel = new StackPanel { Width = 232, Children = { message } };
            reminderButtons = UI.Row();
            reminderButtons.Margin = new Thickness(0, 7, 0, 0);
            Button snooze = null;
            snooze = UI.Button("稍后提醒", () =>
            {
                if (!HasReminder)
                    return;
                snoozeMenu = app.OpenSnoozeMenu(snooze, reminderNote, reminderTask, ClearFeedback);
            });
            reminderButtons.Children.Add(UI.Button("打开便签", () =>
            {
                var open = openReminder;
                ClearFeedback();
                if (open != null)
                    open();
            }, true));
            reminderButtons.Children.Add(snooze);
            var close = UI.Button("×", ClearFeedback);
            System.Windows.Automation.AutomationProperties.SetName(close, "关闭提醒");
            reminderButtons.Children.Add(close);
            panel.Children.Add(reminderButtons);
            // The popup has its own HWND, so its controls remain clickable over a pass-through pet.
            bubble = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                PlacementTarget = sprite,
                Placement = PlacementMode.Top,
                HorizontalOffset = -54,
                VerticalOffset = -6,
                Child = new Border
                {
                    Background = AppearancePalette.ToastBrush(false),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12),
                    Child = panel
                }
            };
            LocationChanged += (sender, args) =>
            {
                if (bubble.IsOpen && PresentationSource.FromVisual(bubble.Child) != null)
                    bubble.HorizontalOffset = bubble.HorizontalOffset == -54 ? -53.99 : -54;
            };
            bubble.Closed += (sender, args) =>
            {
                if (HasReminder)
                    ClearFeedback();
            };

            if (pet.Animated)
            {
                frames = new BitmapSource[FrameCounts.Length][];
                for (int row = 0; row < frames.Length; row++)
                {
                    frames[row] = new BitmapSource[FrameCounts[row]];
                    for (int column = 0; column < frames[row].Length; column++)
                    {
                        var crop = new CroppedBitmap(image, new Int32Rect(column * PetPackage.FrameWidth,
                            row * PetPackage.FrameHeight, PetPackage.FrameWidth, PetPackage.FrameHeight));
                        crop.Freeze();
                        frames[row][column] = crop;
                    }
                }
                sprite.Source = frames[0][0];
            }

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            timer.Tick += (sender, args) => Tick();
            SetWalking(settings.WalkAlongEdge);
            IsVisibleChanged += (sender, args) =>
            {
                if (IsVisible)
                    UpdateTimer();
                else
                    ClearFeedback();
            };
            Closed += (sender, args) =>
            {
                timer.Stop();
                ClearFeedback();
            };
            Closing += (sender, args) =>
            {
                if (controllerClose || app.Exiting)
                    return;
                args.Cancel = true;
                Dispatcher.BeginInvoke(new Action(controller.ToggleVisibility));
            };

            var menu = new ContextMenu();
            AddMenuItem(menu, "新建便签", () => app.NewNote());
            AddMenuItem(menu, "打开管理中心", app.ShowManager);
            AddMenuItem(menu, "隐藏桌宠", controller.ToggleVisibility);
            sprite.ContextMenu = menu;
            sprite.MouseLeftButtonDown += (sender, args) =>
            {
                dragging = true;
                StopWalking(DateTime.UtcNow);
                double left = Left, top = Top;
                try { DragMove(); }
                catch (InvalidOperationException) { return; }
                finally { dragging = false; }
                if (Math.Abs(Left - left) + Math.Abs(Top - top) > 1)
                    controller.RememberPosition(Left, Top);
                else if (!HasReminder)
                    Play(3, 900);
            };
        }

        private static void AddMenuItem(ContextMenu menu, string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (sender, args) => action();
            menu.Items.Add(item);
        }

        public void SetClickThrough(bool enabled)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;
            int style = GetWindowLong(hwnd, ExStyle);
            SetWindowLong(hwnd, ExStyle, enabled ? style | TransparentStyle : style & ~TransparentStyle);
        }

        public void SetWalking(bool enabled)
        {
            walking = enabled;
            StopWalking(DateTime.UtcNow);
            UpdateTimer();
        }

        private void UpdateTimer()
        {
            if (IsVisible && (frames != null || walking || bubble.IsOpen))
                timer.Start();
            else
                timer.Stop();
        }

        public void Celebrate()
        {
            if (HasReminder)
            {
                Play(4, 1000);
                return;
            }
            StopWalking(DateTime.UtcNow);
            Play(4, 1000);
            if (frames == null)
                ShowBubble("这张便签的待办都完成了！", false);
        }

        public bool TryRemind(Note note, TaskItem task)
        {
            if (!IsVisible || HasReminder)
                return false;
            reminderNote = note;
            reminderTask = task;
            openReminder = () => app.ShowNote(note);
            StopWalking(DateTime.UtcNow);
            Play(6, 0);
            try
            {
                ShowBubble("到时间了 · " + task.Text, true);
                if (bubble.IsOpen)
                    return true;
            }
            catch (InvalidOperationException) { }
            ClearFeedback();
            return false;
        }

        private void ShowBubble(string text, bool reminder)
        {
            message.Text = text;
            reminderButtons.Visibility = reminder ? Visibility.Visible : Visibility.Collapsed;
            bubbleUntil = reminder ? DateTime.MaxValue : DateTime.UtcNow.AddSeconds(2);
            bubble.IsOpen = true;
            UpdateTimer();
        }

        private void ClearFeedback()
        {
            bool hadReminder = HasReminder;
            openReminder = null;
            reminderNote = null;
            reminderTask = null;
            if (snoozeMenu != null)
            {
                snoozeMenu.IsOpen = false;
                snoozeMenu = null;
            }
            bubble.IsOpen = false;
            Play(0, 0);
            StopWalking(DateTime.UtcNow);
            UpdateTimer();
            if (hadReminder && ReminderDismissed != null)
                ReminderDismissed();
        }

        private void Play(int row, int milliseconds)
        {
            state = row;
            frame = 0;
            actionUntil = DateTime.UtcNow.AddMilliseconds(milliseconds);
            nextFrame = DateTime.UtcNow.AddMilliseconds(160);
            if (frames != null)
                sprite.Source = frames[state][0];
        }

        private void StopWalking(DateTime now)
        {
            steps = 0;
            nextWalk = now.AddSeconds(8);
            if (state == 1 || state == 2)
                Play(0, 0);
        }

        private void Walk(DateTime now)
        {
            if (!walking || dragging || bubble.IsOpen || !UI.MotionAllowed || sprite.IsMouseOver ||
                (sprite.ContextMenu != null && sprite.ContextMenu.IsOpen))
            {
                if (steps != 0)
                    StopWalking(now);
                return;
            }
            if ((state != 0 && state != 1 && state != 2) || now < nextWalk)
                return;
            var source = PresentationSource.FromVisual(this);
            if (source == null)
                return;
            var scale = source.CompositionTarget.TransformToDevice;
            var origin = PointToScreen(new Point(0, 0));
            var area = Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            double distance = area.Bottom - (origin.Y + Height * scale.M22);
            if (distance < -8 * scale.M22 || distance > 42 * scale.M22 ||
                area.Width <= (Width + 24) * scale.M11)
            {
                StopWalking(now);
                return;
            }
            if (steps == 0)
                steps = 20;
            double delta = direction * 3.2;
            double min = area.Left + 8 * scale.M11;
            double max = area.Right - (Width + 8) * scale.M11;
            if (origin.X + delta * scale.M11 < min || origin.X + delta * scale.M11 > max)
            {
                direction = -direction;
                delta = (Math.Max(min, Math.Min(max, origin.X + direction * 3.2 * scale.M11)) - origin.X) / scale.M11;
            }
            Left += delta;
            int row = direction > 0 ? 1 : 2;
            if (state != row)
                Play(row, 0);
            if (--steps == 0)
                StopWalking(now);
        }

        private void Tick()
        {
            DateTime now = DateTime.UtcNow;
            if (bubble.IsOpen && (now >= bubbleUntil || (HasReminder &&
                (reminderNote.State != "active" || reminderTask.Done || !reminderTask.Reminded ||
                 !reminderNote.Tasks.Contains(reminderTask)))))
                ClearFeedback();
            if (state != 0 && state != 1 && state != 2 && now >= actionUntil &&
                (!HasReminder || state != 6))
                Play(HasReminder ? 6 : 0, 0);
            Walk(now);
            if (frames == null || now < nextFrame || !SystemParameters.ClientAreaAnimation)
                return;
            nextFrame = now.AddMilliseconds(160);
            frame = (frame + 1) % frames[state].Length;
            sprite.Source = frames[state][frame];
        }

        public void CloseForController()
        {
            controllerClose = true;
            ClearFeedback();
            Close();
        }
    }
}
