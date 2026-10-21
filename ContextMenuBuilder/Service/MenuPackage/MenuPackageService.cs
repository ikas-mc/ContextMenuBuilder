using ContextMenuBuilder.Core.Log;
using ContextMenuCustomApp.Common;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace ContextMenuBuilder.Service.MenuPackage
{
    public sealed class MenuPackageService
    {
        private static readonly ILogger Log = AppLog.For<MenuPackageService>();

        private static AppLang Lang => AppContext.AppLang;

        public Task<IReadOnlyList<MenuPackageInfo>> QueryAsync(string prefix, CancellationToken cancellationToken)
        {
            var normalizedPrefix = prefix?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageStatusEnterPrefix);
            }

            return Task.Run(async () =>
            {
                var packages = new List<MenuPackageInfo>();

                try
                {
                    var manager = new PackageManager();
                    foreach (var package in manager.FindPackagesForUser(string.Empty))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var identity = package.Id;
                        if (identity is null)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(identity.Name))
                        {
                            continue;
                        }

                        if (!identity.Name.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        packages.Add(new MenuPackageInfo
                        {
                            IdName = identity.Name,
                            IdFullName = identity.FullName,
                            DisplayName = package.DisplayName ?? string.Empty,
                            Publisher = package.PublisherDisplayName ?? identity.Publisher ?? string.Empty,
                            Version = FormatVersion(identity.Version),
                            FamilyName = identity.FamilyName ?? string.Empty,
                            ApplicationDisplayName = await ReadApplicationDisplayNameAsync(package, cancellationToken).ConfigureAwait(false),
                            InstallPath = package.InstalledLocation?.Path ?? string.Empty,
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Info("Package query canceled, prefix={Prefix}", normalizedPrefix);
                    throw;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Package query failed, prefix={Prefix}", normalizedPrefix);
                    throw new MenuPackageException(string.Format(Lang.MenuPackageStatusQueryFailed, e.Message), e);
                }

                packages.Sort(CompareByIdName);
                Log.Info("Package query finished, prefix={Prefix}, count={Count}", normalizedPrefix, packages.Count);
                return (IReadOnlyList<MenuPackageInfo>)packages;
            }, cancellationToken);
        }

        public async Task UninstallAsync(MenuPackageInfo package, CancellationToken cancellationToken)
        {
            if (package is null)
            {
                throw new MenuPackageValidationException(Lang.MenuPackageStatusSelectUninstall);
            }

            if (string.IsNullOrWhiteSpace(package.IdFullName))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageServicePackageNotFound);
            }

            Log.Info("Package uninstall start, fullName={FullName}", package.IdFullName);

            try
            {
                var manager = new PackageManager();
                var operation = manager.RemovePackageAsync(package.IdFullName, RemovalOptions.None);
                var result = await operation.AsTask(cancellationToken).ConfigureAwait(false);

                if (result.ExtendedErrorCode is not null)
                {
                    throw result.ExtendedErrorCode;
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info("Package uninstall canceled, fullName={FullName}", package.IdFullName);
                throw;
            }
            catch (MenuPackageException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e, "Package uninstall failed, fullName={FullName}", package.IdFullName);
                throw new MenuPackageException(string.Format(Lang.MenuPackageServiceRemoveFailed, e.Message), e);
            }

            Log.Info("Package uninstall finished, fullName={FullName}", package.IdFullName);
        }

        public async Task LaunchAsync(MenuPackageInfo package, CancellationToken cancellationToken)
        {
            if (package is null)
            {
                throw new MenuPackageValidationException(Lang.MenuPackageStatusSelectOpen);
            }

            if (string.IsNullOrWhiteSpace(package.IdFullName))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageServicePackageNotFound);
            }

            try
            {
                var manager = new PackageManager();
                var installed = manager.FindPackageForUser(string.Empty, package.IdFullName);
                if (installed is null)
                {
                    throw new MenuPackageException(Lang.MenuPackageServicePackageNotFound);
                }

                var entries = await installed.GetAppListEntriesAsync().AsTask(cancellationToken).ConfigureAwait(false);
                AppListEntry? entry = null;
                foreach (var candidate in entries)
                {
                    entry = candidate;
                    break;
                }

                if (entry is null)
                {
                    throw new MenuPackageException(Lang.MenuPackageServiceNoLaunchEntry);
                }

                var launched = await entry.LaunchAsync().AsTask(cancellationToken).ConfigureAwait(false);
                if (!launched)
                {
                    throw new MenuPackageException(Lang.MenuPackageServiceLaunchFailed);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info("Package launch canceled, fullName={FullName}", package.IdFullName);
                throw;
            }
            catch (MenuPackageException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e, "Package launch failed, fullName={FullName}", package.IdFullName);
                throw new MenuPackageException(string.Format(Lang.MenuPackageStatusOpenFailed, e.Message), e);
            }

            Log.Info("Package launched, fullName={FullName}", package.IdFullName);
        }

        private static async Task<string> ReadApplicationDisplayNameAsync(Package package, CancellationToken cancellationToken)
        {
            var entries = await package.GetAppListEntriesAsync().AsTask(cancellationToken).ConfigureAwait(false);
            foreach (var entry in entries)
            {
                var displayName = entry.DisplayInfo?.DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }

            return package.DisplayName ?? string.Empty;
        }

        private static string FormatVersion(PackageVersion version)
        {
            return string.Concat(version.Major, ".", version.Minor, ".", version.Build, ".", version.Revision);
        }

        private static int CompareByIdName(MenuPackageInfo left, MenuPackageInfo right)
        {
            return string.Compare(left.IdName, right.IdName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
