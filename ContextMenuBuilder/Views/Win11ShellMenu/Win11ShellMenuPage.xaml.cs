using ContextMenuBuilder.Core.View.Common;
using ContextMenuBuilder.Service.Win11ShellMenu;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace ContextMenuBuilder
{
    public sealed partial class Win11ShellMenuPage : Page
    {
        private CancellationTokenSource? _operationSource;
        private Win11ShellPackageRow? _contextRow;
        private Win11ShellComItem? _contextComItem;

        public Win11ShellMenuViewModel ViewModel { get; } = new();

        public Win11ShellMenuPage()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelOperations();
        }

        private async void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async void OnBlockToggled(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
            {
                return;
            }

            if (checkBox.Tag is not Win11ShellComItem item)
            {
                return;
            }

            if (checkBox.IsChecked is null)
            {
                return;
            }

            var blocked = !checkBox.IsChecked.Value;
            await ViewModel.SetBlockedAsync(item, blocked, ReplaceOperationSource().Token);

            if (checkBox.IsChecked == item.IsEnabled)
            {
                return;
            }

            checkBox.IsChecked = item.IsEnabled;
        }

        private void OnPackageContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            if (element.Tag is not Win11ShellPackageRow row)
            {
                return;
            }

            _contextRow = row;
            ContextMenuHelper.ShowAt(PackageContextFlyout, sender, args);
        }

        private async void OnOpenAppClicked(object sender, RoutedEventArgs e)
        {
            var row = _contextRow;
            if (row is null)
            {
                return;
            }

            await ViewModel.LaunchAsync(row, ReplaceOperationSource().Token);
        }

        private async void OnOpenInstallFolderClicked(object sender, RoutedEventArgs e)
        {
            var row = _contextRow;
            if (row is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(row.InstallPath))
            {
                return;
            }

            try
            {
                await Launcher.LaunchFolderPathAsync(row.InstallPath);
            }
            catch (Exception ex)
            {
                ViewModel.ReportStatus(string.Format(AppContext.AppLang.Win11ShellMenuStatusOpenFolderFailed, ex.Message));
            }
        }

        private void OnComItemContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            if (element.Tag is not Win11ShellComItem item)
            {
                return;
            }

            _contextComItem = item;

            var flyout = ComItemContextFlyout;
            if (flyout.Items.Count > 1)
            {
                if (item.IsEnabled)
                {
                    flyout.Items[0].Visibility = Visibility.Collapsed;
                    flyout.Items[1].Visibility = Visibility.Visible;
                }
                else
                {
                    flyout.Items[0].Visibility = Visibility.Visible;
                    flyout.Items[1].Visibility = Visibility.Collapsed;
                }
            }

            ContextMenuHelper.ShowAt(flyout, sender, args);
        }

        private async void OnComItemContextToggleClicked(object sender, RoutedEventArgs e)
        {
            var item = _contextComItem;
            if (item is null)
            {
                return;
            }

            await ViewModel.SetBlockedAsync(item, item.IsEnabled, ReplaceOperationSource().Token);
        }

        private async Task LoadAsync()
        {
            await ViewModel.LoadAsync(ReplaceOperationSource().Token);
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
