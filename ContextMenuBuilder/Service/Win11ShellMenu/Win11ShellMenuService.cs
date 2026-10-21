using ContextMenuBuilder.Core.Log;
using ContextMenuCustomApp.Common;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;
using Windows.Storage.Streams;

namespace ContextMenuBuilder.Service.Win11ShellMenu
{
    public sealed class Win11ShellMenuService
    {
        private const string PackagedComRegistryKey = @"SOFTWARE\Classes\PackagedCom\Package";

        private const string BlockedRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

        private const string BlockedValue = "##custom-menu-builder";

        private const string ManifestFileName = "AppxManifest.xml";

        private static readonly ILogger Log = AppLog.For<Win11ShellMenuService>();

        private static AppLang Lang => AppContext.AppLang;

        public Task<IReadOnlyList<Win11ShellComItem>> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.Run(async () => await LoadAllAsync(cancellationToken).ConfigureAwait(false), cancellationToken);
        }

        public Task SetEnabledAsync(string menuComId, bool enabled, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(menuComId))
            {
                throw new ShellMenuException(string.Format(Lang.Win11ShellMenuStatusSetBlockedFailed, string.Empty));
            }

            return Task.Run(() =>
            {
                var normalized = FormatClsid(menuComId);

                try
                {
                    if (enabled)
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(BlockedRegistryKey, true);
                        key?.DeleteValue(normalized, false);
                    }
                    else
                    {
                        using var key = Registry.CurrentUser.CreateSubKey(BlockedRegistryKey, true);
                        key?.SetValue(normalized, BlockedValue, RegistryValueKind.String);
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    Log.Error(e, "Shell menu block state write denied, clsid={Clsid}", normalized);
                    throw new ShellMenuException(Lang.Win11ShellMenuStatusAdminRequired, e);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Shell menu block state write failed, clsid={Clsid}", normalized);
                    throw new ShellMenuException(string.Format(Lang.Win11ShellMenuStatusSetBlockedFailed, e.Message), e);
                }

                Log.Info("Shell menu block state changed, clsid={Clsid}, enabled={Enabled}", normalized, enabled);
            }, cancellationToken);
        }

        public Task LaunchAsync(string packageFullName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageFullName))
            {
                throw new ShellMenuException(Lang.MenuPackageServicePackageNotFound);
            }

            return Task.Run(async () =>
            {
                var manager = new PackageManager();
                var package = manager.FindPackageForUser(string.Empty, packageFullName);
                if (package is null)
                {
                    throw new ShellMenuException(Lang.MenuPackageServicePackageNotFound);
                }

                var entries = await package.GetAppListEntriesAsync().AsTask(cancellationToken).ConfigureAwait(false);
                if (entries.Count == 0)
                {
                    throw new ShellMenuException(Lang.MenuPackageServiceNoLaunchEntry);
                }

                var launched = await entries[0].LaunchAsync().AsTask(cancellationToken).ConfigureAwait(false);
                if (!launched)
                {
                    throw new ShellMenuException(Lang.MenuPackageServiceLaunchFailed);
                }

                Log.Info("Shell menu package launched, fullName={FullName}", packageFullName);
            }, cancellationToken);
        }

        private static async Task<IReadOnlyList<Win11ShellComItem>> LoadAllAsync(CancellationToken cancellationToken)
        {
            var items = new List<Win11ShellComItem>();

            using var localMachine = Registry.LocalMachine;
            using var packagedComKey = localMachine.OpenSubKey(PackagedComRegistryKey, false);
            if (packagedComKey is null)
            {
                Log.Warn("PackagedCom registry key not found");
                return items;
            }

            var blockedClsids = ReadBlockedClsids();

            foreach (var packageFullName in packagedComKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var packageItems = await ReadPackageItemsAsync(packagedComKey, packageFullName, blockedClsids, cancellationToken).ConfigureAwait(false);
                    items.AddRange(packageItems);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Log.Warn(e, "Shell menu package read failed, fullName={FullName}", packageFullName);
                }
            }

            Log.Info("Shell menu loaded, items={Count}", items.Count);
            return items;
        }

        private static async Task<IReadOnlyList<Win11ShellComItem>> ReadPackageItemsAsync(RegistryKey packagedComKey, string packageFullName, HashSet<string> blockedClsids, CancellationToken cancellationToken)
        {
            var items = new List<Win11ShellComItem>();

            using var packageKey = packagedComKey.OpenSubKey(packageFullName, false);
            using var classKey = packageKey?.OpenSubKey("Class", false);
            if (classKey is null)
            {
                return items;
            }

            var clsids = classKey.GetSubKeyNames();
            if (clsids.Length == 0)
            {
                return items;
            }

            var manager = new PackageManager();
            var package = manager.FindPackageForUser(string.Empty, packageFullName);
            if (package is null)
            {
                return items;
            }

            var installPath = package.InstalledLocation?.Path;
            if (string.IsNullOrWhiteSpace(installPath))
            {
                installPath = FindInstallPath(packageFullName);
            }

            if (installPath is null)
            {
                return items;
            }

            var menuClsids = ReadManifestMenuClsids(installPath);
            if (menuClsids.Count == 0)
            {
                return items;
            }

            var menuNames = ReadManifestComDisplayNames(installPath);
            var applicationDisplayName = await ReadApplicationDisplayNameAsync(package, cancellationToken).ConfigureAwait(false);
            var logo = ReadLogo(package);

            foreach (var clsid in clsids)
            {
                var normalized = FormatClsid(clsid);
                if (!menuClsids.Contains(normalized))
                {
                    continue;
                }

                menuNames.TryGetValue(normalized, out var menuName);

                items.Add(new Win11ShellComItem
                {
                    MenuComId = normalized,
                    MenuName = string.IsNullOrEmpty(menuName) ? normalized : menuName,
                    IdFullName = packageFullName,
                    IdName = package.Id?.Name ?? string.Empty,
                    PackageDisplayName = package.DisplayName ?? string.Empty,
                    ApplicationDisplayName = applicationDisplayName,
                    PackageInstallPath = installPath,
                    PackageLogo = logo,
                    IsEnabled = !blockedClsids.Contains(normalized),
                });
            }

            return items;
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

            return string.Empty;
        }

        private static RandomAccessStreamReference? ReadLogo(Package package)
        {
            try
            {
                return package.GetLogoAsRandomAccessStreamReference(new Size(48, 48));
            }
            catch (Exception e)
            {
                Log.Debug(e, "Shell menu package logo read failed, fullName={FullName}", package.Id?.FullName);
                return null;
            }
        }

        private static HashSet<string> ReadBlockedClsids()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(BlockedRegistryKey, false);
                if (key is null)
                {
                    return result;
                }

                foreach (var name in key.GetValueNames())
                {
                    result.Add(FormatClsid(name));
                }
            }
            catch (Exception e)
            {
                Log.Warn(e, "Shell menu blocked list read failed");
            }

            return result;
        }

        private static HashSet<string> ReadManifestMenuClsids(string installPath)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var document = LoadManifest(installPath);
            if (document is null)
            {
                return result;
            }

            foreach (var extension in document.Descendants().Where(e => e.Name.LocalName == "Extension"))
            {
                var category = extension.Attribute("Category")?.Value;
                if (!string.Equals(category, "windows.fileExplorerContextMenus", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var verb in extension.Descendants().Where(e => e.Name.LocalName == "Verb"))
                {
                    var clsid = verb.Attribute("Clsid")?.Value;
                    if (string.IsNullOrEmpty(clsid))
                    {
                        continue;
                    }

                    result.Add(FormatClsid(clsid));
                }
            }

            return result;
        }

        private static Dictionary<string, string> ReadManifestComDisplayNames(string installPath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var document = LoadManifest(installPath);
            if (document is null)
            {
                return result;
            }

            foreach (var extension in document.Descendants().Where(e => e.Name.LocalName == "Extension"))
            {
                var category = extension.Attribute("Category")?.Value;
                if (!string.Equals(category, "windows.comServer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var element in extension.Descendants().Where(e => e.Name.LocalName == "Class"))
                {
                    var id = element.Attribute("Id")?.Value;
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    var name = element.Attribute("DisplayName")?.Value ?? element.Parent?.Attribute("DisplayName")?.Value;
                    result[FormatClsid(id)] = string.IsNullOrEmpty(name) ? id : name;
                }
            }

            return result;
        }

        private static XDocument? LoadManifest(string installPath)
        {
            var manifestPath = Path.Combine(installPath, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                return XDocument.Load(manifestPath);
            }
            catch (Exception e)
            {
                Log.Warn(e, "Shell menu manifest read failed, path={Path}", manifestPath);
                return null;
            }
        }

        private static string? FindInstallPath(string packageFullName)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidate = Path.Combine(programFiles, "WindowsApps", packageFullName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            return null;
        }

        private static string FormatClsid(string clsid)
        {
            var normalized = clsid.Trim();
            if (!normalized.StartsWith('{'))
            {
                normalized = "{" + normalized;
            }

            if (!normalized.EndsWith('}'))
            {
                normalized += "}";
            }

            return normalized.ToUpperInvariant();
        }
    }
}
