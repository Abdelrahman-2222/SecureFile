namespace SecureFile.Core.Files;

/// <summary>The chosen path is a folder, not a file (review item 12).</summary>
/// <remarks>
/// <para>
/// <b>Why a type of its own:</b> people drop a folder by mistake, and "the file can't be found" sends them looking for
/// a file that is right there. A distinct type lets the window say "this is a folder" instead.
/// </para>
/// <para>
/// <b>Why it derives from <see cref="FileNotFoundException"/>:</b> <see cref="IFileDetailsReader.Read"/> documented a
/// folder as <see cref="FileNotFoundException"/> before this type existed, so every caller that catches that (or
/// <see cref="IOException"/>) keeps working unchanged.
/// </para>
/// </remarks>
public sealed class PathIsFolderException : FileNotFoundException
{
    /// <summary>The message used by the parameterless constructor.</summary>
    internal const string DefaultMessage = "The path names a folder, not a file.";

    /// <summary>Creates the exception with the default message.</summary>
    public PathIsFolderException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message that explains the problem.</summary>
    /// <param name="message">A technical description for the log.</param>
    public PathIsFolderException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">A technical description for the log.</param>
    /// <param name="innerException">The original exception, kept so the log shows the full cause.</param>
    public PathIsFolderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a given folder.</summary>
    /// <param name="message">A technical description for the log.</param>
    /// <param name="folderPath">The folder that was chosen (kept in <see cref="FileNotFoundException.FileName"/>).</param>
    public PathIsFolderException(string message, string folderPath)
        : base(message, folderPath)
    {
    }
}
