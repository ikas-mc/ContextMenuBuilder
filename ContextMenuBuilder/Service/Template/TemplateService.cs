using ContextMenuBuilder.Core.Log;
using ContextMenuCustomApp.Common;
using NLog;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;

namespace ContextMenuBuilder.Service.Template
{
    public sealed class TemplateService
    {
        private const string ManifestFileName = "AppxManifest.xml";

        private const string UapNamespaceUri = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

        private const string BackupSearchPattern = "template-backup-*.zip";

        private const int MaxBackupCount = 2;

        private static readonly ILogger Log = AppLog.For<TemplateService>();

        private static AppLang Lang => AppContext.AppLang;

        public static readonly Uri DownloadUri = new("https://github.com/ikas-mc/ContextMenuForWindows11/releases/");

        public string TemplateFolderPath => AppContext.AppSettings.MenuPackageTemplatePath ?? string.Empty;

        public Task<TemplateInfo> LoadInfoAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => LoadInfo(TemplateFolderPath), cancellationToken);
        }

        public Task ImportAsync(string sourceFilePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                throw new TemplateValidationException(Lang.TemplateStatusSelectFileFirst);
            }

            if (!File.Exists(sourceFilePath))
            {
                throw new TemplateValidationException(Lang.TemplateStatusSelectFileFirst);
            }

            var folder = TemplateFolderPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new TemplateValidationException(string.Format(Lang.TemplateStatusImportFailed, Lang.TemplateStatusNoTemplate));
            }

            return Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(folder))
                    {
                        CreateBackup(folder);
                        CleanupOldBackups();
                        Directory.Delete(folder, true);
                    }

                    Directory.CreateDirectory(folder);
                    ZipFile.ExtractToDirectory(sourceFilePath, folder, true);
                    RemovePackageArtifacts(folder);
                    AppContext.AppSettings.MenuPackageTemplatePath = folder;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Template import failed, source={Source}, folder={Folder}", sourceFilePath, folder);
                    throw new TemplateException(string.Format(Lang.TemplateStatusImportFailed, e.Message), e);
                }

                Log.Info("Template imported, source={Source}, folder={Folder}", sourceFilePath, folder);
            }, cancellationToken);
        }

        private static TemplateInfo LoadInfo(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return Empty(TemplateAvailability.None);
            }

            if (!Directory.Exists(folder))
            {
                return Empty(TemplateAvailability.None);
            }

            if (!Directory.EnumerateFileSystemEntries(folder).Any())
            {
                return Empty(TemplateAvailability.None);
            }

            var manifestPath = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return Empty(TemplateAvailability.MissingManifest);
            }

            XDocument document;

            try
            {
                document = XDocument.Load(manifestPath);
            }
            catch (Exception e)
            {
                Log.Error(e, "Template manifest read failed, path={Path}", manifestPath);
                throw new TemplateException(string.Format(Lang.TemplateStatusReadFailed, e.Message), e);
            }

            var root = document.Root;
            if (root is null)
            {
                return Empty(TemplateAvailability.MissingIdentity);
            }

            var identity = root.Element(root.Name.Namespace + "Identity");
            if (identity is null)
            {
                return Empty(TemplateAvailability.MissingIdentity);
            }

            var uapNamespace = root.GetNamespaceOfPrefix("uap");
            if (uapNamespace is null)
            {
                uapNamespace = UapNamespaceUri;
            }

            var visual = document.Descendants(uapNamespace + "VisualElements").FirstOrDefault();

            var info = new TemplateInfo
            {
                Availability = TemplateAvailability.Ready,
                Name = identity.Attribute("Name")?.Value ?? string.Empty,
                Publisher = identity.Attribute("Publisher")?.Value ?? string.Empty,
                Version = identity.Attribute("Version")?.Value ?? string.Empty,
                Architecture = identity.Attribute("ProcessorArchitecture")?.Value ?? string.Empty,
                DisplayName = visual?.Attribute("DisplayName")?.Value ?? string.Empty,
            };

            Log.Info("Template info loaded, path={Path}, name={Name}, version={Version}", manifestPath, info.Name, info.Version);
            return info;
        }

        private static TemplateInfo Empty(TemplateAvailability availability)
        {
            return new TemplateInfo
            {
                Availability = availability,
                Name = string.Empty,
                Publisher = string.Empty,
                Version = string.Empty,
                Architecture = string.Empty,
                DisplayName = string.Empty,
            };
        }

        private static void CreateBackup(string folder)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "template-backup-" + timestamp + ".zip");
            ZipFile.CreateFromDirectory(folder, backupPath, CompressionLevel.SmallestSize, false);
            Log.Info("Template backup created, path={Path}", backupPath);
        }

        private static void CleanupOldBackups()
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder.Path;
                var backups = Directory.GetFiles(folder, BackupSearchPattern, SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetCreationTimeUtc)
                    .ToList();

                foreach (var backup in backups.Skip(MaxBackupCount))
                {
                    File.Delete(backup);
                    Log.Info("Template backup removed, path={Path}", backup);
                }
            }
            catch (Exception e)
            {
                Log.Warn(e, "Template backup cleanup failed");
            }
        }

        private static void RemovePackageArtifacts(string folder)
        {
            var metadataFolder = Path.Combine(folder, "AppxMetadata");
            if (Directory.Exists(metadataFolder))
            {
                Directory.Delete(metadataFolder, true);
            }

            var artifactNames = new[] { "[Content_Types].xml", "AppxBlockMap.xml", "AppxSignature.p7x" };
            foreach (var name in artifactNames)
            {
                var path = Path.Combine(folder, name);
                if (!File.Exists(path))
                {
                    continue;
                }

                File.Delete(path);
            }
        }
    }
}
