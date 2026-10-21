using ContextMenuBuilder.Core.View.Common;
using ContextMenuBuilder.Service.MenuPackage;
using ContextMenuBuilder.Views.MenuPackage.PackageEditor;
using ContextMenuBuilder.Views.MenuPackage.PackageMenu;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace ContextMenuBuilder.Views.MenuPackage.PackageList
{
    public sealed partial class MenuPackagesPage : Page
    {
        private CancellationTokenSource? _operationSource;
        private bool _isActive;

        public MenuPackagesViewModel ViewModel { get; }

        public MenuPackagesPage()
        {
            ViewModel = new MenuPackagesViewModel();
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private AppLang Lang => AppContext.AppLang;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isActive = true;

            var source = ReplaceOperationSource();
            await ViewModel.InitializeAsync(source.Token);

            if (!_isActive)
            {
                return;
            }

            PrefixBox.Text = ViewModel.Prefix;
            AttachInteractionEvents();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isActive = false;
            DetachInteractionEvents();
            CancelOperations();
        }

        private void AttachInteractionEvents()
        {
            DetachInteractionEvents();
            PackagesList.SelectionChanged += OnSelectionChanged;
        }

        private void DetachInteractionEvents()
        {
            PackagesList.SelectionChanged -= OnSelectionChanged;
        }

        private async void OnQueryClicked(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            await QueryAsync(source.Token);
        }

        private async void OnPrefixKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            e.Handled = true;

            var source = ReplaceOperationSource();
            await QueryAsync(source.Token);
        }

        private void OnOpenMenusClicked(object sender, RoutedEventArgs e)
        {
            OpenMenusFromSelection();
        }

        private async Task QueryAsync(CancellationToken cancellationToken)
        {
            var selected = ViewModel.SelectedPackage;
            var succeeded = await ViewModel.QueryAsync(PrefixBox.Text, cancellationToken);
            if (!succeeded)
            {
                return;
            }

            if (selected is null)
            {
                return;
            }

            foreach (var package in ViewModel.Packages)
            {
                if (!string.Equals(package.IdFullName, selected.IdFullName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                PackagesList.SelectedItem = package;
                break;
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not MenuPackageInfo package)
            {
                return;
            }

            NavigateToMenus(package);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.Select(PackagesList.SelectedItem as MenuPackageInfo);
        }

        private void OnAddClicked(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MenuPackageEditorPage), null);
        }

        private void OnUpgradeClicked(object sender, RoutedEventArgs e)
        {
            var package = ViewModel.SelectedPackage;
            if (package is null)
            {
                ViewModel.ReportSelectionMissing(MenuPackageSelectionPurpose.Upgrade);
                return;
            }

            Frame.Navigate(typeof(MenuPackageEditorPage), package);
        }

        private async void OnUninstallClicked(object sender, RoutedEventArgs e)
        {
            var package = ViewModel.SelectedPackage;
            if (package is null)
            {
                ViewModel.ReportSelectionMissing(MenuPackageSelectionPurpose.Uninstall);
                return;
            }

            var confirmed = await ConfirmAsync(
                Lang.MenuPackageUninstallConfirmTitle,
                string.Format(Lang.MenuPackageUninstallConfirmContent, package.ApplicationDisplayName),
                Lang.CommonOk,
                Lang.CommonCancel);

            if (!confirmed)
            {
                return;
            }

            var source = ReplaceOperationSource();
            await ViewModel.UninstallAsync(package, source.Token);
        }

        private async void OnOpenClicked(object sender, RoutedEventArgs e)
        {
            var package = ViewModel.SelectedPackage;
            if (package is null)
            {
                ViewModel.ReportSelectionMissing(MenuPackageSelectionPurpose.Open);
                return;
            }

            var source = ReplaceOperationSource();
            await ViewModel.LaunchAsync(package, source.Token);
        }

        private void ContextMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            if (element.DataContext is not MenuPackageInfo package)
            {
                return;
            }

            PackagesList.SelectedItem = package;
            ContextMenuHelper.ShowAt(ContextMenu, element);
        }

        private void PackagesList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.OriginalSource is not FrameworkElement element)
            {
                return;
            }

            if (element.DataContext is not MenuPackageInfo package)
            {
                return;
            }

            PackagesList.SelectedItem = package;
            ContextMenuHelper.ShowAt(ContextMenu, sender, args);
        }

        private void ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem menuItem)
            {
                return;
            }

            var command = menuItem.Tag as string;
            switch (command)
            {
                case "menus":
                    OpenMenusFromSelection();
                    return;
                case "open":
                    OnOpenClicked(sender, e);
                    return;
                case "upgrade":
                    OnUpgradeClicked(sender, e);
                    return;
                case "delete":
                    OnUninstallClicked(sender, e);
                    return;
                default:
                    return;
            }
        }

        private void OpenMenusFromSelection()
        {
            var package = ViewModel.SelectedPackage;
            if (package is null)
            {
                ViewModel.ReportSelectionMissing(MenuPackageSelectionPurpose.OpenMenus);
                return;
            }

            NavigateToMenus(package);
        }

        private void NavigateToMenus(MenuPackageInfo package)
        {
            Frame.Navigate(typeof(MenuPage), package);
        }

        private async Task<bool> ConfirmAsync(string title, string content, string primaryButtonText, string closeButtonText)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = closeButtonText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private CancellationTokenSource ReplaceOperationSource()
        {
            CancelOperations();
            var source = new CancellationTokenSource();
            _operationSource = source;
            return source;
        }

        private void CancelOperations()
        {
            var source = _operationSource;
            if (source is null)
            {
                return;
            }

            _operationSource = null;
            source.Cancel();
            source.Dispose();
        }
    }
}
