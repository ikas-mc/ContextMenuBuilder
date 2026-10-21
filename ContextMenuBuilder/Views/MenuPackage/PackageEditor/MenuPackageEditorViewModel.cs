using CommunityToolkit.Mvvm.ComponentModel;
using ContextMenuBuilder.Core.Log;
using ContextMenuBuilder.Service.MenuPackage;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContextMenuBuilder.Views.MenuPackage.PackageEditor
{
    public sealed partial class MenuPackageEditorViewModel : ObservableObject
    {
        private const string DefaultVersion = "1.0.0.0";

        private readonly MenuPackageEditorService _editorService = new();
        private readonly AppLang _lang = AppContext.AppLang;
        private readonly ILogger _logger = AppLog.For<MenuPackageEditorViewModel>();

        private string _generatedPackageId = string.Empty;

        public MenuPackageEditorViewModel()
        {
            HeaderTitle = _lang.MenuPackageEditorTitle;
        }

        [ObservableProperty]
        public partial string HeaderTitle
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string TemplateFolderPath
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string PackageId
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string Version
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial string DisplayName
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial bool AddToStartMenu
        {
            get; private set;
        } = true;

        [ObservableProperty]
        public partial bool IsPackageIdEditable
        {
            get; private set;
        } = true;

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
        public partial string? GeneratedPackagePath
        {
            get; private set;
        }

        public bool IsNotBusy => !IsBusy;

        public bool HasGeneratedPackage => !string.IsNullOrWhiteSpace(GeneratedPackagePath);

        public string GeneratedPackageDisplay => string.IsNullOrWhiteSpace(GeneratedPackagePath)
            ? _lang.MenuPackageEditorOutputEmpty
            : GeneratedPackagePath;

        public string OutputFolderPath => _editorService.OutputFolderPath;

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotBusy));
        }

        partial void OnGeneratedPackagePathChanged(string? value)
        {
            OnPropertyChanged(nameof(HasGeneratedPackage));
            OnPropertyChanged(nameof(GeneratedPackageDisplay));
        }

        public async Task<bool> InitializeAsync(MenuPackageInfo? package, CancellationToken cancellationToken)
        {
            try
            {
                AppxManifestInfo info;
                if (package is null)
                {
                    info = await _editorService.LoadTemplateManifestAsync(cancellationToken).ConfigureAwait(true);
                    PackageId = AppContext.AppSettings.MenuPackageIdPrefix;
                    IsPackageIdEditable = true;
                    HeaderTitle = _lang.MenuPackageEditorTitle;
                }
                else
                {
                    info = await _editorService.LoadInstalledManifestAsync(package.IdFullName, cancellationToken).ConfigureAwait(true);
                    PackageId = package.IdName;
                    IsPackageIdEditable = false;
                    HeaderTitle = string.Format("{0} ({1})", package.ApplicationDisplayName, package.IdName);
                }

                TemplateFolderPath = _editorService.TemplateFolderPath;
                if (string.IsNullOrWhiteSpace(info.IdVersion))
                {
                    Version = DefaultVersion;
                }
                else
                {
                    Version = MenuPackageEditorService.IncrementBuild(info.IdVersion);
                }

                DisplayName = ResolveDisplayName(info, package);
                AddToStartMenu = info.AppListEntry;
                DismissMessage();
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Editor initialization canceled, fullName={FullName}", package?.IdFullName);
                return false;
            }
            catch (MenuPackageException e)
            {
                _logger.Error(e, "Editor initialization rejected, fullName={FullName}", package?.IdFullName);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Editor initialization failed, fullName={FullName}", package?.IdFullName);
                ReportError(e.Message);
                return false;
            }
        }

        public async Task<bool> GenerateAsync(MenuPackageBuildRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                ReportWarning(_lang.MenuPackageEditorStatusEnterPackageId);
                return false;
            }

            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            DismissMessage();
            GeneratedPackagePath = null;
            _generatedPackageId = string.Empty;

            var packageId = request.PackageId ?? string.Empty;

            try
            {
                var progress = new Progress<string>(OnBuildProgress);
                var packagePath = await _editorService.GenerateAsync(request, progress, cancellationToken).ConfigureAwait(true);
                _generatedPackageId = packageId.Trim();
                GeneratedPackagePath = packagePath;
                ReportSuccess(string.Format(_lang.MenuPackageEditorStatusGenerated, packagePath));
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Package generation canceled, packageId={PackageId}", packageId);
                DismissMessage();
                return false;
            }
            catch (MenuPackageException e)
            {
                _logger.Error(e, "Package generation rejected, packageId={PackageId}", packageId);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Package generation failed, packageId={PackageId}", packageId);
                ReportError(e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> InstallAsync(CancellationToken cancellationToken)
        {
            if (IsBusy)
            {
                return false;
            }

            var packagePath = GeneratedPackagePath;
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                ReportWarning(_lang.MenuPackageEditorStatusGenerateFirst);
                return false;
            }

            IsBusy = true;
            ReportInfo(_lang.MenuPackageEditorStatusInstalling);

            var packageId = _generatedPackageId;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                packageId = PackageId;
            }

            try
            {
                await _editorService.InstallAsync(packagePath, packageId, cancellationToken).ConfigureAwait(true);
                ReportSuccess(_lang.MenuPackageEditorStatusInstallSuccess);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Package installation canceled, path={Path}", packagePath);
                DismissMessage();
                return false;
            }
            catch (MenuPackageException e)
            {
                _logger.Error(e, "Package installation rejected, path={Path}", packagePath);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Package installation failed, path={Path}", packagePath);
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

        private static string ResolveDisplayName(AppxManifestInfo info, MenuPackageInfo? package)
        {
            if (!string.IsNullOrWhiteSpace(info.ApplicationDisplayName))
            {
                return info.ApplicationDisplayName;
            }

            if (package is null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(package.ApplicationDisplayName))
            {
                return package.ApplicationDisplayName;
            }

            return package.DisplayName;
        }

        private void OnBuildProgress(string message)
        {
            ReportInfo(message);
            _logger.Debug("Build progress, message={Message}", message);
        }
    }
}
