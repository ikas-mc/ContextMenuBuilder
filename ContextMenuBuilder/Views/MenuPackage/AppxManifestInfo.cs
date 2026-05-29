namespace ContextMenuBuilder
{
    /// <summary>
    /// Represents the parsed contents of an AppxManifest.xml that are
    /// relevant to menu-package building and editing.
    /// </summary>
    public record AppxManifestInfo
    {
        // ── Identity ────────────────────────────────────────────────
        public string? IdName { get; set; }
        public string? IdVersion { get; set; }
        public string? IdPublisher { get; set; }

        // ── Application visual ──────────────────────────────────────
        /// <summary>DisplayName attribute on uap:VisualElements.</summary>
        public string? ApplicationDisplayName { get; set; }

        /// <summary>
        /// AppListEntry attribute on uap:VisualElements.
        /// <c>true</c> = "default" (show in Start), <c>false</c> = "none".
        /// <c>null</c> = attribute not present / file not readable.
        /// </summary>
        public bool AppListEntry { get; set; }

        // ── COM / shell extension ────────────────────────────────────
        /// <summary>
        /// The COM Class Id (first &lt;Class Id="…"&gt; found).
        /// Used as both the Class Id and all Clsid attribute values.
        /// </summary>
        public string? ClassId { get; set; }
    }
}
