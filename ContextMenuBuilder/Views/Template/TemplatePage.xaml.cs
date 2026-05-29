using ContextMenuBuilder.Modules.File;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace ContextMenuBuilder
{
    public sealed partial class TemplatePage : Page
    {
        private readonly string _templateFolder;
        private string? _importFilePath;
        private static readonly Uri TemplateDownloadUri = new("https://github.com/ikas-mc/ContextMenuForWindows11/releases/");
        private AppLang Lang => AppContext.AppLang;

        public TemplatePage()
        {
            InitializeComponent();
            _templateFolder = AppContext.AppSettings.MenuPackageTemplatePath;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await RefreshTemplateInfoAsync();
        }

        private async Task RefreshTemplateInfoAsync()
        {
            try
            {
                if (!Directory.Exists(_templateFolder) || !Directory.EnumerateFileSystemEntries(_templateFolder).Any())
                {
                    InfoStatusText.Text = Lang.TemplateStatusNoTemplate;
                    ManifestInfoPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                var manifestPath = Path.Combine(_templateFolder, "AppxManifest.xml");
                if (!File.Exists(manifestPath))
                {
                    InfoStatusText.Text = Lang.TemplateStatusMissingManifest;
                    ManifestInfoPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                var doc = XDocument.Load(manifestPath);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                var identity = doc.Root?.Element(ns + "Identity");
                if (identity is null)
                {
                    InfoStatusText.Text = Lang.TemplateStatusMissingIdentity;
                    ManifestInfoPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                var name = identity.Attribute("Name")?.Value ?? "-";
                var publisher = identity.Attribute("Publisher")?.Value ?? "-";
                var version = identity.Attribute("Version")?.Value ?? "-";
                var arch = identity.Attribute("ProcessorArchitecture")?.Value ?? "-";

                var uapNs = doc.Root?.GetNamespaceOfPrefix("uap") ?? "http://schemas.microsoft.com/appx/manifest/uap/windows10";
                var visual = doc.Descendants(uapNs + "VisualElements").FirstOrDefault();
                var displayName = visual?.Attribute("DisplayName")?.Value ?? "-";

                IdentityText.Text = $"Name: {name}\nPublisher: {publisher}\nVersion: {version}\nProcessorArchitecture: {arch}";
                DisplayNameText.Text = displayName;
                ManifestInfoPanel.Visibility = Visibility.Visible;
                InfoStatusText.Text = Lang.TemplateStatusLoaded;
            }
            catch (Exception ex)
            {
                InfoStatusText.Text = string.Format(Lang.TemplateStatusReadFailed, ex.Message);
                ManifestInfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void OnOpenTemplateFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_templateFolder);
                await Launcher.LaunchFolderPathAsync(_templateFolder);
            }
            catch (Exception ex)
            {
                InfoStatusText.Text = string.Format(Lang.TemplateStatusOpenFolderFailed, ex.Message);
            }
        }

        private async void OnDownloadTemplateClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var launched = await Launcher.LaunchUriAsync(TemplateDownloadUri);
                if (!launched)
                {
                    ImportStatusText.Text = string.Format(Lang.TemplateStatusDownloadFailed, "Unknown error");
                }
            }
            catch (Exception ex)
            {
                ImportStatusText.Text = string.Format(Lang.TemplateStatusDownloadFailed, ex.Message);
            }
        }

        private async void OnPickTemplateFileClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = await PickerHelper.PickSingleFileAsync(picker =>
                {
                    picker.FileTypeFilter.Add(".msix");
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                });
                if (file is null)
                {
                    return;
                }

                _importFilePath = file.Path;
                ImportFileText.Text = file.Name;
                ImportStatusText.Text = string.Empty;
            }
            catch (Exception ex)
            {
                ImportStatusText.Text = string.Format(Lang.TemplateStatusPickFileFailed, ex.Message);
            }
        }

        private async void OnImportTemplateClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_importFilePath) || !File.Exists(_importFilePath))
            {
                ImportStatusText.Text = Lang.TemplateStatusSelectFileFirst;
                return;
            }

            try
            {
                ImportStatusText.Text = Lang.TemplateStatusImporting;
                await Task.Run(() => ImportTemplateInternal(_importFilePath!));
                ImportStatusText.Text = Lang.TemplateStatusImportCompleted;
                await RefreshTemplateInfoAsync();
            }
            catch (Exception ex)
            {
                ImportStatusText.Text = string.Format(Lang.TemplateStatusImportFailed, ex.Message);
            }
        }

        private void ImportTemplateInternal(string sourceFile)
        {
            if (Directory.Exists(_templateFolder))
            {
                CreateTemplateBackup();
                CleanupOldBackups();
                Directory.Delete(_templateFolder, true);
            }

            Directory.CreateDirectory(_templateFolder);
            ZipFile.ExtractToDirectory(sourceFile, _templateFolder, overwriteFiles: true);
            RemovePackageArtifacts(_templateFolder);
            AppContext.AppSettings.MenuPackageTemplatePath = _templateFolder;
        }

        private string CreateTemplateBackup()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"template-backup-{timestamp}.zip");
            ZipFile.CreateFromDirectory(_templateFolder, backupPath, CompressionLevel.SmallestSize, includeBaseDirectory: false);
            return backupPath;
        }

        private void CleanupOldBackups()
        {
            try
            {
                var dir = ApplicationData.Current.LocalFolder.Path;
                var backups = Directory.GetFiles(dir, "template-backup-*.zip", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetCreationTimeUtc)
                    .ToList();

                foreach (var file in backups.Skip(2))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // ignore cleanup errors
            }
        }

        private static void RemovePackageArtifacts(string folder)
        {
            try
            {
                var metadataPath = Path.Combine(folder, "AppxMetadata");
                if (Directory.Exists(metadataPath))
                {
                    Directory.Delete(metadataPath, true);
                }

                var cleanupFiles = new[] { "[Content_Types].xml", "AppxBlockMap.xml", "AppxSignature.p7x" };
                foreach (var file in cleanupFiles)
                {
                    var path = Path.Combine(folder, file);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
            catch
            {
                // ignore cleanup failures
            }
        }

        private async void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is Pivot pivot && pivot.SelectedIndex == 0)
            {
                await RefreshTemplateInfoAsync();
            }
        }
    }
}
