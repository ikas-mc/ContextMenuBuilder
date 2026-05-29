using ContextMenuBuilder.Core.View.Common;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using Windows.Storage;

namespace ContextMenuBuilder
{
    public sealed partial class MenuPackagesPage : Page
    {
        private const string DefaultPrefix = "CMC.";
        private AppLang Lang => AppContext.AppLang;

        public MenuPackagesPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var storedPrefix = AppContext.AppSettings.MenuPackageIdPrefix;
            PrefixBox.Text = string.IsNullOrWhiteSpace(storedPrefix) ? DefaultPrefix : storedPrefix;
            _ = QueryAsync();
        }

        private async void OnQueryClicked(object sender, RoutedEventArgs e)
        {
            await QueryAsync();
        }

        private ObservableCollection<MenuPackageView> _packages = new ObservableCollection<MenuPackageView>();

        private async System.Threading.Tasks.Task QueryAsync()
        {
            var prefix = PrefixBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(prefix))
            {
                _packages.Clear();
                //PackagesList.ItemsSource = Array.Empty<MenuPackageView>();
                StatusText.Text = Lang.MenuPackageStatusEnterPrefix;
                return;
            }

            try
            {
                var packages = await MenuPackageService.QueryPackagesAsync(prefix);
                _packages.Clear();
                foreach (var item in packages)
                {
                    _packages.Add(item);
                }
                StatusText.Text = string.Empty;
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Lang.MenuPackageStatusQueryFailed, ex.Message);
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MenuPackageView item)
            {
                Frame.Navigate(typeof(MenuPage), item);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PackagesList.SelectedItem is MenuPackageView item)
            {
                StatusText.Text = string.Format(Lang.MenuPackageStatusSelected, item.IdName);
            }
            else
            {
                StatusText.Text = string.Empty;
            }
        }

        private void OnAddClicked(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MenuPackageEditorNewPage), null);
        }

        private void OnUpgradeClicked(object sender, RoutedEventArgs e)
        {
            if (PackagesList.SelectedItem is not MenuPackageView item)
            {
                StatusText.Text = Lang.MenuPackageStatusSelectUpgrade;
                return;
            }

            Frame.Navigate(typeof(MenuPackageEditorNewPage), item);
        }

        private async void OnUninstallClicked(object sender, RoutedEventArgs e)
        {
            if (PackagesList.SelectedItem is not MenuPackageView item)
            {
                StatusText.Text = Lang.MenuPackageStatusSelectUninstall;
                return;
            }

            try
            {
                StatusText.Text = Lang.MenuPackageStatusUninstalling;
                await MenuPackageService.RemovePackageAsync(item.IdFullName);
                StatusText.Text = Lang.MenuPackageStatusUninstallFinished;

                // 清理记录文件
                var recordPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "menuPackage", item.IdName);
                if (File.Exists(recordPath))
                {
                    File.Delete(recordPath);
                }

                await QueryAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Lang.MenuPackageStatusUninstallFailed, ex.Message);
            }
        }

        private async void OnOpenClicked(object sender, RoutedEventArgs e)
        {
            if (PackagesList.SelectedItem is not MenuPackageView item)
            {
                StatusText.Text = Lang.MenuPackageStatusSelectOpen;
                return;
            }

            try
            {
                await MenuPackageService.LaunchPackageAsync(item.IdFullName);
                StatusText.Text = Lang.MenuPackageStatusOpened;
            }
            catch (System.Exception ex)
            {
                StatusText.Text = string.Format(Lang.MenuPackageStatusOpenFailed, ex.Message);
            }
        }

        private void ContextMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: MenuPackageView menuPackageView } element)
            {
                PackagesList.SelectedItem = menuPackageView;
                ShowContextMenu(menuPackageView, element);
            }
        }

        private void PackagesList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.OriginalSource is FrameworkElement element && element.DataContext is MenuPackageView menuPackageView)
            {
                PackagesList.SelectedItem = menuPackageView;
                ShowContextMenu(menuPackageView, sender, args);
            }
        }

        private void ShowContextMenu(MenuPackageView menuPackageView, UIElement placementTarget, ContextRequestedEventArgs? args = null)
        {

            if (null != args)
            {
                ContextMenuHelper.ShowAt(ContextMenu, placementTarget, args);
            }
            else
            {
                ContextMenuHelper.ShowAt(ContextMenu, placementTarget);
            }
        }

        private async void ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            var menuPackageView = PackagesList.SelectedItem as MenuPackageView;

            if (sender is MenuFlyoutItem menuFlyoutItem && null != menuPackageView)
            {
                var type = menuFlyoutItem.Tag as string;

                switch (type)
                {
                    case "menus":
                        {
                            Frame.Navigate(typeof(MenuPage), menuPackageView);
                            break;
                        }
                    case "open":
                        {
                            OnOpenClicked(sender, e);
                            break;
                        }
                    case "upgrade":
                        {
                            OnUpgradeClicked(sender, e);
                            break;
                        }
                    case "delete":
                        {
                            OnUninstallClicked(sender, e);
                            break;
                        }
                }
            }
        }

    }

    public partial record MenuPackageView(string IdName, string IdFullName, string DisplayName, string IdPublisher, string IdVersion, string IdFamilyName, string ApplicationDisplayName)
    {
    }
}
