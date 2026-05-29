using ContextMenuBuilder.Modules.File;
using ContextMenuBuilder.Modules.Lang;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using System;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace ContextMenuBuilder
{
    public sealed partial class SettingsPage : Page
    {
        private SettingViewModel _settingViewModel;
        public SettingsPage()
        {
            _settingViewModel = new SettingViewModel();
            InitializeComponent();
            LanguageOverrideComboBox.SelectionChanged += LanguageOverrideComboBox_SelectionChanged;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await _settingViewModel.LoadLanguages();
            LanguageOverrideComboBox.SelectedItem = _settingViewModel.GetCurrentLang();
        }

        private async void OpenDataFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder f = ApplicationData.Current.LocalFolder;
            await Launcher.LaunchFolderAsync(f);
        }

        private async void ClearTempFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            await _settingViewModel.ClearTempFolder();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            await _settingViewModel.ImportLang();
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            await _settingViewModel.ExportLang();
        }

        private async void RestartAppButton_Click(object sender, RoutedEventArgs e)
        {
            AppRestartFailureReason restartError = AppInstance.Restart("");

            switch (restartError)
            {
                case AppRestartFailureReason.RestartPending:
                    //SendToast("Another restart is currently pending.");
                    break;
                case AppRestartFailureReason.InvalidUser:
                    //SendToast("Current user is not signed in or not a valid user.");
                    break;
                case AppRestartFailureReason.Other:
                    //SendToast("Failure restarting.");
                    break;
            }
        }

        // Backup core logic is in BackupService

        private void LanguageOverrideComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem is LangInfo langInfo)
            {
                _settingViewModel.UpdateLangSetting(langInfo);
            }
        }

        private async void OnPickWinAppCliPathClicked(object sender, RoutedEventArgs e)
        {
            var file = await PickerHelper.PickSingleFileAsync(picker =>
            {
                picker.SuggestedStartLocation = PickerLocationId.Desktop;
                picker.FileTypeFilter.Add(".exe");
                picker.FileTypeFilter.Add("*");
            });

            if (file is null)
            {
                return;
            }

            WinAppCliPathBox.Text = file.Path;
            AppContext.AppSettings.WinAppCliPath = file.Path;
        }

        private async void OnPickMenuConfigRootClicked(object sender, RoutedEventArgs e)
        {
            var folder = await PickerHelper.PickFolderAsync(picker =>
            {
                picker.SuggestedStartLocation = PickerLocationId.Desktop;
            });

            if (folder is null)
            {
                return;
            }

            MenuConfigRootBox.Text = folder.Path;
            AppContext.AppSettings.MenuBackupPath = folder.Path;
        }

        private async void OnPickMenuPackageInputRootClicked(object sender, RoutedEventArgs e)
        {
            var folder = await PickerHelper.PickFolderAsync(picker =>
            {
                picker.SuggestedStartLocation = PickerLocationId.Desktop;
            });

            if (folder is null)
            {
                return;
            }

            MenuPackageInputRootBox.Text = folder.Path;
            AppContext.AppSettings.MenuPackageTemplatePath = folder.Path;
        }
    }
}
