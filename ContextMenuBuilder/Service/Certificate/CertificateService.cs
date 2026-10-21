using ContextMenuBuilder.Core.Log;
using ContextMenuCustomApp.Common;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContextMenuBuilder.Service.Certificate
{
    public sealed record CertificateGenerationResult
    {
        public required string Output { get; init; }

        public required string Password { get; init; }
    }

    public sealed class CertificateService
    {
        private const string DefaultSubject = "CN=CMC-TEST";

        private const string DefaultPassword = "password";

        private const string CliWorkingFolderName = "WinAppCliCert";

        private const string CliOutputFileName = "cert_tmp.pfx";

        private static readonly ILogger Log = AppLog.For<CertificateService>();

        private static readonly StoreName[] TrackedStores = new[] { StoreName.TrustedPeople, StoreName.TrustedPublisher };

        private static AppLang Lang => AppContext.AppLang;

        public string CertificatePath => AppContext.AppSettings.CertPath ?? string.Empty;

        public bool Exists
        {
            get
            {
                var path = CertificatePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                return File.Exists(path);
            }
        }

        public string? LoadRememberedPassword()
        {
            var password = AppContext.AppSettings.CertPassword;
            if (string.IsNullOrEmpty(password))
            {
                return null;
            }

            return password;
        }

        public void RememberPassword(string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                AppContext.AppSettings.CertPassword = null;
                Log.Info("Certificate password cleared");
                return;
            }

            AppContext.AppSettings.CertPassword = password;
            Log.Info("Certificate password stored");
        }

        public Task<CertificateInfo?> LoadCurrentAsync(string? password, CancellationToken cancellationToken)
        {
            var path = CertificatePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return Task.FromResult<CertificateInfo?>(null);
            }

            if (!File.Exists(path))
            {
                return Task.FromResult<CertificateInfo?>(null);
            }

            return Task.Run<CertificateInfo?>(() =>
            {
                X509Certificate2 certificate;

                try
                {
                    certificate = Load(path, password);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Certificate load failed, path={Path}", path);
                    throw new CertificateException(string.Format(Lang.CertificatesStatusLoadFailed, e.Message), e);
                }

                using (certificate)
                {
                    var installed = IsInstalled(certificate);
                    Log.Info("Certificate loaded, path={Path}, subject={Subject}, installed={Installed}", path, certificate.Subject, installed);
                    return new CertificateInfo
                    {
                        Path = path,
                        FileName = Path.GetFileName(path),
                        Subject = certificate.Subject,
                        NotAfter = certificate.NotAfter,
                        Installed = installed,
                    };
                }
            }, cancellationToken);
        }

        public Task ImportAsync(string sourcePath, string? password, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new CertificateValidationException(Lang.CertificatesStatusSelectPfxFirst);
            }

            if (!File.Exists(sourcePath))
            {
                throw new CertificateValidationException(Lang.CertificatesStatusSelectPfxFirst);
            }

            return Task.Run(() =>
            {
                try
                {
                    using var probe = Load(sourcePath, password);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Certificate validation failed, path={Path}", sourcePath);
                    throw new CertificateException(string.Format(Lang.CertificatesStatusValidateFailed, e.Message), e);
                }

                var targetPath = CertificatePath;
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    throw new CertificateValidationException(string.Format(Lang.CertificatesStatusImportFailed, Lang.CertificatesNoCertificatePlaceholder));
                }

                try
                {
                    BackupIfExists(targetPath);
                    File.Copy(sourcePath, targetPath, true);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Certificate import failed, source={Source}, target={Target}", sourcePath, targetPath);
                    throw new CertificateException(string.Format(Lang.CertificatesStatusImportFailed, e.Message), e);
                }

                Log.Info("Certificate imported, source={Source}, target={Target}", sourcePath, targetPath);
            }, cancellationToken);
        }

        public async Task<CertificateGenerationResult> GenerateAsync(string? subject, string? password, string? validDaysText, CancellationToken cancellationToken)
        {
            var resolvedSubject = string.IsNullOrWhiteSpace(subject) ? DefaultSubject : subject.Trim();
            var resolvedPassword = string.IsNullOrWhiteSpace(password) ? DefaultPassword : password;
            var resolvedValidDays = ResolveValidDays(validDaysText);

            var targetPath = CertificatePath;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new CertificateValidationException(string.Format(Lang.CertificatesStatusCopyFailed, Lang.CertificatesNoCertificatePlaceholder));
            }

            var workingFolder = Path.Combine(Path.GetTempPath(), CliWorkingFolderName);
            Directory.CreateDirectory(workingFolder);

            var outputPath = Path.Combine(workingFolder, CliOutputFileName);

            var arguments = new StringBuilder("cert generate");
            arguments.Append(" --output \"").Append(CliOutputFileName).Append('"');
            arguments.Append(" --valid-days ").Append(resolvedValidDays);
            arguments.Append(" --if-exists Overwrite");
            arguments.Append(" --verbose");
            arguments.Append(" --publisher \"").Append(resolvedSubject).Append('"');
            arguments.Append(" --password \"").Append(resolvedPassword).Append('"');

            var result = await WinAppCliService.RunAsync(arguments.ToString(), workingFolder).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!result.Result)
            {
                Log.Error("Certificate generation failed, output={Output}", result.Output);
                throw new CertificateException(string.Format(Lang.CertificatesStatusCopyFailed, result.Output));
            }

            if (!File.Exists(outputPath))
            {
                Log.Error("Certificate generation produced no file, path={Path}", outputPath);
                throw new CertificateException(result.Output + Environment.NewLine + Lang.CertificatesStatusGeneratedFileMissing);
            }

            try
            {
                BackupIfExists(targetPath);
                File.Copy(outputPath, targetPath, true);
            }
            catch (Exception e)
            {
                Log.Error(e, "Certificate copy failed, target={Target}", targetPath);
                throw new CertificateException(result.Output + Environment.NewLine + string.Format(Lang.CertificatesStatusCopyFailed, e.Message), e);
            }

            Log.Info("Certificate generated, subject={Subject}, target={Target}", resolvedSubject, targetPath);
            return new CertificateGenerationResult
            {
                Output = result.Output + Environment.NewLine + string.Format(Lang.CertificatesStatusCopySuccess, targetPath),
                Password = resolvedPassword,
            };
        }

        public Task<string> InstallToStoreAsync(string? password, CancellationToken cancellationToken)
        {
            var path = CertificatePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new CertificateValidationException(Lang.CertificatesStatusFileMissing);
            }

            if (!File.Exists(path))
            {
                throw new CertificateValidationException(Lang.CertificatesStatusFileMissing);
            }

            return Task.Run(() =>
            {
                try
                {
                    using var certificate = Load(path, password);
                    using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(certificate);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Certificate install failed, path={Path}", path);
                    throw new CertificateException(string.Format(Lang.CertificatesStatusInstallFailed, e.Message), e);
                }

                Log.Info("Certificate installed, store={Store}, path={Path}", StoreName.TrustedPeople, path);
                return string.Format(Lang.CertificatesStatusInstallLocalSuccess, StoreName.TrustedPeople, path);
            }, cancellationToken);
        }

        public async Task<string> InstallWithCliAsync(string? password, CancellationToken cancellationToken)
        {
            var path = CertificatePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new CertificateValidationException(Lang.CertificatesStatusFileMissing);
            }

            if (!File.Exists(path))
            {
                throw new CertificateValidationException(Lang.CertificatesStatusFileMissing);
            }

            var arguments = new StringBuilder("cert install ");
            arguments.Append('"').Append(path).Append('"');

            if (!string.IsNullOrEmpty(password))
            {
                arguments.Append(" --password \"").Append(password).Append('"');
            }

            var workingFolder = Path.GetDirectoryName(path);
            var result = await WinAppCliService.RunAsAdmin2Async(arguments.ToString(), workingFolder).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!result.Result)
            {
                Log.Error("Certificate CLI install failed, output={Output}", result.Output);
                throw new CertificateException(string.Format(Lang.CertificatesStatusInstallFailed, result.Output));
            }

            Log.Info("Certificate CLI install finished, path={Path}", path);
            return result.Output;
        }

        private static int ResolveValidDays(string? validDaysText)
        {
            if (string.IsNullOrWhiteSpace(validDaysText))
            {
                return 365;
            }

            if (!int.TryParse(validDaysText.Trim(), out var parsed))
            {
                return 365;
            }

            if (parsed <= 0)
            {
                return 365;
            }

            return parsed;
        }

        private static X509Certificate2 Load(string path, string? password)
        {
            var flags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet;
            return new X509Certificate2(path, password, flags);
        }

        private static bool IsInstalled(X509Certificate2 certificate)
        {
            foreach (var store in TrackedStores)
            {
                if (IsInStore(store, StoreLocation.LocalMachine, certificate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInStore(StoreName storeName, StoreLocation location, X509Certificate2 certificate)
        {
            try
            {
                using var store = new X509Store(storeName, location);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                return store.Certificates.Cast<X509Certificate2>().Any(c => string.Equals(c.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e)
            {
                Log.Debug(e, "Certificate store read failed, store={Store}", storeName);
                return false;
            }
        }

        private static void BackupIfExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!File.Exists(path))
            {
                return;
            }

            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(path);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = Path.Combine(directory, fileName + "_" + timestamp + ".bak");
            File.Move(path, backupPath, false);
            Log.Info("Certificate backup created, path={Path}", backupPath);
        }
    }
}
