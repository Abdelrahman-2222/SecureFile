using System.Text;

namespace SecureFile.Core.Files;

/// <summary>
/// Decides whether an output path would replace the input file, looking through symbolic links, directory links and
/// case or Unicode-normalisation aliases (backlog C14).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a plain string comparison is not enough.</b> On Windows the input stays open while the output is written,
/// and Windows refuses to rename over an open file, so an alias that slips past the check only fails the operation.
/// On Linux and macOS the final rename replaces the directory entry even while the input is open: the encryption
/// finishes from the old data, and then the only copy of the plaintext is gone. A path such as
/// <c>~/alias/report.pdf</c>, where <c>alias</c> links to the input's folder, names the input under another string.
/// </para>
/// <para>
/// <b>Why resolve paths instead of comparing device and inode numbers.</b> .NET exposes no inode number, and a
/// P/Invoke into <c>stat</c> needs a struct layout per operating system and processor. Resolving every link along the
/// path gives the same answer for links; hard links need no check, because renaming onto a second name of the input
/// replaces only that name, and the input's own name keeps the data. Bind mounts are not detected (rare, admin-made).
/// </para>
/// <para>
/// <b>Why the comparison ignores case and normalisation everywhere.</b> macOS volumes are case-insensitive and
/// normalisation-insensitive by default, Windows volumes are case-insensitive; telling them apart would need another
/// probe. On a case-sensitive Linux volume this refuses <c>a.txt</c> → <c>A.TXT</c> although they are two files —
/// a false refusal the user fixes by choosing another name, never a lost file.
/// </para>
/// </remarks>
internal static class FileIdentity
{
    /// <summary>How many links are followed before the path counts as a loop (Linux uses 40 too).</summary>
    private const int MaximumLinkHops = 40;

    /// <summary>The separators a path may use on this system (Windows accepts both).</summary>
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>Returns whether writing <paramref name="outputFilePath"/> would replace <paramref name="inputFilePath"/>.</summary>
    /// <param name="inputFilePath">The full input path.</param>
    /// <param name="outputFilePath">The full output path.</param>
    /// <returns><see langword="true"/> when both paths reach the same file.</returns>
    /// <remarks>
    /// Every link in the input path is followed, the file name included, because the rename would replace the link's
    /// target. In the output path the file name itself is not followed: the rename replaces a link there, not its target.
    /// </remarks>
    /// <exception cref="IOException">A path holds a link loop (more than 40 links).</exception>
    internal static bool AreSameFile(string inputFilePath, string outputFilePath)
    {
        string input = ResolveLinks(inputFilePath, followLastPart: true);
        string output = ResolveLinks(outputFilePath, followLastPart: false);
        return string.Equals(
            input.Normalize(NormalizationForm.FormC),
            output.Normalize(NormalizationForm.FormC),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rebuilds <paramref name="fullPath"/> one part at a time, replacing every link by its target.</summary>
    /// <param name="fullPath">A full path.</param>
    /// <param name="followLastPart">Whether the last part (the file name) is followed when it is a link.</param>
    /// <returns>The path with no links left in it (as far as they exist).</returns>
    /// <remarks>
    /// A link's target may itself run through links (on macOS <c>/var</c> links to <c>/private/var</c>), so after a
    /// link the walk starts again from the target's root with the target's parts in front of the parts still left.
    /// </remarks>
    /// <exception cref="IOException">More than <see cref="MaximumLinkHops"/> links were followed.</exception>
    private static string ResolveLinks(string fullPath, bool followLastPart)
    {
        var remaining = new Queue<string>(SplitParts(fullPath));
        string current = Path.GetPathRoot(fullPath) ?? string.Empty;
        int hops = 0;
        while (remaining.Count > 0)
        {
            current = Step(current, remaining, followLastPart, ref hops);
        }

        return current;
    }

    /// <summary>Takes the next part from <paramref name="remaining"/>, following it when it is a link.</summary>
    /// <param name="current">The path built so far.</param>
    /// <param name="remaining">The parts still left; a followed link puts its target's parts in front.</param>
    /// <param name="followLastPart">Whether the last part is followed when it is a link.</param>
    /// <param name="hops">How many links were followed so far; raised by one when this part is a link.</param>
    /// <returns>The new path built so far.</returns>
    /// <exception cref="IOException">More than <see cref="MaximumLinkHops"/> links were followed.</exception>
    private static string Step(string current, Queue<string> remaining, bool followLastPart, ref int hops)
    {
        string candidate = Path.Combine(current, remaining.Dequeue());
        string? target = remaining.Count > 0 || followLastPart ? GetLinkTarget(candidate) : null;
        if (target is null)
        {
            return candidate;
        }

        if (++hops > MaximumLinkHops)
        {
            throw new IOException("The path contains too many links; it may hold a link loop.");
        }

        string[] rest = [.. SplitParts(target), .. remaining];
        remaining.Clear();
        foreach (string part in rest)
        {
            remaining.Enqueue(part);
        }

        return Path.GetPathRoot(target) ?? string.Empty;
    }

    /// <summary>Returns the parts of a full path after its root.</summary>
    /// <param name="fullPath">A full path.</param>
    /// <returns>The folder and file names, in order.</returns>
    private static string[] SplitParts(string fullPath)
    {
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        return fullPath[root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Returns the full target of <paramref name="path"/> when it is a link, else <see langword="null"/>.</summary>
    /// <param name="path">A full path that may or may not exist.</param>
    /// <returns>The link's direct target as a full path, or <see langword="null"/>.</returns>
    /// <remarks>
    /// <see cref="FileSystemInfo.LinkTarget"/> works for file and folder links alike and is <see langword="null"/> for
    /// a missing path or an ordinary entry. A relative target is relative to the folder that holds the link.
    /// </remarks>
    private static string? GetLinkTarget(string path)
    {
        string? target;
        try
        {
            target = new FileInfo(path).LinkTarget;
        }
        catch (IOException)
        {
            // On Unix readlink also fails for a missing folder or a file used as a folder: then this part cannot be a
            // link the rename would go through, and the write itself reports the real problem (Stage 9.0 review).
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // A folder the user may not enter: the same reasoning; the write fails with the access error.
            return null;
        }

        return target is null ? null : Path.GetFullPath(target, Path.GetDirectoryName(path) ?? path);
    }
}
