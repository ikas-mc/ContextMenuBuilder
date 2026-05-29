using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ContextMenuBuilder
{
    public sealed partial class MenuPackageEditorNewPage : Page
    {
        private MenuPackageView? _package;
        private string? _lastPackagePath;
        private AppLang Lang => AppContext.AppLang;

        public MenuPackageEditorNewPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _package = e.Parameter as MenuPackageView;
        }

        // ── Load ─────────────────────────────────────────────────────

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AppxManifestInfo? info;

                if (null != _package)
                {
                    info = await Task.Run(() => MenuPackageEditorService.ReadInstalledManifestInfo(_package.IdFullName));
                    if (null == info)
                    {
                        OutputBox.Text = $"Failed to read ManifestInfo from installted package: {_package?.IdName}";
                    }
                }
                else
                {
                    info = await Task.Run(() => MenuPackageEditorService.LoadTemplateManifestInfo());
                    if (null == info)
                    {
                        OutputBox.Text = $"Failed to read ManifestInfo from template";
                    }
                }

                if (null != info)
                {
                    var templatePath = AppContext.AppSettings.MenuPackageTemplatePath ?? string.Empty;
                    var currentVersion = info?.IdVersion ?? "1.0.1.0";

                    InputFolderText.Text = templatePath;
                    PackageIdBox.Text = _package?.IdName ?? AppContext.AppSettings.MenuPackageIdPrefix ?? string.Empty;
                    PackageIdBox.IsEnabled = _package is null;
                    VersionBox.Text = MenuPackageEditorService.IncrementBuild(currentVersion);
                    AppDisplayNameBox.Text = info?.ApplicationDisplayName ?? string.Empty;
                    StartMenuCheckBox.IsChecked = info?.AppListEntry ?? true;
                }
            }
            catch (Exception ex)
            {
                OutputBox.Text = $"Failed to initialize: {ex.Message}";
            }
        }

        // ── Generate ─────────────────────────────────────────────────

        private void OnGenerateClicked(object sender, RoutedEventArgs e) => _ = GenerateAsync();

        private async Task GenerateAsync()
        {
            var id = PackageIdBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                OutputBox.Text = Lang.MenuPackageEditorStatusEnterPackageId;
                return;
            }

            var version = VersionBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                OutputBox.Text = Lang.MenuPackageEditorStatusVersionRequired;
                return;
            }

            var displayName = AppDisplayNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                OutputBox.Text = Lang.MenuPackageEditorStatusDisplayNameRequired;
                return;
            }


            string? classId = null;
            if (_package != null)
            {
                try
                {
                    classId = await Task.Run(() => MenuPackageEditorService.ReadInstalledManifestInfo(_package.IdFullName).ClassId);
                }
                catch (Exception e)
                {
                    OutputBox.Text = e.Message;
                }

                OutputBox.Text = $"current classId is {classId}";
            }
            if (string.IsNullOrEmpty(classId))
            {
                classId = Guid.NewGuid().ToString();
            }

            var manifest = new AppxManifestInfo
            {
                IdName = id,
                IdVersion = version,
                ApplicationDisplayName = displayName,
                AppListEntry = StartMenuCheckBox.IsChecked ?? true,
                ClassId = classId,   // null → service reads from template
            };

            OutputBox.Text = string.Empty;

            try
            {
                _lastPackagePath = await MenuPackageEditorService.GenerateAsync(
                    manifest,
                    log: msg => DispatcherQueue.TryEnqueue(() => OutputBox.Text += msg + "\n"));
            }
            catch (Exception ex)
            {
                OutputBox.Text += string.Format(Lang.MenuPackageEditorGeneratepackageFailed, ex.Message);
            }
        }

        // ── Install ──────────────────────────────────────────────────

        private void OnInstallClicked(object sender, RoutedEventArgs e) => _ = InstallAsync();

        private async Task InstallAsync()
        {
            if (_lastPackagePath is null || !File.Exists(_lastPackagePath))
            {
                OutputBox.Text = Lang.MenuPackageEditorStatusGenerateFirst;
                return;
            }

            try
            {
                OutputBox.Text = Lang.MenuPackageEditorStatusInstalling;
                await MenuPackageEditorService.InstallAsync(_lastPackagePath, PackageIdBox.Text?.Trim() ?? string.Empty);
                OutputBox.Text = Lang.MenuPackageEditorStatusInstallSuccess;
            }
            catch (Exception ex)
            {
                OutputBox.Text = string.Format(Lang.MenuPackageEditorStatusInstallFailed, ex.Message);
            }
        }
    }
}
