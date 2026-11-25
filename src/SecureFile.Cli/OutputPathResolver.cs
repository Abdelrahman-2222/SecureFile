using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;

namespace SecureFile.Cli;

/// <summary>Chooses the result file name when the user did not give one, and compares paths.</summary>
/// <remarks>The same naming as the Windows app: <c>report.pdf</c> becomes <c>report.pdf.enc</c> and back.</remarks>
internal static class OutputPathResolver
{
    /// <summary>The ending added to a decrypted file's name when the encrypted file does not end in <c>.enc</c>.</summary>
    private const string DecryptedEnding = ".decrypted";

    /// <summary>Gives the result path for a command.</summary>
    /// <param name="command">Encrypt or decrypt.</param>
    /// <param name="inputPath">The file the user chose.</param>
    /// <param name="chosenOutput">The path from <c>--output</c>, or <see langword="null"/>.</param>
    /// <returns>The path the result will be written to.</returns>
    public static string Resolve(CliCommand command, string inputPath, string? chosenOutput)
    {
        if (chosenOutput is not null)
        {
            return chosenOutput;
        }

        return command == CliCommand.Encrypt ? inputPath + EncryptedFileFormat.FileExtension : DecryptedName(inputPath);
    }

    /// <summary>Tells whether writing <paramref name="output"/> would replace <paramref name="input"/>.</summary>
    /// <param name="input">The chosen file.</param>
    /// <param name="output">Where the result would go.</param>
    /// <returns><see langword="true"/> when both lead to the same file.</returns>
    /// <remarks>
    /// Uses the engine's own check (links, case and Unicode aliases), so a path the engine would refuse is refused
    /// here first with a plain message instead of an unhandled <see cref="ArgumentException"/> (review CLI-1).
    /// </remarks>
    /// <exception cref="IOException">A path holds a link loop.</exception>
    public static bool IsSameFile(string input, string output)
    {
        return FileIdentity.AreSameFile(Path.GetFullPath(input), Path.GetFullPath(output));
    }

    /// <summary>Removes <c>.enc</c> from the name; a name without it gets <c>.decrypted</c>, so the suggestion never equals the input.</summary>
    /// <param name="inputPath">The encrypted file.</param>
    /// <returns>The suggested result path.</returns>
    private static string DecryptedName(string inputPath)
    {
        string name = Path.GetFileName(inputPath);
        bool hasEncryptedEnding = name.Length > EncryptedFileFormat.FileExtension.Length
            && name.EndsWith(EncryptedFileFormat.FileExtension, StringComparison.OrdinalIgnoreCase);

        return hasEncryptedEnding
            ? inputPath[..^EncryptedFileFormat.FileExtension.Length]
            : inputPath + DecryptedEnding;
    }
}
