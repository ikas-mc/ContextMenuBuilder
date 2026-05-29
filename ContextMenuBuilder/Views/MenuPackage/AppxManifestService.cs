using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Windows.Management.Deployment;

namespace ContextMenuBuilder
{
    /// <summary>
    /// Centralised service for reading and writing AppxManifest.xml files.
    /// </summary>
    internal static class AppxManifestService
    {
        // ── Public: read ─────────────────────────────────────────────

        /// <summary>
        /// Reads <see cref="AppxManifestInfo"/> from a manifest at the given
        /// absolute file path.  Returns <c>null</c> if the file does not exist.
        /// </summary>
        /// <remarks>
        /// Returns <c>null</c> only when the file does not exist.
        /// All other failures (malformed XML, I/O errors) propagate as exceptions.
        /// </remarks>
        public static AppxManifestInfo? ReadFromPath(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            var doc = XDocument.Load(manifestPath);
            return ParseDocument(doc);
        }

        /// <summary>
        /// Looks up the installed package by <paramref name="idFullName"/> via
        /// <see cref="PackageManager"/>, then reads
        /// <see cref="AppxManifestInfo"/> from its install directory.
        /// Returns <c>null</c> if the package or its manifest cannot be found.
        /// </summary>
        public static AppxManifestInfo? ReadFromInstalledPackage(string idFullName)
        {
            var manager = new PackageManager();
            var pkg = manager.FindPackageForUser(null, idFullName);
            var installPath = pkg?.InstalledLocation?.Path;
            if (string.IsNullOrWhiteSpace(installPath))
            {
                return null;
            }

            var manifestPath = Path.Combine(installPath, "AppxManifest.xml");
            return ReadFromPath(manifestPath);
        }

        // ── Public: write ────────────────────────────────────────────

        /// <summary>
        /// Applies the fields of <paramref name="info"/> to the manifest at
        /// <paramref name="manifestPath"/>, writing every non-null property in a
        /// single load/save cycle.
        /// </summary>
        public static void Update(string manifestPath, AppxManifestInfo info)
        {
            var doc = XDocument.Load(manifestPath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var uapNs = doc.Root?.GetNamespaceOfPrefix("uap")
                        ?? (XNamespace)"http://schemas.microsoft.com/appx/manifest/uap/windows10";

            // Identity
            if (info.IdName is not null || info.IdVersion is not null || info.IdPublisher is not null)
            {
                var identity = doc.Root?.Element(ns + "Identity")
                    ?? throw new InvalidOperationException("Manifest 缺少 Identity 元素");

                if (info.IdName is not null)
                {
                    identity.SetAttributeValue("Name", info.IdName);
                }

                if (info.IdVersion is not null)
                {
                    identity.SetAttributeValue("Version", info.IdVersion);
                }

                if (info.IdPublisher is not null)
                {
                    identity.SetAttributeValue("Publisher", info.IdPublisher);
                }
            }

            // VisualElements
            var visual = doc.Descendants(uapNs + "VisualElements").FirstOrDefault();
            if (visual is not null)
            {
                if (info.ApplicationDisplayName is not null)
                {
                    visual.SetAttributeValue("DisplayName", info.ApplicationDisplayName);
                }

                if (info.AppListEntry)
                {
                    visual.SetAttributeValue("AppListEntry", info.AppListEntry ? "default" : "none");
                }
            }

            // COM Class Id / Clsid references
            if (info.ClassId is not null)
            {
                ApplyClassId(doc, info.ClassId);
            }

            doc.Save(manifestPath);
        }

        // ── Public: full-text ────────────────────────────────────────

        /// <summary>
        /// Reads the entire manifest file as a UTF-8 string.
        /// Returns <c>null</c> only when the file does not exist.
        /// </summary>
        public static string? ReadFullText(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            return File.ReadAllText(manifestPath, Encoding.UTF8);
        }

        /// <summary>
        /// Writes <paramref name="content"/> verbatim to
        /// <paramref name="manifestPath"/>, overwriting any existing file.
        /// </summary>
        public static void SaveFullText(string manifestPath, string content)
        {
            File.WriteAllText(manifestPath, content, Encoding.UTF8);
        }

        // ── Private helpers ──────────────────────────────────────────

        private static AppxManifestInfo ParseDocument(XDocument doc)
        {
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var uapNs = doc.Root?.GetNamespaceOfPrefix("uap")
                        ?? (XNamespace)"http://schemas.microsoft.com/appx/manifest/uap/windows10";

            var identity = doc.Root?.Element(ns + "Identity");
            var visual = doc.Descendants(uapNs + "VisualElements").FirstOrDefault();

            // AppListEntry
            bool appListEntry = true;
            if (visual is not null)
            {
                var attr = visual.Attribute(uapNs + "AppListEntry") ?? visual.Attribute("AppListEntry");
                if (attr is not null)
                {
                    appListEntry = !attr.Value.Equals("none", StringComparison.OrdinalIgnoreCase);
                }
            }

            // COM Class Id
            var classElement = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Class", StringComparison.OrdinalIgnoreCase));

            return new AppxManifestInfo
            {
                IdName = identity?.Attribute("Name")?.Value,
                IdVersion = identity?.Attribute("Version")?.Value,
                IdPublisher = identity?.Attribute("Publisher")?.Value,
                ApplicationDisplayName = visual?.Attribute("DisplayName")?.Value,
                AppListEntry = appListEntry,
                ClassId = classElement?.Attribute("Id")?.Value,
            };
        }

        private static void ApplyClassId(XDocument doc, string classId)
        {
            // All Clsid attributes
            foreach (var attr in doc.Descendants().Attributes())
            {
                if (attr.Name.LocalName.Equals("Clsid", StringComparison.OrdinalIgnoreCase))
                {
                    attr.Value = classId;
                }
            }

            // <Class Id="…">
            foreach (var cls in doc.Descendants()
                         .Where(e => e.Name.LocalName.Equals("Class", StringComparison.OrdinalIgnoreCase)))
            {
                var idAttr = cls.Attribute("Id");
                if (idAttr is not null)
                {
                    idAttr.Value = classId;
                }
            }

            // <SurrogateServer DisplayName="…">
            foreach (var surrogate in doc.Descendants()
                         .Where(e => e.Name.LocalName.Equals("SurrogateServer", StringComparison.OrdinalIgnoreCase)))
            {
                var displayAttr = surrogate.Attribute("DisplayName");
                if (displayAttr is null)
                {
                    surrogate.Add(new XAttribute("DisplayName", classId));
                }
                else
                {
                    displayAttr.Value = classId;
                }
            }
        }
    }
}
