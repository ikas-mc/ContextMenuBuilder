using ContextMenuBuilder.Core.Log;
using ContextMenuCustomApp.Common;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;

namespace ContextMenuBuilder.Service.MenuPackage
{
    public sealed record MenuPackageBuildRequest
    {
        public required string PackageId { get; init; }

        public required string Version { get; init; }

        public required string DisplayName { get; init; }

        public bool AddToStartMenu { get; init; }

        public string? SourcePackageFullName { get; init; }
    }

    public sealed class MenuPackageEditorService
    {
        private const int MaxPackageIdLength = 50;

        private const string ManifestFileName = "AppxManifest.xml";

        private const string UapNamespaceUri = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

        private static readonly ILogger Log = AppLog.For<MenuPackageEditorService>();

        private static AppLang Lang => AppContext.AppLang;

        private static Settings AppSettings => AppContext.AppSettings;

        public string TemplateFolderPath => AppSettings.MenuPackageTemplatePath ?? string.Empty;

        public string OutputFolderPath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "package-output");

        public Task<AppxManifestInfo> LoadTemplateManifestAsync(CancellationToken cancellationToken)
        {
            var templateFolder = TemplateFolderPath;
            if (string.IsNullOrWhiteSpace(templateFolder))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageEditorStatusInputNotConfigured);
            }

            if (!Directory.Exists(templateFolder))
            {
                throw new MenuPackageValidationException(string.Format(Lang.MenuPackageEditorStatusFolderMissing, templateFolder));
            }

            var manifestPath = Path.Combine(templateFolder, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                throw new MenuPackageValidationException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath));
            }

            return Task.Run(() =>
            {
                try
                {
                    var info = ReadManifest(manifestPath);
                    Log.Info("Template manifest read, path={Path}, id={IdName}, version={Version}", manifestPath, info.IdName, info.IdVersion);
                    return info;
                }
                catch (MenuPackageException e)
                {
                    Log.Error(e, "Template manifest read failed, path={Path}", manifestPath);
                    throw;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Template manifest read failed, path={Path}", manifestPath);
                    throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath), e);
                }
            }, cancellationToken);
        }

        public async Task<AppxManifestInfo> LoadInstalledManifestAsync(string packageFullName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageFullName))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageServicePackageNotFound);
            }

            var installPath = await Task.Run(() =>
            {
                var manager = new PackageManager();
                var package = manager.FindPackageForUser(string.Empty, packageFullName);
                return package?.InstalledLocation?.Path ?? string.Empty;
            }, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(installPath))
            {
                throw new MenuPackageException(Lang.MenuPackageServicePackageNotFound);
            }

            var manifestPath = Path.Combine(installPath, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath));
            }

            return await Task.Run(() =>
            {
                try
                {
                    var info = ReadManifest(manifestPath);
                    Log.Info("Installed manifest read, fullName={FullName}, classId={ClassId}", packageFullName, info.ClassId);
                    return info;
                }
                catch (MenuPackageException e)
                {
                    Log.Error(e, "Installed manifest read failed, fullName={FullName}, path={Path}", packageFullName, manifestPath);
                    throw;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Installed manifest read failed, fullName={FullName}, path={Path}", packageFullName, manifestPath);
                    throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath), e);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> GenerateAsync(MenuPackageBuildRequest request, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            var packageId = request.PackageId.Trim();

            var templateFolder = TemplateFolderPath;
            if (string.IsNullOrWhiteSpace(templateFolder))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageEditorStatusInputNotConfigured);
            }

            if (!Directory.Exists(templateFolder))
            {
                throw new MenuPackageValidationException(string.Format(Lang.MenuPackageEditorStatusFolderMissing, templateFolder));
            }

            var certificatePath = AppSettings.CertPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(certificatePath))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageServiceCertificateNotConfigured);
            }

            if (!File.Exists(certificatePath))
            {
                throw new MenuPackageValidationException(string.Format(Lang.MenuPackageServiceCertificateMissing, certificatePath));
            }

            var certificatePassword = AppSettings.CertPassword ?? string.Empty;

            var workspace = OutputFolderPath;
            Directory.CreateDirectory(workspace);

            var inputFolder = Path.Combine(workspace, "input-" + Guid.NewGuid().ToString("N"));
            Log.Info("Package build start, packageId={PackageId}, version={Version}, source={Source}", packageId, request.Version, request.SourcePackageFullName);

            try
            {
                Report(progress, Lang.MenuPackageEditorStatusCopyingTemplate);
                await CopyDirectoryAsync(templateFolder, inputFolder, cancellationToken).ConfigureAwait(false);

                var manifestPath = Path.Combine(inputFolder, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath));
                }

                Report(progress, Lang.MenuPackageEditorStatusReadingCertificate);
                var publisher = await ReadCertificatePublisherAsync(certificatePath, certificatePassword, cancellationToken).ConfigureAwait(false);

                var classId = await ResolveClassIdAsync(request.SourcePackageFullName, cancellationToken).ConfigureAwait(false);

                Report(progress, Lang.MenuPackageEditorStatusUpdatingManifest);
                var manifest = new AppxManifestInfo
                {
                    IdName = packageId,
                    IdVersion = request.Version.Trim(),
                    IdPublisher = publisher,
                    ApplicationDisplayName = request.DisplayName.Trim(),
                    AppListEntry = request.AddToStartMenu,
                    ClassId = classId,
                };

                await Task.Run(() => WriteManifest(manifestPath, manifest), cancellationToken).ConfigureAwait(false);

                Report(progress, Lang.MenuPackageEditorStatusPackaging);
                var outputFileName = packageId + ".msix";
                var outputPath = Path.Combine(workspace, outputFileName);
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                var arguments = BuildPackArguments(inputFolder, outputFileName, certificatePath, certificatePassword);
                await RunCliAsync(arguments, workspace, cancellationToken).ConfigureAwait(false);

                Report(progress, Lang.MenuPackageEditorStatusVerifying);
                if (!File.Exists(outputPath))
                {
                    throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusOutputMissing, outputPath));
                }

                Log.Info("Package build finished, packageId={PackageId}, path={Path}, size={Size}", packageId, outputPath, new FileInfo(outputPath).Length);
                return outputPath;
            }
            catch (OperationCanceledException)
            {
                Log.Info("Package build canceled, packageId={PackageId}", packageId);
                throw;
            }
            catch (MenuPackageException e)
            {
                Log.Error(e, "Package build failed, packageId={PackageId}", packageId);
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e, "Package build failed, packageId={PackageId}", packageId);
                throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusBuildFailed, e.Message), e);
            }
            finally
            {
                TryDeleteDirectory(inputFolder);
            }
        }

        public async Task InstallAsync(string packagePath, string packageId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageServiceInstallPathEmpty);
            }

            var fullPath = Path.GetFullPath(packagePath);
            if (!File.Exists(fullPath))
            {
                throw new MenuPackageValidationException(string.Format(Lang.MenuPackageServiceInstallFileMissing, fullPath));
            }

            Log.Info("Package install start, packageId={PackageId}, path={Path}", packageId, fullPath);

            try
            {
                var manager = new PackageManager();
                var operation = manager.AddPackageAsync(new Uri(fullPath), null, DeploymentOptions.ForceApplicationShutdown);
                var result = await operation.AsTask(cancellationToken).ConfigureAwait(false);

                if (result.ExtendedErrorCode is not null)
                {
                    throw result.ExtendedErrorCode;
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info("Package install canceled, path={Path}", fullPath);
                throw;
            }
            catch (MenuPackageException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e, "Package install failed, path={Path}", fullPath);
                throw new MenuPackageException(string.Format(Lang.MenuPackageServiceInstallFailed, e.Message), e);
            }

            Log.Info("Package install finished, packageId={PackageId}, path={Path}", packageId, fullPath);
        }

        public static string IncrementBuild(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return version ?? string.Empty;
            }

            if (!Version.TryParse(version.Trim(), out var parsed))
            {
                return version;
            }

            if (parsed is null)
            {
                return version;
            }

            var build = parsed.Build < 0 ? 0 : parsed.Build;
            var revision = parsed.Revision < 0 ? 0 : parsed.Revision;
            return string.Concat(parsed.Major, ".", parsed.Minor, ".", build, ".", revision + 1);
        }

        private static void ValidateRequest(MenuPackageBuildRequest request)
        {
            if (request is null)
            {
                throw new MenuPackageValidationException(Lang.MenuPackageEditorStatusEnterPackageId);
            }

            var packageId = request.PackageId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageEditorStatusEnterPackageId);
            }

            if (packageId.Length > MaxPackageIdLength)
            {
                throw new MenuPackageValidationException(string.Format(Lang.MenuPackageEditorStatusPackageIdTooLong, MaxPackageIdLength));
            }

            foreach (var character in packageId)
            {
                if (IsPackageIdCharacter(character))
                {
                    continue;
                }

                throw new MenuPackageValidationException(string.Format(Lang.MenuPackageEditorStatusPackageIdInvalid, character));
            }

            var version = request.Version?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageEditorStatusVersionRequired);
            }

            if (!IsPackageVersion(version))
            {
                throw new MenuPackageValidationException(string.Format(Lang.MenuPackageEditorStatusVersionInvalid, version));
            }

            if (string.IsNullOrWhiteSpace(request.DisplayName))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageEditorStatusDisplayNameRequired);
            }
        }

        private static bool IsPackageIdCharacter(char character)
        {
            if (character >= 'a' && character <= 'z')
            {
                return true;
            }

            if (character >= 'A' && character <= 'Z')
            {
                return true;
            }

            if (character >= '0' && character <= '9')
            {
                return true;
            }

            if (character == '.')
            {
                return true;
            }

            return character == '-';
        }

        private static bool IsPackageVersion(string version)
        {
            if (!Version.TryParse(version, out var parsed))
            {
                return false;
            }

            if (parsed is null)
            {
                return false;
            }

            if (parsed.Revision < 0)
            {
                return false;
            }

            if (!IsVersionComponent(parsed.Major))
            {
                return false;
            }

            if (!IsVersionComponent(parsed.Minor))
            {
                return false;
            }

            if (!IsVersionComponent(parsed.Build))
            {
                return false;
            }

            return IsVersionComponent(parsed.Revision);
        }

        private static bool IsVersionComponent(int value)
        {
            return value >= 0 && value <= ushort.MaxValue;
        }

        private async Task<string> ResolveClassIdAsync(string? sourcePackageFullName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourcePackageFullName))
            {
                var generated = Guid.NewGuid().ToString();
                Log.Info("ClassId generated, classId={ClassId}", generated);
                return generated;
            }

            var source = await LoadInstalledManifestAsync(sourcePackageFullName, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(source.ClassId))
            {
                Log.Info("ClassId reused, classId={ClassId}, fullName={FullName}", source.ClassId, sourcePackageFullName);
                return source.ClassId;
            }

            var fallback = Guid.NewGuid().ToString();
            Log.Warn("Installed manifest has no ClassId, generated new one, classId={ClassId}, fullName={FullName}", fallback, sourcePackageFullName);
            return fallback;
        }

        private static Task<string> ReadCertificatePublisherAsync(string certificatePath, string certificatePassword, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var certificate = new X509Certificate2(certificatePath, certificatePassword, X509KeyStorageFlags.EphemeralKeySet);
                    var subject = certificate.Subject;
                    if (string.IsNullOrWhiteSpace(subject))
                    {
                        throw new MenuPackageException(Lang.MenuPackageEditorStatusPublisherMissing);
                    }

                    Log.Info("Certificate read, path={Path}, subject={Subject}", certificatePath, subject);
                    return subject;
                }
                catch (MenuPackageException e)
                {
                    Log.Error(e, "Certificate has no subject, path={Path}", certificatePath);
                    throw;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Certificate read failed, path={Path}", certificatePath);
                    throw new MenuPackageException(string.Format(Lang.MenuPackageServiceCertificateInvalid, certificatePath, e.Message), e);
                }
            }, cancellationToken);
        }

        private static Task CopyDirectoryAsync(string sourceFolder, string destinationFolder, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var source = new DirectoryInfo(sourceFolder);
                if (!source.Exists)
                {
                    throw new MenuPackageValidationException(string.Format(Lang.MenuPackageEditorStatusFolderMissing, sourceFolder));
                }

                CopyDirectory(source, new DirectoryInfo(destinationFolder), cancellationToken);
            }, cancellationToken);
        }

        private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination, CancellationToken cancellationToken)
        {
            destination.Create();

            foreach (var file in source.GetFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                file.CopyTo(Path.Combine(destination.FullName, file.Name), true);
            }

            foreach (var directory in source.GetDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopyDirectory(directory, new DirectoryInfo(Path.Combine(destination.FullName, directory.Name)), cancellationToken);
            }
        }

        private static string BuildPackArguments(string inputFolder, string outputFileName, string certificatePath, string certificatePassword)
        {
            var builder = new StringBuilder();
            builder.Append("pack ");
            builder.Append(Quote(inputFolder));
            builder.Append(" --output ");
            builder.Append(Quote(outputFileName));
            builder.Append(" --skip-pri");
            builder.Append(" --cert ");
            builder.Append(Quote(certificatePath));

            if (!string.IsNullOrWhiteSpace(certificatePassword))
            {
                builder.Append(" --cert-password ");
                builder.Append(Quote(certificatePassword));
            }

            return builder.ToString();
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        private static async Task RunCliAsync(string arguments, string workingDirectory, CancellationToken cancellationToken)
        {
            var executable = AppSettings.WinAppCliPath;
            if (string.IsNullOrWhiteSpace(executable))
            {
                throw new MenuPackageValidationException(Lang.MenuPackageServiceCliNotConfigured);
            }

            Directory.CreateDirectory(workingDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            };

            Log.Debug("CLI start, file={File}, args={Arguments}", executable, arguments);

            using var process = new Process
            {
                StartInfo = startInfo,
            };

            try
            {
                if (!process.Start())
                {
                    throw new MenuPackageException(string.Format(Lang.WinAppCliServiceStartFailed, executable));
                }
            }
            catch (MenuPackageException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e, "CLI start failed, file={File}", executable);
                throw new MenuPackageException(string.Format(Lang.WinAppCliServiceStartFailed, executable), e);
            }

            using var registration = cancellationToken.Register(() => TryKill(process));

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var output = ComposeOutput(await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false));
            if (process.ExitCode != 0)
            {
                Log.Error("CLI failed, exitCode={ExitCode}, output={Output}", process.ExitCode, output);
                throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusPackFailed, output));
            }

            Log.Info("CLI finished, output={Output}", output);
        }

        private static string ComposeOutput(string standardOutput, string standardError)
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                builder.Append(standardOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(standardError.TrimEnd());
            }

            return builder.ToString();
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process.HasExited)
                {
                    return;
                }

                process.Kill(true);
            }
            catch (Exception e)
            {
                Log.Debug(e, "CLI process kill failed");
            }
        }

        private static AppxManifestInfo ReadManifest(string manifestPath)
        {
            XDocument document;

            try
            {
                document = XDocument.Load(manifestPath);
            }
            catch (Exception e)
            {
                throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath), e);
            }

            var root = document.Root;
            if (root is null)
            {
                throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath));
            }

            var identity = root.Element(root.Name.Namespace + "Identity");
            var visual = FindVisualElements(document);

            var appListEntry = true;
            if (visual is not null)
            {
                var attribute = visual.Attribute("AppListEntry");
                if (attribute is null)
                {
                    attribute = visual.Attribute(visual.Name.Namespace + "AppListEntry");
                }

                if (attribute is not null)
                {
                    appListEntry = !attribute.Value.Equals("none", StringComparison.OrdinalIgnoreCase);
                }
            }

            string? classId = null;
            foreach (var element in document.Descendants())
            {
                if (!element.Name.LocalName.Equals("Class", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                classId = element.Attribute("Id")?.Value;
                break;
            }

            return new AppxManifestInfo
            {
                IdName = identity?.Attribute("Name")?.Value,
                IdVersion = identity?.Attribute("Version")?.Value,
                IdPublisher = identity?.Attribute("Publisher")?.Value,
                ApplicationDisplayName = visual?.Attribute("DisplayName")?.Value,
                AppListEntry = appListEntry,
                ClassId = classId,
            };
        }

        private static void WriteManifest(string manifestPath, AppxManifestInfo info)
        {
            XDocument document;

            try
            {
                document = XDocument.Load(manifestPath);
            }
            catch (Exception e)
            {
                throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath), e);
            }

            var root = document.Root;
            if (root is null)
            {
                throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestMissing, manifestPath));
            }

            ApplyIdentity(root, info);
            ApplyVisualElements(document, info);

            if (!string.IsNullOrWhiteSpace(info.ClassId))
            {
                ApplyClassId(document, info.ClassId);
            }

            try
            {
                document.Save(manifestPath);
            }
            catch (Exception e)
            {
                throw new MenuPackageException(string.Format(Lang.MenuPackageEditorStatusManifestUpdateFailed, manifestPath), e);
            }
        }

        private static void ApplyIdentity(XElement root, AppxManifestInfo info)
        {
            if (info.IdName is null && info.IdVersion is null && info.IdPublisher is null)
            {
                return;
            }

            var identity = root.Element(root.Name.Namespace + "Identity");
            if (identity is null)
            {
                throw new MenuPackageException(Lang.MenuPackageEditorStatusManifestMissing);
            }

            if (info.IdName is not null)
            {
                identity.SetAttributeValue("Name", info.IdName);
            }

            if (info.IdVersion is not null)
            {
                identity.SetAttributeValue("Version", info.IdVersion);
            }

            if (info.IdPublisher is not null)
            {
                identity.SetAttributeValue("Publisher", info.IdPublisher);
            }
        }

        private static void ApplyVisualElements(XDocument document, AppxManifestInfo info)
        {
            var visual = FindVisualElements(document);
            if (visual is null)
            {
                return;
            }

            if (info.ApplicationDisplayName is not null)
            {
                visual.SetAttributeValue("DisplayName", info.ApplicationDisplayName);
            }

            var appListEntry = info.AppListEntry ? "default" : "none";
            var attribute = visual.Attribute(visual.Name.Namespace + "AppListEntry");
            if (attribute is null)
            {
                attribute = visual.Attribute("AppListEntry");
            }

            if (attribute is null)
            {
                visual.SetAttributeValue("AppListEntry", appListEntry);
                return;
            }

            attribute.Value = appListEntry;
        }

        private static void ApplyClassId(XDocument document, string classId)
        {
            foreach (var attribute in document.Descendants().Attributes())
            {
                if (attribute.Name.LocalName.Equals("Clsid", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.Value = classId;
                }
            }

            foreach (var element in document.Descendants())
            {
                if (!element.Name.LocalName.Equals("Class", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var idAttribute = element.Attribute("Id");
                if (idAttribute is not null)
                {
                    idAttribute.Value = classId;
                }
            }

            foreach (var element in document.Descendants())
            {
                if (!element.Name.LocalName.Equals("SurrogateServer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var displayAttribute = element.Attribute("DisplayName");
                if (displayAttribute is null)
                {
                    element.Add(new XAttribute("DisplayName", classId));
                    continue;
                }

                displayAttribute.Value = classId;
            }
        }

        private static XElement? FindVisualElements(XDocument document)
        {
            var root = document.Root;
            if (root is null)
            {
                return null;
            }

            var uapNamespace = root.GetNamespaceOfPrefix("uap");
            if (uapNamespace is not null)
            {
                var namespaced = document.Descendants(uapNamespace + "VisualElements").FirstOrDefault();
                if (namespaced is not null)
                {
                    return namespaced;
                }
            }

            return document.Descendants((XNamespace)UapNamespaceUri + "VisualElements").FirstOrDefault();
        }

        private static void Report(IProgress<string>? progress, string message)
        {
            if (progress is null)
            {
                return;
            }

            progress.Report(message);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return;
                }

                Directory.Delete(path, true);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Workspace cleanup failed, path={Path}", path);
            }
        }
    }
}
