using CommunityToolkit.Mvvm.ComponentModel;
using ContextMenuBuilder.Core.Log;
using ContextMenuBuilder.Service.Template;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContextMenuBuilder
{
    public sealed partial class TemplateViewModel : ObservableObject
    {
        private const string EmptyValue = "-";

        private readonly TemplateService _service = new();
        private readonly AppLang _lang = AppContext.AppLang;
        private readonly ILogger _logger = AppLog.For<TemplateViewModel>();

        private string _importSourcePath = string.Empty;

        [ObservableProperty]
        public partial string TemplateStateText
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string DisplayNameText
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string IdentityNameText
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string PublisherText
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string VersionText
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string ArchitectureText
        {
            get; private set;
        } = string.Empty;

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

        [ObservableProperty]
        public partial bool IsBusy
        {
            get; private set;
        }

        [ObservableProperty]
        public partial string TemplateFolderPath
        {
            get; private set;
        } = string.Empty;

        public bool IsNotBusy => !IsBusy;

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotBusy));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            ImportFileName = _lang.TemplateNoFileSelected;
            TemplateFolderPath = _service.TemplateFolderPath;
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }

        public async Task<bool> LoadAsync(CancellationToken cancellationToken)
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;

            try
            {
                var info = await _service.LoadInfoAsync(cancellationToken).ConfigureAwait(true);
                ApplyInfo(info);

                if (info.Availability != TemplateAvailability.Ready)
                {
                    ReportWarning(DescribeAvailability(info.Availability));
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Template info load canceled");
                return false;
            }
            catch (TemplateException e)
            {
                _logger.Error(e, "Template info load rejected");
                ApplyInfo(null);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Template info load failed");
                ApplyInfo(null);
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
                ImportFileName = _lang.TemplateNoFileSelected;
                return;
            }

            ImportFileName = fileName;
        }

        public async Task<bool> ImportAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_importSourcePath))
            {
                ReportWarning(_lang.TemplateStatusSelectFileFirst);
                return false;
            }

            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            ReportInfo(_lang.TemplateStatusImporting);

            try
            {
                await _service.ImportAsync(_importSourcePath, cancellationToken).ConfigureAwait(true);
                TemplateFolderPath = _service.TemplateFolderPath;
                var info = await _service.LoadInfoAsync(cancellationToken).ConfigureAwait(true);
                ApplyInfo(info);
                ReportSuccess(_lang.TemplateStatusImportCompleted);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Template import canceled, source={Source}", _importSourcePath);
                return false;
            }
            catch (TemplateException e)
            {
                _logger.Error(e, "Template import rejected, source={Source}", _importSourcePath);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Template import failed, source={Source}", _importSourcePath);
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

        private void ApplyInfo(TemplateInfo? info)
        {
            if (info is null)
            {
                ApplyEmpty(_lang.TemplateStateNotImported);
                return;
            }

            if (info.Availability == TemplateAvailability.None)
            {
                ApplyEmpty(_lang.TemplateStateNotImported);
                return;
            }

            if (info.Availability == TemplateAvailability.MissingManifest)
            {
                ApplyEmpty(_lang.TemplateStateManifestMissing);
                return;
            }

            if (info.Availability == TemplateAvailability.MissingIdentity)
            {
                ApplyEmpty(_lang.TemplateStateIdentityMissing);
                return;
            }

            TemplateStateText = _lang.TemplateStateReady;
            IdentityNameText = ValueOrEmpty(info.Name);
            PublisherText = ValueOrEmpty(info.Publisher);
            VersionText = ValueOrEmpty(info.Version);
            ArchitectureText = ValueOrEmpty(info.Architecture);
            DisplayNameText = ValueOrEmpty(info.DisplayName);
        }

        private void ApplyEmpty(string state)
        {
            TemplateStateText = state;
            IdentityNameText = EmptyValue;
            PublisherText = EmptyValue;
            VersionText = EmptyValue;
            ArchitectureText = EmptyValue;
            DisplayNameText = EmptyValue;
        }

        private string DescribeAvailability(TemplateAvailability availability)
        {
            if (availability == TemplateAvailability.MissingManifest)
            {
                return _lang.TemplateStatusMissingManifest;
            }

            if (availability == TemplateAvailability.MissingIdentity)
            {
                return _lang.TemplateStatusMissingIdentity;
            }

            return _lang.TemplateStatusNoTemplate;
        }

        private static string ValueOrEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return EmptyValue;
            }

            return value;
        }
    }
}
