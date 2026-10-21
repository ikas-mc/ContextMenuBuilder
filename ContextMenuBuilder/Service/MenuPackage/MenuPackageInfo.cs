namespace ContextMenuBuilder.Service.MenuPackage
{
    public sealed record MenuPackageInfo
    {
        public required string IdName { get; init; }

        public required string IdFullName { get; init; }

        public required string DisplayName { get; init; }

        public required string Publisher { get; init; }

        public required string Version { get; init; }

        public required string FamilyName { get; init; }

        public required string ApplicationDisplayName { get; init; }

        public required string InstallPath { get; init; }
    }
}
