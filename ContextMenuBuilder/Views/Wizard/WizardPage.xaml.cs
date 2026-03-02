using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;

namespace ContextMenuBuilder
{
    public sealed partial class WizardPage : Page
    {
        private static readonly Uri WinAppCliReleasesUri = new("https://github.com/microsoft/winappcli/releases");

        public WizardPage()
        {
            InitializeComponent();
        }

        private async void OnOpenWinAppCliReleasesClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                await Launcher.LaunchUriAsync(WinAppCliReleasesUri);
            }
            catch
            {
                // ignore failures; hyperlink button at top still available
            }
        }

        private void OnNavigateToCertificatesClicked(object sender, RoutedEventArgs e)
        {
            NavigateTo("certs", typeof(CertificatesPage));
        }

        private void OnNavigateToTemplateClicked(object sender, RoutedEventArgs e)
        {
            NavigateTo("template", typeof(TemplatePage));
        }

        private void OnNavigateToSettingsClicked(object sender, RoutedEventArgs e)
        {
            NavigateTo("settings", typeof(SettingsPage));
        }

        private void OnNavigateToMenusClicked(object sender, RoutedEventArgs e)
        {
            NavigateTo("menus", typeof(MenuPackagesPage));
        }

        private void NavigateTo(string tag, Type fallbackPage)
        {
            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToTag(tag);
            }
            else
            {
                Frame.Navigate(fallbackPage);
            }
        }
    }
}
