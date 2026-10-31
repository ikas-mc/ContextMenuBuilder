using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Windows.Storage;

namespace ContextMenuBuilder.Core.Log
{
    public static class AppLog
    {
        private const string LogFolderName = "logs";
        private static string? _logDirectory;

        public static ILogger GetLogger(string name)
        {
            return LogManager.GetLogger(name);
        }

        public static void Initialize()
        {
            try
            {
                var directory = ResolveLogDirectory();
                Directory.CreateDirectory(directory);
                _logDirectory = directory;
                LogManager.Configuration = BuildConfiguration(directory);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private static LoggingConfiguration BuildConfiguration(string directory)
        {
            var configuration = new LoggingConfiguration();

            var fileTarget = new FileTarget("file")
            {
                FileName = Path.Combine(directory, "app-${shortdate}.log"),
                Layout = "${longdate}|${level:uppercase=true}|${logger:shortName=true}|${threadid}|${message:withexception=true}",
                Encoding = Encoding.UTF8,
                CreateDirs = true,
                KeepFileOpen = false,
                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveFileName = Path.Combine(directory, "app-${shortdate}.log"),
                MaxArchiveFiles = 14,
            };

            var minLevel = LogLevel.Info;
#if DEBUG
            minLevel = LogLevel.Debug;

            var debuggerTarget = new DebuggerTarget("debugger")
            {
                Layout = "${level:uppercase=true}|${logger:shortName=true}|${message:withexception=true}",
            };

            configuration.AddTarget(debuggerTarget);
            configuration.AddRule(minLevel, LogLevel.Fatal, debuggerTarget);
#endif

            configuration.AddTarget(fileTarget);
            configuration.AddRule(minLevel, LogLevel.Fatal, fileTarget);

            return configuration;
        }

        private static string ResolveLogDirectory()
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, LogFolderName);
        }
    }
}
