namespace ContextMenuBuilder.Service.MenuPackage
{
    public sealed record AppxManifestInfo
    {
        public string? IdName { get; init; }

        public string? IdVersion { get; init; }

        public string? IdPublisher { get; init; }

        public string? ApplicationDisplayName { get; init; }

        public bool AppListEntry { get; init; } = true;

        public string? ClassId { get; init; }
    }
}
