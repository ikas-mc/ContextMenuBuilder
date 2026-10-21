namespace ContextMenuBuilder.Service.Template
{
    public enum TemplateAvailability
    {
        None,
        MissingManifest,
        MissingIdentity,
        Ready,
    }

    public sealed record TemplateInfo
    {
        public required TemplateAvailability Availability { get; init; }

        public required string Name { get; init; }

        public required string Publisher { get; init; }

        public required string Version { get; init; }

        public required string Architecture { get; init; }

        public required string DisplayName { get; init; }
    }
}
