using ContextMenuBuilder.Core.View.Common;
using ContextMenuBuilder.Modules.File;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using Windows.Storage.Pickers;

namespace ContextMenuBuilder
{
    public sealed partial class MenuPackageSettingsControl : UserControl, INotifyPropertyChanged
    {
        private readonly AppLang _appLang;
        private readonly MenuPackageSettings _settings;

        public event PropertyChangedEventHandler? PropertyChanged;

        private MenuPackageSettingsControl(MenuPackageSettings settings)
        {
            _appLang = AppContext.AppLang;
            _settings = settings;

            InitializeComponent();
        }

        public static MenuPackageSettingsControl Create(MenuPackageSettings settings)
        {
            return new MenuPackageSettingsControl(settings);
        }

        public string MenuName
        {
            get
            {
                try
                {
                    return _settings.MenuName;
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    return string.Empty;
                }
            }
            set
            {
                try
                {
                    if (_settings.MenuName != value)
                    {
                        _settings.MenuName = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MenuName)));
                    }
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        public string LightIcon
        {
            get
            {
                try
                {
                    return _settings.MenuLightIcon;
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    return string.Empty;
                }
            }
            set
            {
                try
                {
                    if (_settings.MenuLightIcon != value)
                    {
                        _settings.MenuLightIcon = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LightIcon)));
                    }
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        public string DarkIcon
        {
            get
            {
                try
                {
                    return _settings.MenuDarkIcon;
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    return string.Empty;
                }
            }
            set
            {
                try
                {
                    if (_settings.MenuDarkIcon != value)
                    {
                        _settings.MenuDarkIcon = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DarkIcon)));
                    }
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void ShowError(Exception ex)
        {
            ShellContext.UpdateMessage(true, MessageType.Error, string.Format(_appLang.MenuPackageSettingsSaveFailed, ex.Message));
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            var file = await PickerHelper.PickSingleFileAsync(picker =>
            {
                picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                string[] fileTypes = { ".dll", ".exe", ".ico", ".png", ".bmp", ".jpeg", ".jpg", ".heic", ".tif", "*" };
                foreach (var fileType in fileTypes)
                {
                    picker.FileTypeFilter.Add(fileType);
                }
            });

            if (null == file)
            {
                return;
            }

            var iconPath = file.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || file.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? $"\"{file.Path}\",0"
                : $"\"{file.Path}\"";

            switch (button.Tag)
            {
                case string tag when tag == "Dark":
                    DarkIconTextBox.Text = iconPath;
                    break;
                default:
                    LightIconTextBox.Text = iconPath;
                    break;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            switch (button.Tag)
            {
                case string tag when tag == "Dark":
                    DarkIconTextBox.Text = string.Empty;
                    break;
                default:
                    LightIconTextBox.Text = string.Empty;
                    break;
            }
        }
    }
}
