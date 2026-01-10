using System.Diagnostics;
using System.IO;
using TinyPreprocessor.Core;

namespace TinyAst.Preprocessor.Bridge.Resources;

public static class ResourceIdPathResolver
{
    public static ResourceId Resolve<TContent>(string reference, IResource<TContent>? context) => Resolve(reference, context?.Id);

    public static ResourceId Resolve(string reference, ResourceId? contextId)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return new ResourceId(string.Empty);
        }

        // Treat absolute URIs and fully-qualified OS paths as "already resolved".
        if (IsAbsoluteUri(reference) || Path.IsPathFullyQualified(reference))
        {
            return new ResourceId(reference);
        }

        var isRooted = Path.IsPathRooted(reference);

        if (isRooted || contextId is null)
        {
            return new ResourceId(Normalize(reference, isRooted));
        }

        var baseDir = GetDirectory(contextId.Value.Path);
        var combined = string.IsNullOrEmpty(baseDir) ? reference : Path.Combine(baseDir, reference);
        return new ResourceId(Normalize(combined, isRooted: false));
    }

    private static bool IsAbsoluteUri(string reference)
    {
        return Uri.TryCreate(reference, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Scheme);
    }

    private static string GetDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var normalized = NormalizeSeparators(path);
        var directory = Path.GetDirectoryName(normalized);
        return string.IsNullOrEmpty(directory) ? string.Empty : directory;
    }

    private static string Normalize(string path, bool isRooted)
    {
        // We normalize using Path.GetFullPath against a deterministic anchor to avoid
        // depending on the process current directory.
        var anchor = CreateNormalizationAnchor();
        var anchorFull = Path.GetFullPath(anchor);

        // Make the input relative to the anchor so rooted paths don't become drive-qualified.
        var osPath = NormalizeSeparators(path);
        var trimmed = osPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(trimmed, anchorFull);

        // Clamp attempts to escape above the anchor (extra "..") to preserve prior behavior.
        if (!IsWithinAnchor(anchorFull, full))
        {
            full = anchorFull;
        }

        var relative = Path.GetRelativePath(anchorFull, full);
        if (string.Equals(relative, ".", StringComparison.Ordinal))
        {
            relative = string.Empty;
        }

        var result = ToResourceIdPath(relative);
        return isRooted ? (string.IsNullOrEmpty(result) ? "/" : $"/{result}") : result;
    }

    private static string NormalizeSeparators(string path)
    {
        Debug.Assert(path is not null);
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string ToResourceIdPath(string path)
    {
        Debug.Assert(path is not null);
        return path.Replace('\\', '/');
    }

    private static string CreateNormalizationAnchor()
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory);
        if (string.IsNullOrEmpty(root))
        {
            root = Path.DirectorySeparatorChar.ToString();
        }

        return Path.Combine(root, "__tinyast_preprocessor__");
    }

    private static bool IsWithinAnchor(string anchorFull, string fullPath)
    {
        if (OperatingSystem.IsWindows())
        {
            if (string.Equals(anchorFull, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return fullPath.StartsWith(anchorFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(anchorFull, fullPath, StringComparison.Ordinal))
        {
            return true;
        }

        return fullPath.StartsWith(anchorFull + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
