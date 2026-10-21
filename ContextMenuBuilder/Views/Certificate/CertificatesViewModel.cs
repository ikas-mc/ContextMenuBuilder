using CommunityToolkit.Mvvm.ComponentModel;
using ContextMenuBuilder.Core.Log;
using ContextMenuBuilder.Service.Certificate;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ContextMenuBuilder
{
    public sealed partial class CertificatesViewModel : ObservableObject
    {
        private readonly CertificateService _service = new();
        private readonly AppLang _lang = AppContext.AppLang;
        private readonly ILogger _logger = AppLog.For<CertificatesViewModel>();

        private string _importSourcePath = string.Empty;
        private string _currentPassword = string.Empty;

        [ObservableProperty]
        public partial string CertificateFileName
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string CertificateFullPath
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string SubjectText
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string ExpireText
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string TrustStatusText
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial bool CanInstall
        {
            get; private set;
        }

        public Visibility InstallButtonVisibility => CanInstall ? Visibility.Visible : Visibility.Collapsed;

        [ObservableProperty]
        public partial bool RememberPassword
        {
            get; set;
        }

        [ObservableProperty]
        public partial bool IsBusy
        {
            get; private set;
        }

        public bool IsNotBusy => !IsBusy;

        [ObservableProperty]
        public partial string ImportFileName
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string Message
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial InfoBarSeverity MessageSeverity
        {
            get; private set;
        } = InfoBarSeverity.Informational;

        [ObservableProperty]
        public partial bool IsMessageOpen
        {
            get; private set;
        }

        public string InitialPassword => _currentPassword;

        partial void OnCanInstallChanged(bool value)
        {
            OnPropertyChanged(nameof(InstallButtonVisibility));
        }

        partial void OnRememberPasswordChanged(bool value)
        {
            if (value)
            {
                _service.RememberPassword(_currentPassword);
                return;
            }

            _service.RememberPassword(null);
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotBusy));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            ImportFileName = _lang.CertificatesImportNoSelection;

            var remembered = _service.LoadRememberedPassword();
            if (remembered is null)
            {
                _currentPassword = string.Empty;
                RememberPassword = false;
            }
            else
            {
                _currentPassword = remembered;
                RememberPassword = true;
            }

            OnPropertyChanged(nameof(InitialPassword));

            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }

        public void SetCurrentPassword(string? password)
        {
            _currentPassword = password ?? string.Empty;

            if (!RememberPassword)
            {
                return;
            }

            _service.RememberPassword(_currentPassword);
        }

        public async Task<bool> ReloadAsync(CancellationToken cancellationToken)
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;

            try
            {
                var info = await _service.LoadCurrentAsync(_currentPassword, cancellationToken).ConfigureAwait(true);
                ApplyInfo(info);

                if (info is null)
                {
                    ReportWarning(_lang.CertificatesStatusFileMissing);
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Certificate reload canceled");
                return false;
            }
            catch (CertificateException e)
            {
                _logger.Error(e, "Certificate reload rejected");
                ApplyInfo(null);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Certificate reload failed");
                ApplyInfo(null);
                ReportError(e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> ImportAsync(string? password, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_importSourcePath))
            {
                ReportWarning(_lang.CertificatesStatusSelectPfxFirst);
                return false;
            }

            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;

            try
            {
                await _service.ImportAsync(_importSourcePath, password, cancellationToken).ConfigureAwait(true);
                SetCurrentPassword(password);

                var info = await _service.LoadCurrentAsync(_currentPassword, cancellationToken).ConfigureAwait(true);
                ApplyInfo(info);
                ReportSuccess(string.Format(_lang.CertificatesStatusImportSuccess, _service.CertificatePath));
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Certificate import canceled, source={Source}", _importSourcePath);
                return false;
            }
            catch (CertificateException e)
            {
                _logger.Error(e, "Certificate import rejected, source={Source}", _importSourcePath);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Certificate import failed, source={Source}", _importSourcePath);
                ReportError(e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void SetImportSource(string? path, string? fileName)
        {
            _importSourcePath = path ?? string.Empty;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                ImportFileName = _lang.CertificatesImportNoSelection;
                return;
            }

            ImportFileName = fileName;
        }

        public async Task<bool> GenerateAsync(string? subject, string? password, string? validDaysText, CancellationToken cancellationToken)
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            ReportInfo(_lang.CertificatesStatusGenerateRunning);

            try
            {
                var result = await _service.GenerateAsync(subject, password, validDaysText, cancellationToken).ConfigureAwait(true);
                SetCurrentPassword(result.Password);

                var info = await _service.LoadCurrentAsync(_currentPassword, cancellationToken).ConfigureAwait(true);
                ApplyInfo(info);
                ReportSuccess(string.Format(_lang.CertificatesStatusCopySuccess, _service.CertificatePath));
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Certificate generation canceled");
                return false;
            }
            catch (CertificateException e)
            {
                _logger.Error(e, "Certificate generation rejected");
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Certificate generation failed");
                ReportError(e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> InstallWithCliAsync(CancellationToken cancellationToken)
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            ReportInfo(_lang.CertificatesStatusInstallCliRunning);

            try
            {
                await _service.InstallWithCliAsync(_currentPassword, cancellationToken).ConfigureAwait(true);
                var info = await _service.LoadCurrentAsync(_currentPassword, cancellationToken).ConfigureAwait(true);
                ApplyInfo(info);
                ReportSuccess(_lang.CertificatesInstallSuccess);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Certificate CLI install canceled");
                return false;
            }
            catch (CertificateException e)
            {
                _logger.Error(e, "Certificate CLI install rejected");
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Certificate CLI install failed");
                ReportError(e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ReportInfo(string message)
        {
            SetMessage(InfoBarSeverity.Informational, message);
        }

        public void ReportSuccess(string message)
        {
            SetMessage(InfoBarSeverity.Success, message);
        }

        public void ReportWarning(string message)
        {
            SetMessage(InfoBarSeverity.Warning, message);
        }

        public void ReportError(string message)
        {
            SetMessage(InfoBarSeverity.Error, message);
        }

        public void DismissMessage()
        {
            IsMessageOpen = false;
        }

        private void SetMessage(InfoBarSeverity severity, string message)
        {
            MessageSeverity = severity;
            Message = message;
            IsMessageOpen = true;
        }

        private void ApplyInfo(CertificateInfo? info)
        {
            if (info is null)
            {
                CertificateFileName = _lang.CertificatesNoCertificatePlaceholder;
                CertificateFullPath = string.Empty;
                SubjectText = string.Empty;
                ExpireText = string.Empty;
                TrustStatusText = string.Empty;
                CanInstall = false;
                return;
            }

            CertificateFileName = info.FileName;
            CertificateFullPath = info.Path;
            SubjectText = info.Subject;
            ExpireText = info.NotAfter.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            TrustStatusText = string.Format(
                _lang.CertificatesStatusInstalledFormat,
                info.Installed ? _lang.CertificatesStatusInstalledYes : _lang.CertificatesStatusInstalledNo);
            CanInstall = !info.Installed;
        }
    }
}
