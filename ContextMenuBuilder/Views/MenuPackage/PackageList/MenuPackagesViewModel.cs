using CommunityToolkit.Mvvm.ComponentModel;
using ContextMenuBuilder.Core.Log;
using ContextMenuBuilder.Service.MenuPackage;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace ContextMenuBuilder.Views.MenuPackage.PackageList
{
    public enum MenuPackageSelectionPurpose
    {
        OpenMenus,
        Upgrade,
        Uninstall,
        Open,
    }

    public sealed partial class MenuPackagesViewModel : ObservableObject
    {
        private const string FallbackPrefix = "CMC.";

        private readonly MenuPackageService _packageService = new();
        private readonly AppLang _lang = AppContext.AppLang;
        private readonly ILogger _logger = AppLog.For<MenuPackagesViewModel>();

        public ObservableCollection<MenuPackageInfo> Packages { get; } = new();

        [ObservableProperty]
        public partial string Prefix
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial MenuPackageInfo? SelectedPackage
        {
            get; private set;
        }

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

        public bool IsNotBusy => !IsBusy;

        public bool HasSelection => SelectedPackage is not null;

        partial void OnSelectedPackageChanged(MenuPackageInfo? value)
        {
            OnPropertyChanged(nameof(HasSelection));
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotBusy));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var storedPrefix = AppContext.AppSettings.MenuPackageIdPrefix;
            if (string.IsNullOrWhiteSpace(storedPrefix))
            {
                Prefix = FallbackPrefix;
            }
            else
            {
                Prefix = storedPrefix;
            }

            await QueryAsync(Prefix, cancellationToken).ConfigureAwait(true);
        }

        public async Task<bool> QueryAsync(string prefix, CancellationToken cancellationToken)
        {
            if (IsBusy)
            {
                return false;
            }

            Prefix = prefix ?? string.Empty;
            IsBusy = true;
            DismissMessage();

            try
            {
                var packages = await _packageService.QueryAsync(Prefix, cancellationToken).ConfigureAwait(true);
                ReplacePackages(packages);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Package query canceled, prefix={Prefix}", Prefix);
                return false;
            }
            catch (MenuPackageException e)
            {
                _logger.Error(e, "Package query rejected, prefix={Prefix}", Prefix);
                ReplacePackages(Array.Empty<MenuPackageInfo>());
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Package query failed, prefix={Prefix}", Prefix);
                ReplacePackages(Array.Empty<MenuPackageInfo>());
                ReportError(e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void Select(MenuPackageInfo? package)
        {
            SelectedPackage = package;
        }

        public void ReportSelectionMissing(MenuPackageSelectionPurpose purpose)
        {
            switch (purpose)
            {
                case MenuPackageSelectionPurpose.Upgrade:
                    ReportWarning(_lang.MenuPackageStatusSelectUpgrade);
                    return;
                case MenuPackageSelectionPurpose.Uninstall:
                    ReportWarning(_lang.MenuPackageStatusSelectUninstall);
                    return;
                case MenuPackageSelectionPurpose.Open:
                    ReportWarning(_lang.MenuPackageStatusSelectOpen);
                    return;
                case MenuPackageSelectionPurpose.OpenMenus:
                    ReportWarning(_lang.MenuPackageStatusSelectMenus);
                    return;
                default:
                    DismissMessage();
                    return;
            }
        }

        public async Task<bool> UninstallAsync(MenuPackageInfo package, CancellationToken cancellationToken)
        {
            if (package is null)
            {
                ReportWarning(_lang.MenuPackageStatusSelectUninstall);
                return false;
            }

            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            ReportInfo(_lang.MenuPackageStatusUninstalling);

            try
            {
                await _packageService.UninstallAsync(package, cancellationToken).ConfigureAwait(true);
                var packages = await _packageService.QueryAsync(Prefix, cancellationToken).ConfigureAwait(true);
                ReplacePackages(packages);
                ReportSuccess(_lang.MenuPackageStatusUninstallFinished);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Package uninstall canceled, fullName={FullName}", package.IdFullName);
                DismissMessage();
                return false;
            }
            catch (MenuPackageException e)
            {
                _logger.Error(e, "Package uninstall rejected, fullName={FullName}", package.IdFullName);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Package uninstall failed, fullName={FullName}", package.IdFullName);
                ReportError(e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> LaunchAsync(MenuPackageInfo package, CancellationToken cancellationToken)
        {
            if (package is null)
            {
                ReportWarning(_lang.MenuPackageStatusSelectOpen);
                return false;
            }

            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            DismissMessage();

            try
            {
                await _packageService.LaunchAsync(package, cancellationToken).ConfigureAwait(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Package launch canceled, fullName={FullName}", package.IdFullName);
                return false;
            }
            catch (MenuPackageException e)
            {
                _logger.Error(e, "Package launch rejected, fullName={FullName}", package.IdFullName);
                ReportError(e.Message);
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Package launch failed, fullName={FullName}", package.IdFullName);
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

        private void ReplacePackages(IReadOnlyList<MenuPackageInfo> packages)
        {
            SelectedPackage = null;
            Packages.Clear();

            foreach (var package in packages)
            {
                Packages.Add(package);
            }
        }
    }
}
