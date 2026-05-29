using ContextMenuCustomApp.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace ContextMenuBuilder
{
    internal static class MenuPackageService
    {
        private static AppLang Lang => AppContext.AppLang;

        public static Task<IReadOnlyList<MenuPackageView>> QueryPackagesAsync(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                IReadOnlyList<MenuPackageView> empty = Array.Empty<MenuPackageView>();
                return Task.FromResult(empty);
            }

            return Task.Run(async () =>
            {
                var manager = new PackageManager();
                var result = new List<MenuPackageView>();
                foreach (var pkg in manager.FindPackagesForUser(null))
                {
                    var id = pkg.Id;
                    if (id?.Name is null)
                    {
                        continue;
                    }

                    if (!id.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    var version = $"{id.Version.Major}.{id.Version.Minor}.{id.Version.Build}.{id.Version.Revision}";
                    var publisher = pkg.PublisherDisplayName ?? id.Publisher;

                    var appEntries = await pkg.GetAppListEntriesAsync();

                    string appName = string.Empty;
                    foreach (var app in appEntries)
                    {
                        appName = app.DisplayInfo.DisplayName;
                    }

                    result.Add(new MenuPackageView(id.Name, id.FullName, pkg.DisplayName, publisher, version, id.FamilyName, appName));
                }

                return (IReadOnlyList<MenuPackageView>)result;
            });
        }

        public static Task RemovePackageAsync(string fullName, RemovalOptions options = RemovalOptions.None)
        {
            var manager = new PackageManager();
            return manager.RemovePackageAsync(fullName, options).AsTask();
        }

        public static async Task LaunchPackageAsync(string fullName)
        {
            var manager = new PackageManager();
            var package = manager.FindPackageForUser(null, fullName) ?? throw new InvalidOperationException(Lang.MenuPackageServicePackageNotFound);

            var entries = await package.GetAppListEntriesAsync();
            var entry = entries.FirstOrDefault() ?? throw new InvalidOperationException(Lang.MenuPackageServiceNoLaunchEntry);
            var launched = await entry.LaunchAsync();
            if (!launched)
            {
                throw new InvalidOperationException(Lang.MenuPackageServiceLaunchFailed);
            }
        }

        public static string? GetInstalledManifestPath(string fullName)
        {
            try
            {
                var manager = new PackageManager();
                var pkg = manager.FindPackageForUser(null, fullName);
                var path = pkg?.InstalledLocation?.Path;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var manifestPath = Path.Combine(path, "AppxManifest.xml");
                return File.Exists(manifestPath) ? manifestPath : null;
            }
            catch
            {
                return null;
            }
        }

        public static async Task InstallPackageAsync(string packagePath, DeploymentOptions options = DeploymentOptions.ForceApplicationShutdown)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentException(Lang.MenuPackageServiceInstallPathEmpty, nameof(packagePath));
            }

            var fullPath = Path.GetFullPath(packagePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(string.Format(Lang.MenuPackageServiceInstallFileMissing, fullPath), fullPath);
            }

            var uri = new Uri(fullPath);
            var manager = new PackageManager();
            await manager.AddPackageAsync(uri, null, options).AsTask();
        }
    }
}
