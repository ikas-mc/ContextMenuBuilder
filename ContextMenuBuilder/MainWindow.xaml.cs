using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ContextMenuBuilder
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetupWindow();
            OnLoaded();
        }

        private void SetupWindow()
        {
            this.AppWindow.TitleBar.ButtonBackgroundColor= Colors.Transparent;
            this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            //var hwnd = WindowNative.GetWindowHandle(this);
            //var window = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
            //window.Resize(new Windows.Graphics.SizeInt32 { Width = 1000, Height = 1000 });
        }
        private void OnLoaded()
        {
            if (RootNav.SelectedItem is null)
            {
                RootNav.SelectedItem = RootNav.MenuItems.FirstOrDefault();
            }

            NavigateTo((RootNav.SelectedItem as NavigationViewItem)?.Tag as string);
        }

        private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavigateTo("settings");
            }
            else {
                NavigateTo((args.SelectedItem as NavigationViewItem)?.Tag as string);
            }
        }

        private void NavigateTo(string? tag)
        {
            var pageType = tag switch
            {
                "wizard" => typeof(WizardPage),
                "certs" => typeof(CertificatesPage),
                "menus" => typeof(MenuPackagesPage),
                "template" => typeof(TemplatePage),
                "settings" => typeof(SettingsPage),
                _ => typeof(WizardPage)
            };

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void RootNav_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if(ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();  
            }
        }

        internal void NavigateToTag(string? tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            var navItem = FindNavigationItemByTag(tag);
            if (navItem is not null && !Equals(RootNav.SelectedItem, navItem))
            {
                RootNav.SelectedItem = navItem;
            }

            NavigateTo(tag);
        }

        private NavigationViewItem? FindNavigationItemByTag(string tag)
        {
            foreach (var item in RootNav.MenuItems.Concat(RootNav.FooterMenuItems))
            {
                if (item is NavigationViewItem navItem &&
                    string.Equals(navItem.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return navItem;
                }
            }

            return null;
        }
    }
}
