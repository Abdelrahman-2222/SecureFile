namespace SecureFile.Presentation.Errors;

/// <summary>
/// The texts of the two dialogs the desktop apps' safety nets show after an unexpected error (ADR-13 layer 3), shared
/// by WPF and Avalonia so both say the same thing.
/// </summary>
/// <remarks>
/// Both name the log file (review item 15): "details were saved to the log" was no help to someone who has to find it
/// to send it. The full path is shown on the user's own screen; the log itself never holds paths (review item 29).
/// </remarks>
public static class SafetyNetMessages
{
    /// <summary>The message after an unexpected error the app recovered from.</summary>
    /// <param name="logFilePath">Where the log is (<see cref="FileErrorLog.LogFilePath"/>).</param>
    /// <returns>The dialog text.</returns>
    public static string Recovered(string logFilePath)
    {
        return $"Something unexpected went wrong. Your files were not changed.\n\nDetails were saved to the log:\n{logFilePath}";
    }

    /// <summary>The message before the app closes because continuing would not be safe.</summary>
    /// <param name="logFilePath">Where the log is (<see cref="FileErrorLog.LogFilePath"/>).</param>
    /// <returns>The dialog text.</returns>
    public static string MustClose(string logFilePath)
    {
        return $"SecureFile hit a serious problem and must close. Your files were not changed.\n\nDetails were saved to the log:\n{logFilePath}";
    }
}
