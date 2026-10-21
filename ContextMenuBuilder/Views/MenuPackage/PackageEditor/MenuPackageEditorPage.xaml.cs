using ContextMenuBuilder.Service.MenuPackage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace ContextMenuBuilder.Views.MenuPackage.PackageEditor
{
    public sealed partial class MenuPackageEditorPage : Page
    {
        private MenuPackageInfo? _package;
        private CancellationTokenSource? _operationSource;

        public MenuPackageEditorViewModel ViewModel { get; }

        public MenuPackageEditorPage()
        {
            ViewModel = new MenuPackageEditorViewModel();
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _package = e.Parameter as MenuPackageInfo;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            await ViewModel.InitializeAsync(_package, source.Token);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelOperations();
        }

        private async void OnGenerateClicked(object sender, RoutedEventArgs e)
        {
            var request = new MenuPackageBuildRequest
            {
                PackageId = PackageIdBox.Text,
                Version = VersionBox.Text,
                DisplayName = AppDisplayNameBox.Text,
                AddToStartMenu = StartMenuSwitch.IsOn,
                SourcePackageFullName = _package?.IdFullName,
            };

            var source = ReplaceOperationSource();
            await ViewModel.GenerateAsync(request, source.Token);
        }

        private async void OnInstallClicked(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            await ViewModel.InstallAsync(source.Token);
        }

        private async void OnOpenOutputFolderClicked(object sender, RoutedEventArgs e)
        {
            var folder = ViewModel.OutputFolderPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            await Launcher.LaunchFolderPathAsync(folder);
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
