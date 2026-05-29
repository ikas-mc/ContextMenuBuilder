using ContextMenuBuilder.Modules.File;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.Storage;

namespace ContextMenuBuilder
{
    /// <summary>
    /// All business logic for the menu-package editor.
    /// Methods throw on failure; callers are responsible for catching.
    /// </summary>
    internal static class MenuPackageEditorService
    {
        private static readonly string WorkingDir = Path.Combine(Path.GetTempPath(), "WinAppMenuPack");

        // ── Read manifest info ────────────────────────────────────────

        /// <summary>
        /// Reads <see cref="AppxManifestInfo"/> from the configured template directory.
        /// Returns <c>null</c> when the template manifest file does not exist.
        /// </summary>
        public static AppxManifestInfo? LoadTemplateManifestInfo()
        {
            var templatePath = AppContext.AppSettings.MenuPackageTemplatePath
                ?? throw new InvalidOperationException("Template path is not configured.");

            var manifestPath = Path.Combine(templatePath, "AppxManifest.xml");
            return AppxManifestService.ReadFromPath(manifestPath);
        }

        /// <summary>
        /// Reads <see cref="AppxManifestInfo"/> from the installed package identified
        /// by <paramref name="idFullName"/>.  Throws when the package or manifest
        /// cannot be found.
        /// </summary>
        public static AppxManifestInfo ReadInstalledManifestInfo(string idFullName)
        {
            var manager = new PackageManager();
            var pkg = manager.FindPackageForUser(null, idFullName)
                ?? throw new InvalidOperationException($"Package not found: {idFullName}");

            var installPath = pkg.InstalledLocation?.Path
                ?? throw new InvalidOperationException($"Cannot resolve install path for: {idFullName}");

            var manifestPath = Path.Combine(installPath, "AppxManifest.xml");
            return AppxManifestService.ReadFromPath(manifestPath)
                ?? throw new FileNotFoundException($"AppxManifest.xml missing in installed package: {manifestPath}");
        }

        // ── Generate ─────────────────────────────────────────────────

        /// <summary>
        /// Copies the template, applies <paramref name="manifest"/> (Publisher is
        /// resolved from the configured certificate), packs the msix, and returns
        /// the absolute path of the produced .msix file.
        /// </summary>
        /// <param name="manifest">
        /// Fields to write into the manifest.  <see cref="AppxManifestInfo.IdPublisher"/>
        /// is overwritten by the subject of the configured certificate.
        /// </param>
        /// <param name="log">Progress callback; may be invoked from a background thread.</param>
        public static async Task<string> GenerateAsync(AppxManifestInfo manifest, Action<string> log)
        {
            var templatePath = AppContext.AppSettings.MenuPackageTemplatePath
                ?? throw new InvalidOperationException("Template path is not configured.");

            var certPath = AppContext.AppSettings.CertPath
                ?? throw new InvalidOperationException("Certificate path is not configured.");

            var certPassword = string.IsNullOrWhiteSpace(AppContext.AppSettings.CertPassword)
                ? "password"
                : AppContext.AppSettings.CertPassword;

            Directory.CreateDirectory(WorkingDir);

            // ── Copy template ────────────────────────────────────────
            var inputFolder = Directory.CreateTempSubdirectory("context-menu-builder-").FullName;
            var sourceFolder = await StorageFolder.GetFolderFromPathAsync(templatePath);
            var destFolder = await StorageFolder.GetFolderFromPathAsync(inputFolder);
            await Files.CopyFolderDeep(sourceFolder, destFolder, true, name =>
            {
                log($"Copying: {name}");
                return name;
            });

            // ── Resolve ClassId from template when not supplied by caller ─
            var manifestPath = Path.Combine(inputFolder, "AppxManifest.xml");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"AppxManifest.xml not found in template: {manifestPath}");
            }

            // ── Patch manifest ───────────────────────────────────────
            log("Read publisher from cert " + certPath);
            X509Certificate2 x509Certificate;
            try
            {
                x509Certificate = new X509Certificate2(certPath, certPassword);
            }
            catch (Exception e)
            {
                log("cert load error" + e.Message);
                throw;
            }

            var publisher = x509Certificate.Subject;
            if (string.IsNullOrEmpty(publisher))
            {
                throw new InvalidOperationException("Certificate Subject is empty");
            }

            log("Updating AppxManifest.xml ...");
            AppxManifestService.Update(manifestPath, manifest with { IdPublisher = publisher });

            // ── Pack ─────────────────────────────────────────────────
            var outputFileName = $"{manifest.IdName}.msix";
            var args = new StringBuilder();
            args.Append("pack ");
            args.Append('"').Append(inputFolder).Append('"');
            args.Append(" --output \"").Append(outputFileName).Append('"');
            args.Append(" --skip-pri");
            args.Append(" --cert \"").Append(certPath).Append('"');
            if (!string.IsNullOrEmpty(certPassword))
            {
                args.Append(" --cert-password \"").Append(certPassword).Append('"');
            }

            log("Packaging ...");
            var packResult = await WinAppCliService.RunAsync(args.ToString(), WorkingDir);
            log(packResult.Output);

            var outputPath = Path.Combine(WorkingDir, outputFileName);
            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException($"Output file not found after packaging: {outputPath}");
            }

            return outputPath;
        }

        // ── Install ──────────────────────────────────────────────────

        /// <summary>
        /// Installs the .msix at <paramref name="packagePath"/> and records
        /// the path under the app's local data folder.
        /// </summary>
        public static async Task InstallAsync(string packagePath, string packageId)
        {
            await MenuPackageService.InstallPackageAsync(packagePath);

            var recordDir = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "menuPackage");
            Directory.CreateDirectory(recordDir);
            if (!string.IsNullOrWhiteSpace(packageId))
            {
                File.WriteAllText(Path.Combine(recordDir, packageId), packagePath);
            }
        }

        // ── Utility ──────────────────────────────────────────────────

        public static string IncrementBuild(string version)
        {
            if (Version.TryParse(version, out var v))
            {
                var revision = v.Revision >= 0 ? v.Revision + 1 : 1;
                return new Version(v.Major, v.Minor, v.Build, revision).ToString();
            }

            return version;
        }
    }
}
