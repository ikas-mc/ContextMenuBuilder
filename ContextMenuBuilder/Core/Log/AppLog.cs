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
        private static readonly object SyncRoot = new();
        private static string? _logDirectory;
        private static bool _initialized;

        public static string LogDirectory
        {
            get
            {
                EnsureInitialized();
                return _logDirectory ?? string.Empty;
            }
        }

        public static ILogger For<T>()
        {
            EnsureInitialized();
            var name = typeof(T).FullName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return LogManager.GetLogger(typeof(T).Name);
            }

            return LogManager.GetLogger(name);
        }

        public static ILogger For(string name)
        {
            EnsureInitialized();
            return LogManager.GetLogger(name);
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                Initialize();
                _initialized = true;
            }
        }

        private static void Initialize()
        {
            try
            {
                var directory = ResolveLogDirectory();
                Directory.CreateDirectory(directory);
                _logDirectory = directory;
                LogManager.Configuration = BuildConfiguration(directory);
                LogManager.ReconfigExistingLoggers();
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
            try
            {
                return Path.Combine(ApplicationData.Current.LocalFolder.Path, LogFolderName);
            }
            catch (Exception)
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "ContextMenuBuilder", LogFolderName);
            }
        }
    }
}
