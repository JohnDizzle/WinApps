using System.Xml;
using System.Xml.Linq;
using MyApps.Models;

namespace MyApps.Services.Packages;

public sealed class PackageManifestInspector
{
    public const int MaxManifestCharacters = 2 * 1024 * 1024;
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace LegacyFoundation = "http://schemas.microsoft.com/appx/2010/manifest";
    private static readonly XNamespace Uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
    private static readonly XNamespace Uap18 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/18";
    private static readonly XNamespace Restricted = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

    public PackageConfiguration Parse(string manifest, string applicationId, string path = "")
    {
        if (manifest.Length > MaxManifestCharacters) throw new XmlException("Manifest exceeds the 2 MiB character limit.");
        var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = MaxManifestCharacters };
        using (var validation = XmlReader.Create(new StringReader(manifest), options))
            while (validation.Read())
                if (validation.Depth > 128) throw new XmlException("Manifest exceeds the supported XML nesting depth.");
        using var reader = XmlReader.Create(new StringReader(manifest), options);
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new XmlException("Manifest has no package element.");
        if (root.Name.LocalName != "Package" || (root.Name.Namespace != Foundation && root.Name.Namespace != LegacyFoundation))
            throw new XmlException("Document is not a supported Windows package manifest.");
        var ns = root.Name.Namespace;
        var applications = root.Element(ns + "Applications")?.Elements(ns + "Application").ToArray() ?? [];
        var selected = applications.SingleOrDefault(item => (string?)item.Attribute("Id") == applicationId);
        var notices = new List<string>();
        var resourceOnly = applications.Length == 0 && applicationId.Length == 0 &&
            string.Equals(root.Element(ns + "Properties")?.Element(ns + "ResourcePackage")?.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        if (selected is null && !resourceOnly) notices.Add($"Application Id '{applicationId}' was not found. Package declarations are still shown; app classification is unknown.");

        (string runtime, string trust) Classify(XElement? application)
        {
            if (application is null) return ("Unknown", "Unknown");
            var entry = (string?)application.Attribute("EntryPoint");
            var runtime = (string?)application.Attribute(Uap18 + "RuntimeBehavior") ?? (string?)application.Attribute(Uap10 + "RuntimeBehavior");
            var trust = (string?)application.Attribute(Uap18 + "TrustLevel") ?? (string?)application.Attribute(Uap10 + "TrustLevel");
            var fullTrust = string.Equals(entry, "windows.fullTrustApplication", StringComparison.OrdinalIgnoreCase);
            var partialTrust = string.Equals(entry, "windows.partialTrustApplication", StringComparison.OrdinalIgnoreCase);
            var inferredRuntime = fullTrust || partialTrust ? "packagedClassicApp" :
                !string.IsNullOrEmpty(entry) || application.Attribute("StartPage") is not null ? "windowsApp" : null;
            var inferredTrust = fullTrust ? "mediumIL" : "appContainer";
            if (runtime is not null && inferredRuntime is not null && runtime != inferredRuntime)
                notices.Add($"Application {application.Attribute("Id")?.Value}: RuntimeBehavior conflicts with EntryPoint; declarations are shown as written.");
            if (trust is not null && (fullTrust || partialTrust) && trust != inferredTrust)
                notices.Add($"Application {application.Attribute("Id")?.Value}: TrustLevel conflicts with EntryPoint; declarations are shown as written.");
            var effectiveRuntime = runtime ?? inferredRuntime;
            var type = effectiveRuntime switch
            {
                "windowsApp" => "UWP (windowsApp)",
                "packagedClassicApp" => "Packaged desktop (packagedClassicApp)",
                "win32App" => "Win32 (win32App)",
                null => "Unknown — runtime behavior is not established",
                _ => $"Other runtime: {effectiveRuntime}"
            };
            var effectiveTrust = trust ?? (effectiveRuntime is not null ? inferredTrust : null);
            if (effectiveRuntime == "win32App" && effectiveTrust == "appContainer")
                notices.Add($"Application {application.Attribute("Id")?.Value}: win32App with appContainer is not a supported combination.");
            return (effectiveRuntime is null ? type : $"{type} · {(runtime is null ? "inferred from legacy activation declarations" : "declared RuntimeBehavior")}",
                effectiveTrust is null ? "Unknown" : $"{effectiveTrust} · {(trust is null ? "inferred/default; inspect activation declarations" : "declared TrustLevel")}");
        }

        var classification = Classify(selected);
        if (resourceOnly) classification = ("Resource package (declared ResourcePackage)", "No Application activation declarations");
        var dependencies = root.Element(ns + "Dependencies")?.Elements(ns + "PackageDependency").ToArray() ?? [];
        var appSdk = dependencies.Where(item => ((string?)item.Attribute("Name"))?.StartsWith("Microsoft.WindowsAppRuntime.", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var ui = appSdk.Length > 0
            ? "Windows App SDK dependency declared; WinUI 3 usage is possible but not confirmed by the manifest."
            : "UI framework unknown. A manifest alone cannot confirm WinUI 3; self-contained apps may have no Windows App SDK package dependency.";
        var sections = new List<PackageConfigurationSection>();
        if (!resourceOnly) sections.Add(new("Selected application", selected is null ? "No matching Application declaration." : selected.ToString()));
        sections.Add(new("Application activation and isolation", string.Join("\n\n", applications.Select(item =>
        {
            var result = item == selected ? classification : Classify(item);
            return $"Application Id: {item.Attribute("Id")?.Value}\nExecutable: {item.Attribute("Executable")?.Value ?? "Not declared"}\nEntryPoint: {item.Attribute("EntryPoint")?.Value ?? "Not declared"}\nRuntime: {result.runtime}\nTrust / AppContainer: {result.trust}";
        }))));
        foreach (var group in root.Elements().GroupBy(element => element.Name))
        {
            var title = group.Key.LocalName switch
            {
                "Identity" => "Package identity and architecture", "Properties" => "Package properties",
                "Dependencies" => "Dependencies and target device families", "Capabilities" => "Capabilities and permissions",
                "Applications" => "All applications and app extensions", "Extensions" => "Package extensions",
                "Resources" => "Resources and languages", _ => group.Key.LocalName
            };
            sections.Add(new(title, string.Join("\n\n", group.Select(element => element.ToString()))));
        }
        sections.Add(new("Manifest namespaces and compatibility", string.Join("\n", root.Attributes().Select(attribute => $"{attribute.Name}: {attribute.Value}"))));
        notices.Add("Capabilities are package declarations, not a report of granted permissions. Extension activation can override the application trust/runtime. Runtime process tokens and UI framework binaries are not inspected.");
        var allowsElevation = selected is not null && classification.trust.StartsWith("mediumIL", StringComparison.Ordinal) &&
            (classification.runtime.StartsWith("Packaged desktop", StringComparison.Ordinal) || classification.runtime.StartsWith("Win32", StringComparison.Ordinal)) &&
            root.Element(ns + "Capabilities")?.Elements(Restricted + "Capability").Any(item => (string?)item.Attribute("Name") == "allowElevation") == true;
        var launch = selected is null ? null : new PackageLaunchInfo(applicationId, (string?)selected.Attribute("Executable") ?? "", allowsElevation);
        return new(path, classification.runtime, classification.trust, ui, sections, manifest, notices, Launch: launch);
    }
}
