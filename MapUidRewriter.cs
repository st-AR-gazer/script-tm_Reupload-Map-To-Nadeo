using System.Text.RegularExpressions;
using GBX.NET;
using GBX.NET.Engines.Game;

namespace ReuploadMapToNadeo;

internal static class MapUidRewriter
{
    public static MapRewriteResult Rewrite(string originalMapPath, string newMapPath, string outputPath)
    {
        ValidateInputMapPath(originalMapPath, "original");
        ValidateInputMapPath(newMapPath, "new");
        ValidateOutputMapPath(outputPath);

        if (PathEquals(originalMapPath, newMapPath))
        {
            throw new InvalidOperationException("The original map and new map must be different files.");
        }

        CGameCtnChallenge originalMap = Gbx.Parse<CGameCtnChallenge>(originalMapPath).Node;

        string originalUid = RequireUid(originalMap.MapUid, "original map");
        ValidateXmlMatchesMapUid(originalMap, originalUid, "original map");

        return RewriteCore(originalUid, originalMap.MapName, newMapPath, outputPath);
    }

    public static MapRewriteResult RewriteFromUid(string originalUid, string newMapPath, string outputPath)
    {
        ValidateInputMapPath(newMapPath, "new");
        ValidateOutputMapPath(outputPath);

        return RewriteCore(MapUidInput.Normalize(originalUid), originalMapName: null, newMapPath, outputPath);
    }

    private static MapRewriteResult RewriteCore(
        string originalUid,
        string? originalMapName,
        string newMapPath,
        string outputPath)
    {
        var newGbx = Gbx.Parse<CGameCtnChallenge>(newMapPath);
        CGameCtnChallenge newMap = newGbx.Node;
        string previousUid = RequireUid(newMap.MapUid, "new map");
        ValidateXmlMatchesMapUid(newMap, previousUid, "new map");

        newMap.MapUid = originalUid;
        newMap.Xml = MapHeaderXml.ReplaceUid(
            newMap.Xml ?? throw new InvalidDataException("The new map has no XML header."),
            originalUid);

        string fullOutputPath = Path.GetFullPath(outputPath);
        string outputDirectory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new InvalidOperationException("The output path has no parent directory.");
        Directory.CreateDirectory(outputDirectory);

        string temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            newGbx.Save(temporaryPath);
            VerifySavedMap(temporaryPath, originalUid);
            File.Move(temporaryPath, fullOutputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return new MapRewriteResult(
            originalMapName,
            newMap.MapName,
            originalUid,
            previousUid,
            fullOutputPath);
    }

    private static void ValidateXmlMatchesMapUid(CGameCtnChallenge map, string expectedUid, string description)
    {
        string xml = map.Xml ?? throw new InvalidDataException($"The {description} has no XML header.");
        string xmlUid = MapHeaderXml.GetUid(xml);

        if (!string.Equals(xmlUid, expectedUid, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The {description} is internally inconsistent: its GBX UID is '{expectedUid}', " +
                $"but its XML-header UID is '{xmlUid}'.");
        }
    }

    private static void VerifySavedMap(string path, string expectedUid)
    {
        CGameCtnChallenge header = Gbx.ParseHeaderNode<CGameCtnChallenge>(path);
        if (!string.Equals(header.MapUid, expectedUid, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Saved-map verification failed: header UID '{header.MapUid}' does not match '{expectedUid}'.");
        }

        CGameCtnChallenge body = Gbx.ParseNode<CGameCtnChallenge>(path);
        if (!string.Equals(body.MapUid, expectedUid, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Saved-map verification failed: body UID '{body.MapUid}' does not match '{expectedUid}'.");
        }

        string xmlUid = MapHeaderXml.GetUid(
            body.Xml ?? throw new InvalidDataException("Saved-map verification failed: XML header is missing."));

        if (!string.Equals(xmlUid, expectedUid, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Saved-map verification failed: XML UID '{xmlUid}' does not match '{expectedUid}'.");
        }
    }

    private static string RequireUid(string? uid, string description)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new InvalidDataException($"The {description} has no map UID.");
        }

        return uid;
    }

    private static void ValidateInputMapPath(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The {description} map does not exist.", path);
        }

        if (!IsMapFileName(path))
        {
            throw new InvalidOperationException(
                $"The {description} map must have a .Map.Gbx file name: '{path}'.");
        }
    }

    private static void ValidateOutputMapPath(string path)
    {
        if (!IsMapFileName(path))
        {
            throw new InvalidOperationException($"The output must have a .Map.Gbx file name: '{path}'.");
        }
    }

    private static bool IsMapFileName(string path)
    {
        return Path.GetFileName(path).EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}

internal sealed record MapRewriteResult(
    string? OriginalMapName,
    string NewMapName,
    string OriginalUid,
    string PreviousUid,
    string OutputPath);

internal static partial class MapUidInput
{
    public static string Normalize(string? uid)
    {
        string normalized = String.IsNullOrWhiteSpace(uid) ? string.Empty : uid.Trim();
        if (!UidPattern().IsMatch(normalized))
        {
            throw new InvalidDataException(
                "The original map UID must be 1-128 ASCII letters, numbers, hyphens, or underscores.");
        }

        return normalized;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex UidPattern();
}

internal static partial class MapHeaderXml
{
    public static string GetUid(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        MatchCollection matches = IdentUidPattern().Matches(xml);
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                $"Expected exactly one <ident> UID in the map XML header, found {matches.Count}.");
        }

        return matches[0].Groups["uid"].Value;
    }

    public static string ReplaceUid(string xml, string uid)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);

        MatchCollection matches = IdentUidPattern().Matches(xml);
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                $"Expected exactly one <ident> UID in the map XML header, found {matches.Count}.");
        }

        return IdentUidPattern().Replace(
            xml,
            match => string.Concat(
                match.Groups["prefix"].Value,
                match.Groups["quote"].Value,
                uid,
                match.Groups["quote"].Value),
            count: 1);
    }

    [GeneratedRegex(
        @"(?<prefix><ident\b[^>]*\buid\s*=\s*)(?<quote>[""'])(?<uid>[^""']*)(?:\k<quote>)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IdentUidPattern();
}
