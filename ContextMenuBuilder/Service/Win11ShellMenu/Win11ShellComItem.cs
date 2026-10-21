using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Windows.Storage.Streams;

namespace ContextMenuBuilder.Service.Win11ShellMenu
{
    public sealed partial class Win11ShellComItem : ObservableObject
    {
        public required string MenuComId { get; init; }

        public required string MenuName { get; init; }

        public required string IdName { get; init; }

        public required string IdFullName { get; init; }

        public required string PackageDisplayName { get; init; }

        public required string ApplicationDisplayName { get; init; }

        public required string PackageInstallPath { get; init; }

        public RandomAccessStreamReference? PackageLogo { get; init; }

        [ObservableProperty]
        public partial bool IsEnabled
        {
            get; set;
        }

        public bool Matches(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            if (MenuComId.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (MenuName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (PackageDisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ApplicationDisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IdFullName.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
