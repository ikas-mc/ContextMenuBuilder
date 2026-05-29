using ContextMenuBuilder.Core.View.Common;
using ContextMenuBuilder.Core.View.Controls;
using ContextMenuCustomApp.Common;
using ContextMenuCustomApp.Service.Menu;
using ContextMenuCustomApp.View.Menu;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace ContextMenuBuilder
{
    public sealed partial class MenuPage : Page
    {
        private string? _backupFolderPath;
        private string? _menusFolderPath;
        private MenuPackageView? _package;
        public AppLang AppLang { get; private set; }
        public Settings AppSetting { get; private set; }

        public MenuPageViewModel ViewModel
        {
            get { return (MenuPageViewModel)GetValue(ViewModelProperty); }
            private set { SetValue(ViewModelProperty, value); }
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(MenuItem), typeof(MenuPageViewModel), typeof(MenuPage), new PropertyMetadata(null));

        public MenuPage()
        {
            AppLang = AppContext.AppLang;
            AppSetting = AppContext.AppSettings;
            NavigationCacheMode = NavigationCacheMode.Disabled;
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _menusFolderPath = null;
            _backupFolderPath = null;
            _package = e.Parameter as MenuPackageView;
            if (null != _package && _package.IdFamilyName is string familyName)
            {
                PageHeader.Header = $"{_package.ApplicationDisplayName} ({_package.IdName})";

                _menusFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", familyName, "LocalState", "custom_commands");
                Directory.CreateDirectory(_menusFolderPath);
                var menusFolder = await StorageFolder.GetFolderFromPathAsync(_menusFolderPath);
                ViewModel = new MenuPageViewModel(new MenuService(menusFolder), true);
                this.RegisterMessageHandler(ViewModel);

                var root = AppContext.AppSettings.MenuBackupPath;
                if (!string.IsNullOrWhiteSpace(root))
                {
                    _backupFolderPath = Path.Combine(root, _package.IdName);
                    Directory.CreateDirectory(_backupFolderPath);
                }
            }

            if (null != ViewModel)
            {
                await ViewModel.LoadAsync();
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Clear();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = CommandList.SelectedItem as MenuItem;
            await ViewModel.LoadAsync();
            if (null != selectedItem?.File)
            {
                CommandList.SelectedItem = ViewModel.MenuItems.FirstOrDefault(item => Equals(selectedItem.File.Path, item.File.Path));
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var item = ViewModel.CreateMenu();
            CommandList.SelectedItem = item;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (GetSeletedMenu(true, out MenuItem menuItem))
            {
                await ViewModel.SaveAsync(menuItem);
                if (null != menuItem.File)
                {
                    CommandList.SelectedItem = ViewModel.MenuItems.FirstOrDefault(menu => Equals(menuItem.File.Path, menu.File.Path));
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (GetSeletedMenu(true, out MenuItem menuItem))
            {
                var appLang = ViewModel.AppLang;
                var result = await ChooseAsync("Delete Menu ?", appLang.CommonWarning, appLang.CommonOk, appLang.CommonCancel);
                if (result)
                {
                    await ViewModel.DeleteAsync(menuItem);
                }
            }
        }

        public async Task<bool> ChooseAsync(string content, string title = "", string primaryButton = "Ok", string closeButton = "Cancel")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                PrimaryButtonText = primaryButton,
                CloseButtonText = closeButton,
                DefaultButton = ContentDialogButton.Primary,
                Content = content,
                XamlRoot = this.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async void Open_Folder_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.OpenMenusFolderAsync();
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            if (GetSeletedMenu(true, out MenuItem menuItem))
            {
                await ViewModel.OpenMenuFileAsync(menuItem);
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (GetSeletedMenu(true, out MenuItem menuItem))
            {
                var file = menuItem.File;
                if (file == null)
                {
                    // this.ShowMessage("Menu is not saved", MessageType.Warning);
                    return;
                }

                var dialog = new MenuFileRenameDialog(menuItem);
                (bool result, string name) = await dialog.ShowAsync();
                if (result)
                {
                    await ViewModel.RenameMenuFile(menuItem, name);
                }
            }
        }

        private void OpenHelp_Click(object sender, RoutedEventArgs e)
        {
            _ = Launcher.LaunchUriAsync(new Uri("https://github.com/ikas-mc/ContextMenuForWindows11/wiki"));
        }

        private async void Refresh_Menu_Click(object sender, RoutedEventArgs e)
        {
            if (GetSeletedMenu(true, out MenuItem menuItem) && menuItem.File is StorageFile)
            {
                await ViewModel.RefreshMenuAsync(menuItem);
            }
        }

        private async void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (GetSeletedMenu(true, out MenuItem menuItem))
            {
                var json = await ViewModel.ToJson(menuItem, true);
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                var dataPackage = new DataPackage
                {
                    RequestedOperation = DataPackageOperation.Copy
                };
                dataPackage.SetText(json);
                Clipboard.SetContent(dataPackage);
                this.ShowMessage("Copy To Clipboard Successfully", MessageType.Success);
            }

        }


        //TODO refactor
        private async void CopyFromClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (GetSeletedMenu(true, out MenuItem menuItem))
            {
                var json = string.Empty;
                DataPackageView dataPackageView = Clipboard.GetContent();
                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    json = await dataPackageView.GetTextAsync();
                }

                if (string.IsNullOrEmpty(json))
                {
                    this.ShowMessage("Clipboard text is empty", MessageType.Warning);
                    return;
                }

                //bad
                if (await ViewModel.UpdateMenuFromJson(menuItem, json))
                {
                    this.ShowMessage("Copy From Clipboard Successfully", MessageType.Success);
                }
            }
        }

        private bool GetSeletedMenu(bool showWarnning, out MenuItem selectedMenuItem)
        {
            if (CommandList.SelectedItem is MenuItem menuItem)
            {
                selectedMenuItem = menuItem;
                return true;
            }

            if (showWarnning)
            {
                this.ShowMessage("No selected menu", MessageType.Warning);
            }

            selectedMenuItem = null;
            return false;
        }

        private async void Enable_Click(object sender, RoutedEventArgs e)
        {
            if (GetSeletedMenu(true, out MenuItem menuItem))
            {
                var file = menuItem.File;
                if (file == null)
                {
                    this.ShowMessage("Menu is not saved", MessageType.Warning);
                    return;
                }
                await ViewModel.EnableMenuFile(menuItem, !menuItem.Enabled);
            }
        }

        private void OnSyncClicked(object sender, RoutedEventArgs e)
        {
            if (_backupFolderPath is null || _menusFolderPath is null)
            {
                this.ShowMessage(AppContext.AppLang.MenuPackageDetailStatusDirectoriesNotConfigured, MessageType.Info);
                return;
            }

            try
            {
                var destRoot = _menusFolderPath;
                Directory.CreateDirectory(destRoot);
                var files = Directory.GetFiles(_backupFolderPath, "*.json", SearchOption.TopDirectoryOnly);
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

                this.ShowMessage(string.Format(AppContext.AppLang.MenuPackageDetailStatusSyncSuccess, files.Length), MessageType.Info);
            }
            catch (Exception ex)
            {
                this.ShowMessage(string.Format(AppContext.AppLang.MenuPackageDetailStatusSyncFailed, ex.Message), MessageType.Info);
            }
        }

        private void OnBackupClicked(object sender, RoutedEventArgs e)
        {
            if (_backupFolderPath is null || _menusFolderPath is null)
            {
                this.ShowMessage(AppContext.AppLang.MenuPackageDetailStatusDirectoriesNotConfigured, MessageType.Info);
                return;
            }

            try
            {
                Directory.CreateDirectory(_backupFolderPath);
                var files = Directory.Exists(_menusFolderPath)
                    ? Directory.GetFiles(_menusFolderPath, "*.json", SearchOption.TopDirectoryOnly)
                    : Array.Empty<string>();

                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var dest = Path.Combine(_backupFolderPath, name);
                    SafeCopyText(file, dest);
                }

                this.ShowMessage(string.Format(AppContext.AppLang.MenuPackageDetailStatusBackupSuccess, files.Length), MessageType.Info);
            }
            catch (Exception ex)
            {
                this.ShowMessage(string.Format(AppContext.AppLang.MenuPackageDetailStatusBackupFailed, ex.Message), MessageType.Info);
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
                    this.ShowMessage(string.Format(AppContext.AppLang.MenuPackageDetailStatusLaunchFailed, ex.Message), MessageType.Info);
                }
            }
        }

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private static void SafeCopyText(string source, string destination)
        {
            var content = File.ReadAllText(source, Encoding.UTF8);
            File.WriteAllText(destination, content, Utf8NoBom);
        }

        private async void FolderText_Click(object sender, RoutedEventArgs e)
        {
            var path = _backupFolderPath;
            if (Directory.Exists(path))
            {
                await Launcher.LaunchFolderPathAsync(path);
            }
        }
    }
}