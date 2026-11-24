namespace SecureFile.Presentation.Main;

/// <summary>
/// The states of the main window (PROJECT_PLAN Step 2.2). The buttons that can be used follow from the state.
/// </summary>
/// <remarks>
/// <code>
/// NoFile ──Browse──▶ Working ──details read──▶ FileSelected ──Encrypt / Decrypt──▶ Working ──▶ Succeeded
///                       │                           ▲                                  │
///                       └──────────error──────────▶ Failed ◀───────────────error───────┘
/// Succeeded / Failed / FileSelected ──Browse, Encrypt or Decrypt again──▶ Working
/// </code>
/// One enum instead of several <see langword="bool"/> flags, so impossible combinations (for example "working" and
/// "succeeded" at the same time) cannot be represented.
/// </remarks>
public enum MainViewState
{
    /// <summary>No file has been chosen yet; only Browse is available.</summary>
    NoFile,

    /// <summary>A file is chosen and its details are shown; Encrypt and Decrypt are available once a password is entered.</summary>
    FileSelected,

    /// <summary>An operation is running; every button is disabled until it ends.</summary>
    Working,

    /// <summary>The last operation finished and its result is in place.</summary>
    Succeeded,

    /// <summary>The last operation failed; the status message says what happened and what to do.</summary>
    Failed,
}
