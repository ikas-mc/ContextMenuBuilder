using System;
using Windows.Storage.Streams;

namespace ContextMenuBuilder
{
    public sealed class Win11ShellPackageRow
    {
        public required string IdName { get; init; }

        public required string IdFullName { get; init; }

        public required string PackageDisplayName { get; init; }

        public required string ApplicationDisplayName { get; init; }

        public required string InstallPath { get; init; }

        public required int ItemCount { get; init; }

        public RandomAccessStreamReference? PackageLogo { get; init; }

        public string DisplayLabel
        {
            get
            {
                if (string.IsNullOrEmpty(ApplicationDisplayName))
                {
                    return PackageDisplayName;
                }

                return ApplicationDisplayName;
            }
        }

        public string ItemCountLabel => string.Concat(IdName, " : ", ItemCount);
    }
}
