using System;
using Windows.Management.Core;
using Windows.Storage;

namespace ContextMenuCustomApp.Common
{
    public class PackageAppDataSettings
    {
        private readonly ApplicationDataContainer _container;

        public PackageAppDataSettings(string packageFamilyName)
            : this(packageFamilyName, null)
        {
        }

        public PackageAppDataSettings(string packageFamilyName, string? typeName)
        {
            if (string.IsNullOrWhiteSpace(packageFamilyName))
            {
                throw new ArgumentException("packageFamilyName is empty", nameof(packageFamilyName));
            }

            var localSettings = OpenLocalSettings(packageFamilyName);
            _container = string.IsNullOrWhiteSpace(typeName)
                ? localSettings
                : localSettings.CreateContainer(typeName, ApplicationDataCreateDisposition.Always);
        }

        public T GetValue<T>(string propertyName, T defaultValue)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return defaultValue;
            }

            if (!_container.Values.TryGetValue(propertyName, out var value) || null == value)
            {
                return defaultValue;
            }

            return value is T typed ? typed : defaultValue;
        }

        public bool SetValue<T>(string propertyName, T value)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            _container.Values[propertyName] = value;
            return true;
        }

        private static ApplicationDataContainer OpenLocalSettings(string packageFamilyName)
        {
            var appData = ApplicationDataManager.CreateForPackageFamily(packageFamilyName);
            if (null == appData)
            {
                throw new InvalidOperationException($"ApplicationData not found for {packageFamilyName}");
            }

            return appData.LocalSettings;
        }
    }
}
