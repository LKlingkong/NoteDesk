using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace NoteDesk
{
    internal sealed class PetPreferences : StackPanel
    {
        private readonly DeskApp app;
        private readonly PetController controller;
        private readonly CheckBox enabled;
        private readonly CheckBox topmost;
        private readonly CheckBox walking;
        private readonly ComboBox pets;
        private readonly TextBlock status;
        private readonly WrapPanel imports;
        private bool refreshing;
        private bool importing;

        public PetPreferences(DeskApp app)
        {
            this.app = app;
            controller = app.Pets;
            Margin = new Thickness(0, 0, 0, 28);
            var title = UI.Text("桌宠", 17, UI.Ink);
            title.FontWeight = FontWeights.SemiBold;
            Children.Add(title);
            var caption = UI.Text("默认桌宠为小蕾米。支持 Codex v2 桌宠包或静态 PNG，导入后保存在本机。", 12, UI.Muted);
            caption.Margin = new Thickness(0, 6, 0, 12);
            Children.Add(caption);
            enabled = new CheckBox { Content = "显示桌宠", Margin = new Thickness(0, 0, 0, 8) };
            topmost = new CheckBox { Content = "桌宠置顶", Margin = new Thickness(0, 0, 0, 10) };
            Children.Add(enabled);
            Children.Add(topmost);
            walking = new CheckBox { Content = "闲置时沿屏幕底边走动", Margin = new Thickness(0, 0, 0, 8) };
            Children.Add(walking);
            pets = new ComboBox { DisplayMemberPath = "Name", Margin = new Thickness(0, 0, 0, 12) };
            System.Windows.Automation.AutomationProperties.SetName(pets, "当前桌宠");
            Children.Add(pets);

            imports = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            imports.Children.Add(UI.Button("导入本地文件", ImportLocal));
            imports.Children.Add(UI.Button("导入小蕾米", () =>
                Import(() => new GitHubPetSource(PetController.DefaultRepository, PetController.DefaultFolder))));
            imports.Children.Add(UI.Button("小蕾米原仓库", () => Do(() =>
                Process.Start(new ProcessStartInfo(PetController.DefaultRepository) { UseShellExecute = true }))));
            Children.Add(imports);

            var github = new StackPanel();
            github.Children.Add(UI.Text("公开 GitHub 仓库首页", 12, UI.Ink));
            var url = new TextBox { Margin = new Thickness(0, 5, 0, 10), ToolTip = "https://github.com/作者/仓库" };
            System.Windows.Automation.AutomationProperties.SetName(url, "GitHub 仓库地址");
            github.Children.Add(url);
            github.Children.Add(UI.Text("资源目录（可选，例如 output/xiaolemi）", 12, UI.Muted));
            var folder = new TextBox { Margin = new Thickness(0, 5, 0, 8) };
            System.Windows.Automation.AutomationProperties.SetName(folder, "桌宠资源目录");
            github.Children.Add(folder);
            github.Children.Add(UI.Button("从 GitHub 导入", () => Import(() => new GitHubPetSource(url.Text, folder.Text))));
            Children.Add(new Expander { Header = "导入其他 GitHub 桌宠", Content = github, Margin = new Thickness(0, 6, 0, 4) });

            status = UI.Text("小蕾米由 HanaAyane 提供，素材使用遵循上游仓库说明。", 11, UI.Muted);
            status.Margin = new Thickness(0, 8, 0, 0);
            Children.Add(status);
            enabled.Click += (sender, args) => Do(() => controller.SetEnabled(enabled.IsChecked == true));
            topmost.Click += (sender, args) => Do(() => controller.SetTopmost(topmost.IsChecked == true));
            walking.Click += (sender, args) => Do(() => controller.SetWalkAlongEdge(walking.IsChecked == true));
            pets.SelectionChanged += (sender, args) =>
            {
                if (!refreshing && pets.SelectedItem is InstalledPet)
                    Do(() => controller.Select((InstalledPet)pets.SelectedItem));
            };
            Loaded += (sender, args) => { controller.Changed += Refresh; Refresh(); };
            Unloaded += (sender, args) => controller.Changed -= Refresh;
            Refresh();
        }

        private void Refresh()
        {
            refreshing = true;
            try
            {
                enabled.IsChecked = controller.Enabled;
                topmost.IsChecked = controller.Topmost;
                walking.IsChecked = controller.WalkAlongEdge;
                var list = controller.List();
                var current = controller.Selected();
                pets.ItemsSource = list;
                pets.SelectedItem = current == null ? null : list.Find(pet => pet.Key == current.Key);
            }
            finally { refreshing = false; }
        }

        private void Do(Action action)
        {
            try { action(); }
            catch (Exception ex) { status.Text = ex.Message; }
            Refresh();
        }

        private void ImportLocal()
        {
            var dialog = new OpenFileDialog
            {
                Title = "请选择解压后的 pet.json 或静态 PNG 图片",
                Filter = "桌宠配置或静态 PNG|*.json;*.png|桌宠配置|*.json|静态 PNG|*.png"
            };
            if (dialog.ShowDialog(app.Manager) != true)
                return;
            string file = dialog.FileName;
            Import(() => new LocalPetSource(file), missing =>
            {
                if (!string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(missing.FileName, file, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(missing.FileName))
                    throw new InvalidDataException("无法读取所选桌宠文件。", missing);
                var sprite = new OpenFileDialog
                {
                    Title = "请选择配套图集 " + Path.GetFileName(missing.FileName),
                    Filter = "桌宠图集|*.webp;*.png",
                    CheckFileExists = true
                };
                return sprite.ShowDialog(app.Manager) == true ? new LocalPetSource(file, sprite.FileName) : null;
            });
        }

        private async void Import(Func<IPetSource> source, Func<FileNotFoundException, IPetSource> pickSprite = null)
        {
            if (importing)
                return;
            importing = true;
            imports.IsEnabled = false;
            status.Text = "正在导入桌宠…";
            try
            {
                IPetSource retry = null;
                try
                {
                    await controller.Import(source());
                }
                catch (FileNotFoundException ex)
                {
                    if (pickSprite == null)
                        throw;
                    retry = pickSprite(ex);
                    if (retry == null)
                    {
                        status.Text = "缺少配套图集。请解压完整桌宠包，或手动选择对应的 WebP 或 PNG 图集。";
                        return;
                    }
                }
                if (retry != null)
                    await controller.Import(retry);
                status.Text = controller.Enabled
                    ? "桌宠已导入，正在桌面显示。"
                    : "桌宠已导入。勾选「显示桌宠」即可使用。";
                Refresh();
            }
            catch (Exception ex)
            {
                status.Text = "导入失败 · " + ex.Message;
            }
            finally
            {
                importing = false;
                imports.IsEnabled = true;
            }
        }
    }
}
