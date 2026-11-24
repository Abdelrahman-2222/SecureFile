namespace SecureFile.Core.Passwords;

/// <summary>How good a password is for protecting a file, as shown to the user (advice, except <see cref="TooShort"/>).</summary>
public enum PasswordStrength
{
    /// <summary>Nothing typed yet.</summary>
    Empty,

    /// <summary>Shorter than <see cref="PasswordStrengthEvaluator.MinimumLength"/>: not accepted for encryption.</summary>
    TooShort,

    /// <summary>Accepted, but short or made of one kind of character; easy to guess offline.</summary>
    Weak,

    /// <summary>Acceptable for most files.</summary>
    Fair,

    /// <summary>Long or varied enough to resist offline guessing well.</summary>
    Strong,
}
