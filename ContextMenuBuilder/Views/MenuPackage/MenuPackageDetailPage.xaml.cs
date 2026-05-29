using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Windows.System;

namespace ContextMenuBuilder
{
    public sealed partial class MenuPackageDetailPage : Page
    {
        private MenuPackageView? _package;
        private string? _backupFolder;
        private string? _installedFolder;
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private AppLang Lang => AppContext.AppLang;

        public MenuPackageDetailPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _package = e.Parameter as MenuPackageView;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_package is null)
            {
                StatusText.Text = Lang.MenuPackageDetailStatusNoPackage;
                return;
            }

            PageHeader.Header = _package.ApplicationDisplayName ?? _package.DisplayName;
            ResolveFolders();
            _ = LoadMenusAsync();
        }

        private void ResolveFolders()
        {
            var root = AppContext.AppSettings.MenuBackupPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                StatusText.Text = Lang.MenuPackageDetailStatusConfigRoot;
                return;
            }

            _backupFolder = Path.Combine(root, _package!.IdName);
            Directory.CreateDirectory(_backupFolder);

            var familyName = _package.IdFamilyName;
            if (!string.IsNullOrWhiteSpace(familyName))
            {
                _installedFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", familyName, "LocalState", "custom_commands");
            }

            FolderText.Content = _backupFolder;
        }

        private async System.Threading.Tasks.Task LoadMenusAsync(string? selectFileName = null)
        {
            if (_installedFolder is null)
            {
                StatusText.Text = Lang.MenuPackageDetailStatusCustomCommandsMissing;
                return;
            }

            try
            {
                var files = Directory.Exists(_installedFolder)
                    ? Directory.GetFiles(_installedFolder, "*.json", SearchOption.TopDirectoryOnly)
                    : Array.Empty<string>();

                var items = new List<MenuConfigItem>();
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    items.Add(CreateMenuConfigItem(file, name));
                }

                var ordered = items
                    .OrderBy(i => i.Index)
                    .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                MenuList.ItemsSource = ordered;
                StatusText.Text = string.Format(Lang.MenuPackageDetailStatusMenuCount, ordered.Count);

                if (!string.IsNullOrWhiteSpace(selectFileName))
                {
                    var target = ordered.FirstOrDefault(i => string.Equals(i.FileName, selectFileName, StringComparison.OrdinalIgnoreCase));
                    if (target is not null)
                    {
                        MenuList.SelectedItem = target;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Lang.MenuPackageDetailStatusReadFailed, ex.Message);
            }
        }

        private void OnSyncClicked(object sender, RoutedEventArgs e)
        {
            if (_package is null || _backupFolder is null || _installedFolder is null)
            {
                StatusText.Text = Lang.MenuPackageDetailStatusDirectoriesNotConfigured;
                return;
            }

            var destRoot = _installedFolder;
            try
            {
                Directory.CreateDirectory(destRoot);
                var files = Directory.GetFiles(_backupFolder, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var dest = Path.Combine(destRoot, name);
                    SafeCopyText(file, dest);
                }

                StatusText.Text = string.Format(Lang.MenuPackageDetailStatusSyncSuccess, files.Length);
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Lang.MenuPackageDetailStatusSyncFailed, ex.Message);
            }
        }

        private void OnBackupClicked(object sender, RoutedEventArgs e)
        {
            if (_package is null || _backupFolder is null || _installedFolder is null)
            {
                StatusText.Text = Lang.MenuPackageDetailStatusDirectoriesNotConfigured;
                return;
            }

            try
            {
                Directory.CreateDirectory(_backupFolder);
                var files = Directory.Exists(_installedFolder)
                    ? Directory.GetFiles(_installedFolder, "*.json", SearchOption.TopDirectoryOnly)
                    : Array.Empty<string>();

                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var dest = Path.Combine(_backupFolder, name);
                    SafeCopyText(file, dest);
                }

                StatusText.Text = string.Format(Lang.MenuPackageDetailStatusBackupSuccess, files.Length);
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Lang.MenuPackageDetailStatusBackupFailed, ex.Message);
            }
        }

        private async void OnOpenClicked(object sender, RoutedEventArgs e)
        {
            if (_package is MenuPackageView item)
            {
                try
                {
                    await MenuPackageService.LaunchPackageAsync(item.IdFullName);
                }
                catch (Exception ex)
                {
                    StatusText.Text = string.Format(Lang.MenuPackageDetailStatusLaunchFailed, ex.Message);
                }
            }
        }

        private async void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            await LoadMenusAsync();
        }

        private void OnMenuSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuList.SelectedItem is MenuConfigItem item && _installedFolder is not null)
            {
                var path = Path.Combine(_installedFolder, item.FileName);
                try
                {
                    var content = File.ReadAllText(path, Encoding.UTF8);
                    MenuFileNameBox.Text = item.FileName;
                    ContentBox.Text = content;
                    StatusText.Text = "";
                }
                catch (Exception ex)
                {
                    StatusText.Text = string.Format(Lang.MenuPackageDetailStatusReadFailed, ex.Message);
                }
            }
        }

        private void OnNewClicked(object sender, RoutedEventArgs e)
        {
            MenuList.SelectedItem = null;
            MenuFileNameBox.Text = string.Empty;
            ContentBox.Text = string.Empty;
            StatusText.Text = Lang.MenuPackageDetailStatusNewFile;
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            if (_installedFolder is null)
            {
                return;
            }

            var name = (MenuList.SelectedItem as MenuConfigItem)?.FileName;
            if (string.IsNullOrWhiteSpace(name))
            {
                StatusText.Text = Lang.MenuPackageDetailStatusSelectFile;
                return;
            }

            var path = Path.Combine(_installedFolder, name);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                StatusText.Text = Lang.MenuPackageDetailStatusDeleted;
                _ = LoadMenusAsync();
                MenuFileNameBox.Text = string.Empty;
                ContentBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Lang.MenuPackageDetailStatusDeleteFailed, ex.Message);
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            if (_installedFolder is null)
            {
                StatusText.Text = Lang.MenuPackageDetailStatusDirectoriesNotConfigured;
                return;
            }

            var name = MenuFileNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                StatusText.Text = Lang.MenuPackageDetailStatusEnterFileName;
                return;
            }

            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                name += ".json";
            }

            var path = Path.Combine(_installedFolder, name);
            try
            {
                File.WriteAllText(path, ContentBox.Text ?? string.Empty, Utf8NoBom);
                StatusText.Text = Lang.MenuPackageDetailStatusSaved;
                _ = LoadMenusAsync(name);
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Lang.MenuPackageDetailStatusSaveFailed, ex.Message);
            }
        }

        private static void SafeCopyText(string source, string destination)
        {
            var content = File.ReadAllText(source, Encoding.UTF8);
            File.WriteAllText(destination, content, Utf8NoBom);
        }

        private static MenuConfigItem CreateMenuConfigItem(string filePath, string fileName)
        {
            string? title = null;
            var index = int.MaxValue;

            try
            {
                using var stream = File.OpenRead(filePath);
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;
                if (root.TryGetProperty("title", out var titleProp))
                {
                    title = titleProp.GetString();
                }

                if (root.TryGetProperty("index", out var indexProp) &&
                    indexProp.ValueKind == JsonValueKind.Number &&
                    indexProp.TryGetInt32(out var parsedIndex))
                {
                    index = parsedIndex;
                }
            }
            catch
            {
                // ignore malformed json
            }

            return new MenuConfigItem(fileName, title, index);
        }

        private async void FolderText_Click(object sender, RoutedEventArgs e)
        {
            if (FolderText.Content is string path)
            {
                if (Directory.Exists(path))
                {
                    await Launcher.LaunchFolderPathAsync(path);
                }
            }
        }
    }

    public sealed record MenuConfigItem(string FileName, string? Title, int Index)
    {
        public string DisplayText => string.IsNullOrWhiteSpace(Title) ? FileName : Title!;
    }
}
