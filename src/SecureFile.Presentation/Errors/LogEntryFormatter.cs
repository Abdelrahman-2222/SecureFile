using System.Text;
using System.Text.RegularExpressions;

namespace SecureFile.Presentation.Errors;

/// <summary>
/// Turns an exception into the text of one log entry, without full paths, and with a stack trace only for crashes
/// (review item 29).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why no paths:</b> a file-system message such as <c>Could not find file 'C:\Users\Sara\TaxReturn2025.pdf'</c>
/// puts the user name and a telling file name into a plaintext file. The log is for "what went wrong", so every path
/// is replaced by <c>&lt;path&gt;</c>; the extension is kept because it often explains the failure.
/// </para>
/// <para>
/// <b>Why no stack trace for an expected failure:</b> a wrong password or a tampered file is the user's situation,
/// not a bug; its type and message say everything, and a stack trace per wrong password only fills the log. A crash
/// keeps the full trace (with paths removed) because that is what a developer needs.
/// </para>
/// </remarks>
internal static partial class LogEntryFormatter
{
    /// <summary>What every removed path is replaced by.</summary>
    internal const string PathPlaceholder = "<path>";

    /// <summary>How many inner exceptions are written at most, so a cycle or a deep chain cannot grow an entry without bound.</summary>
    private const int MaximumInnerExceptions = 5;

    /// <summary>Builds the text of one entry.</summary>
    /// <param name="exception">The error to describe.</param>
    /// <param name="includeStackTrace">Whether stack traces are written (crashes only).</param>
    /// <returns>The entry: type and message of the exception and its inner exceptions, paths removed.</returns>
    internal static string Format(Exception exception, bool includeStackTrace)
    {
        var builder = new StringBuilder();
        Exception? current = exception;
        for (int depth = 0; current is not null && depth <= MaximumInnerExceptions; depth++)
        {
            AppendOne(builder, current, depth, includeStackTrace);
            current = current.InnerException;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Replaces every file or folder path in <paramref name="text"/> by <see cref="PathPlaceholder"/>.</summary>
    /// <param name="text">A message or stack trace.</param>
    /// <returns>The text without paths; a path's extension (such as <c>.pdf</c>) stays.</returns>
    /// <remarks>
    /// Three shapes are recognised: a quoted text that holds a separator (how .NET quotes paths in messages), a Windows
    /// drive or network path, and an absolute Unix path. Each stops at a quote, a line end or <c>:line</c>.
    /// </remarks>
    internal static string RemovePaths(string text)
    {
        try
        {
            string withoutQuoted = QuotedPath().Replace(text, match => $"{match.Groups["q"].Value}{PathPlaceholder}{match.Groups["ext"].Value}{match.Groups["q"].Value}");
            return BarePath().Replace(withoutQuoted, match => PathPlaceholder + match.Groups["ext"].Value);
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed: text that cannot be scanned in time is not written at all.
            return "<text removed: it could not be checked for paths>";
        }
    }

    /// <summary>Appends one exception of the chain.</summary>
    /// <param name="builder">The entry being built.</param>
    /// <param name="exception">The exception to append.</param>
    /// <param name="depth">0 for the outer exception, then 1, 2, … for inner ones.</param>
    /// <param name="includeStackTrace">Whether its stack trace is written.</param>
    private static void AppendOne(StringBuilder builder, Exception exception, int depth, bool includeStackTrace)
    {
        builder.Append(depth == 0 ? string.Empty : " ---> ")
            .Append(exception.GetType().FullName)
            .Append(": ")
            .AppendLine(RemovePaths(exception.Message));
        if (includeStackTrace && exception.StackTrace is { } stackTrace)
        {
            builder.AppendLine(RemovePaths(stackTrace));
        }
    }

    /// <summary>A quoted text with a <c>\</c> or <c>/</c> in it; group <c>ext</c> is a short extension at its end.</summary>
    [GeneratedRegex(@"(?<q>['""])[^'""\r\n]*[\\/][^'""\r\n]*?(?<ext>\.[A-Za-z0-9]{1,8})?\k<q>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuotedPath();

    /// <summary>A bare Windows drive, network or absolute Unix path, up to a quote, a line end, <c>:</c> or the end.</summary>
    [GeneratedRegex(@"(?:\b[A-Za-z]:[\\/]|\\\\[^\\\s]+\\|(?<![\w.<>])/(?=[^\s/]))[^'""\r\n:<>]*?(?<ext>\.[A-Za-z0-9]{1,8})?(?=['""\r\n:]|\s+(?:in|at)\s|$)", RegexOptions.CultureInvariant | RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BarePath();
}
