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
    /// <para>
    /// Shapes, in order: a quoted full path (it may itself contain quotes, as in <c>O'Brien</c>, so it ends only at a
    /// quote followed by punctuation, a space or the line end); a quoted text that holds a separator; a quoted file name
    /// with an extension (it can name the user's file even without a folder); a bare Windows drive or network path (it
    /// may contain spaces and ends at a quote, <c>:</c> or "in"/"at"); a bare absolute Unix path (it ends at a space).
    /// </para>
    /// <para>
    /// The Unix shape does not start after <c>:</c>, <c>/</c> or a letter, so a web address such as
    /// <c>https://aka.ms/x</c> or a phrase like <c>and/or</c> stays readable (review of Stage 9.0).
    /// </para>
    /// </remarks>
    internal static string RemovePaths(string text)
    {
        try
        {
            string result = QuotedFullPath().Replace(text, Quoted);
            result = QuotedPath().Replace(result, Quoted);
            result = QuotedFileName().Replace(result, Quoted);
            result = BareWindowsPath().Replace(result, Bare);
            return BareUnixPath().Replace(result, Bare);
        }
        // Stryker disable once Block : reached only when a regex times out, which no test can provoke reliably
        catch (RegexMatchTimeoutException)
        {
            // Fail closed: text that cannot be scanned in time is not written at all.
            // Stryker disable once String : reached only when a regex times out, which no test can provoke reliably
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

    /// <summary>Replaces a quoted match by the same quotes around <see cref="PathPlaceholder"/> and the extension.</summary>
    /// <param name="match">A match with groups <c>q</c> (the quote) and <c>ext</c>.</param>
    /// <returns>The replacement text.</returns>
    private static string Quoted(Match match)
    {
        string quote = match.Groups["q"].Value;
        return quote + PathPlaceholder + match.Groups["ext"].Value + quote;
    }

    /// <summary>Replaces a bare match by <see cref="PathPlaceholder"/> and the extension.</summary>
    /// <param name="match">A match with group <c>ext</c>.</param>
    /// <returns>The replacement text.</returns>
    private static string Bare(Match match)
    {
        return PathPlaceholder + match.Groups["ext"].Value;
    }

    /// <summary>A quoted path from a drive, network or Unix root to a closing quote followed by punctuation, space or the end.</summary>
    [GeneratedRegex(@"(?<q>['""])(?:[A-Za-z]:[\\/]|\\\\|/)[^\r\n]*?(?<ext>\.[A-Za-z0-9]{1,8})?\k<q>(?=[.,;:)\s]|$)", RegexOptions.CultureInvariant | RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuotedFullPath();

    /// <summary>A quoted text with a <c>\</c> or <c>/</c> in it; group <c>ext</c> is a short extension at its end.</summary>
    [GeneratedRegex(@"(?<q>['""])[^'""\r\n]*[\\/][^'""\r\n]*?(?<ext>\.[A-Za-z0-9]{1,8})?\k<q>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuotedPath();

    /// <summary>A quoted file name without a folder that ends in an extension, such as <c>'TaxReturn2025.pdf'</c>.</summary>
    [GeneratedRegex(@"(?<q>['""])[^'""\r\n\\/]*?(?<ext>\.[A-Za-z0-9]{1,8})\k<q>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuotedFileName();

    /// <summary>A bare Windows drive or network path; it may contain spaces and ends at a quote, <c>:</c>, "in"/"at" or the line end.</summary>
    [GeneratedRegex(@"(?:\b[A-Za-z]:[\\/]|\\\\[^\\\s]+\\)[^'""\r\n:<>]*?(?<ext>\.[A-Za-z0-9]{1,8})?(?=['""\r\n:]|\s+(?:in|at)\s|$)", RegexOptions.CultureInvariant | RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BareWindowsPath();

    /// <summary>A bare absolute Unix path, not after <c>:</c>, <c>/</c> or a letter; it ends at a space, quote or <c>:</c>.</summary>
    [GeneratedRegex(@"(?<![:/\w.<>])/(?=[^\s/])[^'""\s:<>]*?(?<ext>\.[A-Za-z0-9]{1,8})?(?=['""\s:]|$)", RegexOptions.CultureInvariant | RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BareUnixPath();
}
