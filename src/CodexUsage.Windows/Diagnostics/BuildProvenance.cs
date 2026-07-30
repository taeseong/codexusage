using System.Reflection;
using System.Text.RegularExpressions;

namespace CodexUsage.Windows.Diagnostics;

internal sealed record BuildProvenance(string Version, string Revision)
{
    private const string UnknownRevision = "local build";

    public static BuildProvenance FromEntryAssembly() =>
        FromAssembly(Assembly.GetEntryAssembly());

    internal static BuildProvenance FromAssembly(Assembly? assembly)
    {
        var version = assembly?.GetName().Version?.ToString(3) ?? "0.1.3";
        var informationalVersion = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return FromValues(version, informationalVersion);
    }

    internal static BuildProvenance FromValues(string version, string? informationalVersion)
    {
        var revision = UnknownRevision;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var match = Regex.Match(
                informationalVersion,
                @"\+([0-9a-fA-F]{7,40})(?:\.|$)",
                RegexOptions.CultureInvariant);
            if (match.Success)
            {
                revision = match.Groups[1].Value[..Math.Min(12, match.Groups[1].Value.Length)]
                    .ToLowerInvariant();
            }
        }

        return new BuildProvenance(version, revision);
    }
}
