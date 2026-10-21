using System;

namespace ContextMenuBuilder.Service.Certificate
{
    public sealed record CertificateInfo
    {
        public required string Path { get; init; }

        public required string FileName { get; init; }

        public required string Subject { get; init; }

        public required DateTime NotAfter { get; init; }

        public required bool Installed { get; init; }
    }
}
