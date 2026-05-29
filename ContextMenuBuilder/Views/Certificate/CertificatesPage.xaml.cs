using ContextMenuBuilder.Modules.File;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace ContextMenuBuilder
{
    public sealed partial class CertificatesPage : Page
    {
        public CertificatesPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private string _currentPfxPath;
        private string? _importSourcePath;
        private X509Certificate2? _loadedCertificate;
        private bool _isInitializing;
        private static readonly StoreName[] TrackedStores = new[] { StoreName.TrustedPeople, StoreName.TrustedPublisher };
        private AppLang Lang => AppContext.AppLang;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;
            _currentPfxPath = AppContext.AppSettings.CertPath;

            CliSubjectBox.Text = "CN=CMC-TEST";
            CliValidDaysBox.Text = "3650";
            ImportPathText.Text = Lang.CertificatesImportNoSelection;

            var savedPassword = AppContext.AppSettings.CertPassword;
            var hasSaved = !string.IsNullOrEmpty(savedPassword);
            RememberPasswordCheckBox.IsChecked = hasSaved;
            if (hasSaved)
            {
                CurrentCertPasswordBox.Password = savedPassword;
            }

            _isInitializing = false;

            await RefreshCurrentCertificateAsync();
        }

        private async Task RefreshCurrentCertificateAsync()
        {
            if (!File.Exists(_currentPfxPath))
            {
                ShowNoCertificateState();
                return;
            }

            UpdateCertPathDisplay();
            await LoadCertificateDetailsAsync();
        }

        private void UpdateCertPathDisplay()
        {
            var exists = File.Exists(_currentPfxPath);
            var display = exists ? Path.GetFileName(_currentPfxPath) : Lang.CertificatesNoCertificatePlaceholder;
            CurrentCertPathText.Text = display;
            ToolTipService.SetToolTip(CurrentCertPathText, exists ? _currentPfxPath : null);
        }

        private void ShowNoCertificateState()
        {
            DisposeLoadedCertificate();
            CurrentCertPathText.Text = Lang.CertificatesNoCertificatePlaceholder;
            ToolTipService.SetToolTip(CurrentCertPathText, null);
            CertStatusText.Text = Lang.CertificatesStatusFileMissing;
            CertSubjectText.Text = string.Empty;
            CertExpireText.Text = string.Empty;
            CertInstalledText.Text = string.Empty;
            InstallWithCliButton.Visibility = Visibility.Collapsed;
        }

        private async Task LoadCertificateDetailsAsync()
        {
            DisposeLoadedCertificate();
            try
            {
                var password = CurrentCertPasswordBox.Password;
                _loadedCertificate = LoadCertificate(_currentPfxPath, password);
                CertStatusText.Text = Lang.CertificatesStatusLoadSuccess;
                CertSubjectText.Text = string.Format(Lang.CertificatesStatusSubjectFormat, _loadedCertificate.Subject);
                CertExpireText.Text = string.Format(Lang.CertificatesStatusExpireFormat, _loadedCertificate.NotAfter);
                var installed = await Task.Run(() => IsCertificateInstalled(_loadedCertificate));
                var installedText = installed ? Lang.CertificatesStatusInstalledYes : Lang.CertificatesStatusInstalledNo;
                CertInstalledText.Text = string.Format(Lang.CertificatesStatusInstalledFormat, installedText);
                InstallWithCliButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                DisposeLoadedCertificate();
                CertStatusText.Text = string.Format(Lang.CertificatesStatusLoadFailed, ex.Message);
                CertSubjectText.Text = string.Empty;
                CertExpireText.Text = string.Empty;
                CertInstalledText.Text = string.Empty;
                InstallWithCliButton.Visibility = Visibility.Collapsed;
            }
        }

        private static X509Certificate2 LoadCertificate(string path, string? password)
        {
            var flags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet;
            return new X509Certificate2(path, password, flags);
        }

        private static bool IsCertificateInstalled(X509Certificate2 certificate)
        {
            foreach (var store in TrackedStores)
            {
                if (IsInStore(store, StoreLocation.LocalMachine, certificate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInStore(StoreName storeName, StoreLocation location, X509Certificate2 certificate)
        {
            try
            {
                using var store = new X509Store(storeName, location);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                return store.Certificates.Cast<X509Certificate2>().Any(c => string.Equals(c.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private void DisposeLoadedCertificate()
        {
            _loadedCertificate?.Dispose();
            _loadedCertificate = null;
        }

        private async void OnReloadCertClicked(object sender, RoutedEventArgs e)
        {
            await RefreshCurrentCertificateAsync();
        }

        private void OnCurrentPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            if (RememberPasswordCheckBox.IsChecked == true)
            {
                var value = CurrentCertPasswordBox.Password;
                AppContext.AppSettings.CertPassword = value;
            }
        }

        private void OnRememberPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            if (RememberPasswordCheckBox.IsChecked == true)
            {
                AppContext.AppSettings.CertPassword = CurrentCertPasswordBox.Password;
            }
            else
            {
                AppContext.AppSettings.CertPassword = null;
            }
        }

        private async void OnPickImportPfxClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = await PickerHelper.PickSingleFileAsync(picker =>
                {
                    picker.FileTypeFilter.Add(".pfx");
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                });
                if (file is null)
                {
                    return;
                }

                _importSourcePath = file.Path;
                ImportPathText.Text = file.Name;
            }
            catch (Exception ex)
            {
                OutputBox.Text = string.Format(Lang.CertificatesStatusPickFileFailed, ex.Message);
            }
        }

        private async void OnImportCertClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_importSourcePath) || !File.Exists(_importSourcePath))
            {
                OutputBox.Text = Lang.CertificatesStatusSelectPfxFirst;
                return;
            }

            var password = ImportPasswordBox.Password;
            try
            {
                using var _ = LoadCertificate(_importSourcePath, string.IsNullOrEmpty(password) ? null : password);
            }
            catch (Exception ex)
            {
                OutputBox.Text = string.Format(Lang.CertificatesStatusValidateFailed, ex.Message);
                return;
            }

            try
            {
                BackupIfExists(_currentPfxPath);
                File.Copy(_importSourcePath, _currentPfxPath, overwrite: true);
                CurrentCertPasswordBox.Password = password ?? string.Empty;
                if (RememberPasswordCheckBox.IsChecked == true)
                {
                    AppContext.AppSettings.CertPassword = password;
                }

                OutputBox.Text = string.Format(Lang.CertificatesStatusImportSuccess, _currentPfxPath);
                await RefreshCurrentCertificateAsync();
            }
            catch (Exception ex)
            {
                OutputBox.Text = string.Format(Lang.CertificatesStatusImportFailed, ex.Message);
            }
        }

        private async void OnGenerateWithCliClicked(object sender, RoutedEventArgs e)
        {
            var subject = string.IsNullOrWhiteSpace(CliSubjectBox.Text) ? "CN=CMC-TEST" : CliSubjectBox.Text.Trim();
            var password = CliPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                password = "password";
            }

            var workingDir = Path.Combine(Path.GetTempPath(), "WinAppCliCert");
            Directory.CreateDirectory(workingDir);

            var outputFileName = "cert_tmp.pfx";
            var resolvedOutput = Path.Combine(workingDir, outputFileName);

            var validDays = 365;
            if (!string.IsNullOrWhiteSpace(CliValidDaysBox.Text) && int.TryParse(CliValidDaysBox.Text, out var parsed) && parsed > 0)
            {
                validDays = parsed;
            }

            var args = new StringBuilder("cert generate");
            args.Append($" --output \"{outputFileName}\"");
            args.Append($" --valid-days {validDays}");
            args.Append(" --if-exists Overwrite");
            args.Append(" --verbose");
            args.Append($" --publisher \"{subject}\"");
            args.Append($" --password \"{password}\"");

            OutputBox.Text = Lang.CertificatesStatusGenerateRunning;
            var cliResult = await WinAppCliService.RunAsync(args.ToString(), workingDir);
            var cliOutput = cliResult.Output;

            try
            {
                if (!File.Exists(resolvedOutput))
                {
                    OutputBox.Text = cliOutput + "\n" + Lang.CertificatesStatusGeneratedFileMissing;
                    return;
                }

                BackupIfExists(_currentPfxPath);
                File.Copy(resolvedOutput, _currentPfxPath, overwrite: true);
                CurrentCertPasswordBox.Password = password;
                if (RememberPasswordCheckBox.IsChecked == true)
                {
                    AppContext.AppSettings.CertPassword = password;
                }

                OutputBox.Text = cliOutput + "\n" + string.Format(Lang.CertificatesStatusCopySuccess, _currentPfxPath);
                await RefreshCurrentCertificateAsync();
            }
            catch (Exception ex)
            {
                OutputBox.Text = cliOutput + "\n" + string.Format(Lang.CertificatesStatusCopyFailed, ex.Message);
            }
        }

        private async void OnInstallWithCliClicked(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_currentPfxPath))
            {
                OutputBox.Text = Lang.CertificatesStatusFileMissing;
                return;
            }

            var password = CurrentCertPasswordBox.Password;
            var args = new StringBuilder("cert install");
            args.Append(' ');
            args.Append('"').Append(_currentPfxPath).Append('"');
            if (!string.IsNullOrEmpty(password))
            {
                args.Append(" --password \"").Append(password).Append('"');
            }

            OutputBox.Text = Lang.CertificatesStatusInstallCliRunning;
            var workingDir = Path.GetDirectoryName(_currentPfxPath);
            var result = await WinAppCliService.RunAsAdmin2Async(args.ToString(), workingDir);
            OutputBox.Text = result.Output;
            await RefreshCurrentCertificateAsync();
        }

        private async void OnInstallCurrentClicked(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_currentPfxPath))
            {
                OutputBox.Text = Lang.CertificatesStatusFileMissing;
                return;
            }

            var password = CurrentCertPasswordBox.Password;

            var store = StoreName.TrustedPeople;

            try
            {
                using var cert = LoadCertificate(_currentPfxPath, string.IsNullOrEmpty(password) ? null : password);
                using var storeInstance = new X509Store(store, StoreLocation.LocalMachine);
                storeInstance.Open(OpenFlags.ReadWrite);
                storeInstance.Add(cert);

                OutputBox.Text = string.Format(Lang.CertificatesStatusInstallLocalSuccess, store, _currentPfxPath);
                await RefreshCurrentCertificateAsync();
            }
            catch (Exception ex)
            {
                OutputBox.Text = string.Format(Lang.CertificatesStatusInstallFailed, ex.Message);
            }
        }

        private void OnOpenCertManagerClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var info = new ProcessStartInfo("certlm.msc")
                {
                    UseShellExecute = true
                };
                Process.Start(info);
            }
            catch (Exception ex)
            {
                OutputBox.Text = string.Format(Lang.CertificatesStatusOpenManagerFailed, ex.Message);
            }
        }

        private static void BackupIfExists(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(path);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupName = Path.Combine(directory, $"{fileName}_{timestamp}.bak");

            File.Move(path, backupName, overwrite: false);
        }

        private async void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is Pivot pivot && pivot.SelectedIndex == 0)
            {
                await RefreshCurrentCertificateAsync();
            }
        }
    }
}
