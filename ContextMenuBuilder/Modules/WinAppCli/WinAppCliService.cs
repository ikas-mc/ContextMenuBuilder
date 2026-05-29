using ContextMenuCustomApp.Common;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ContextMenuBuilder
{
    internal static class WinAppCliService
    {
        private static AppLang Lang => AppContext.AppLang;

        private static string GetWinAppCliExe()
        {
            return AppContext.AppSettings.WinAppCliPath ?? "winapp.exe";
        }

        public record RunResult(bool Result, string Output)
        {
        }

        public static Task<RunResult> RunAsync(string arguments, string? workingDirectory = null)
        {
            return RunAsyncInner(arguments, workingDirectory, false);
        }

        public static Task<RunResult> RunAsAdminAsync(string arguments, string? workingDirectory = null)
        {
            return RunAsyncInner(arguments, workingDirectory, true);
        }

        private static Task<RunResult> RunAsyncInner(string arguments, string? workingDirectory = null, bool runAsAdmin = false)
        {
            var cliPath = GetWinAppCliExe();

            return Task.Run(() =>
            {
                var fileName = runAsAdmin ? "sudo" : cliPath;
                var finalArguments = runAsAdmin ? $"{Quote(cliPath)} {arguments}" : arguments;

                var psi = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true,
                };

                string output = string.Empty;
                bool result = false;
                try
                {
                    using var process = Process.Start(psi);
                    if (process is null)
                    {
                        throw new InvalidOperationException(string.Format(Lang.WinAppCliServiceStartFailed, cliPath));
                    }

                    output += process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    var exitCode = process.ExitCode;

                    result = exitCode == 0;


                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        output += "\n}error:\n" + error;
                    }

                    if (!result)
                    {
                        output += "\n}exitCode:\n" + exitCode;
                    }
                }
                catch (Exception)
                {
                    throw;
                }

                return new RunResult(result, output);
            });
        }

        public static Task<RunResult> RunAsAdmin2Async(string arguments, string? workingDirectory = null, bool runAsAdmin = false)
        {
            var cliPath = GetWinAppCliExe();

            return Task.Run(() =>
            {
                var fileName = runAsAdmin ? "sudo" : cliPath;
                var finalArguments = runAsAdmin ? $"{Quote(cliPath)} {arguments}" : arguments;

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = finalArguments,
                    WorkingDirectory = workingDirectory,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    Verb = "runas"
                };

                using var process = Process.Start(psi) ?? throw new InvalidOperationException(string.Format(Lang.WinAppCliServiceStartFailed, cliPath));
                process.WaitForExit();

                string output = string.Empty;
                bool result = false;

                var exitCode = process.ExitCode;

                result = exitCode == 0;

                if (!result)
                {
                    output += "\nexitCode:\n" + exitCode;
                }

                return new RunResult(result, output);
            });
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Contains(' ') ? $"\"{value}\"" : value;
        }
    }
}
