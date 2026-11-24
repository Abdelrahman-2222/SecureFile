namespace SecureFile.Core.Passwords;

/// <summary>
/// Rates a password by length and character variety, so the window can advise the user while a password is chosen
/// for encryption (plan Step 1.8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why only length is enforced:</b> the single hard rule is <see cref="MinimumLength"/>. Forcing symbols, digits
/// and capitals (composition rules) is advised against by NIST SP 800-63B: people answer with <c>Password1!</c>, which
/// passes and is still weak. Length is what slows offline guessing, so it counts most; variety only adds to the score.
/// The rating is advice, never a blocker (apart from "too short"), and it is never applied when decrypting.
/// </para>
/// <para>
/// It is a quick rule of thumb, not a guess-count estimator: it cannot know that <c>aaaaaaaaaaaa</c> or a common
/// phrase is weak. Complexity: O(n) time and O(1) space in the password length n.
/// </para>
/// </remarks>
public static class PasswordStrengthEvaluator
{
    /// <summary>The shortest password accepted when encrypting.</summary>
    public const int MinimumLength = 8;

    private const int GoodLength = 12;
    private const int LongLength = 16;
    private const int VarietyForPoint = 3;

    /// <summary>Rates a password.</summary>
    /// <param name="password">The password as typed; spaces count as characters and are never trimmed.</param>
    /// <returns>The rating; <see cref="PasswordStrength.Empty"/> for an empty string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="password"/> is <see langword="null"/>.</exception>
    public static PasswordStrength Evaluate(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (password.Length == 0)
        {
            return PasswordStrength.Empty;
        }

        if (password.Length < MinimumLength)
        {
            return PasswordStrength.TooShort;
        }

        int points = (password.Length >= GoodLength ? 1 : 0)
            + (password.Length >= LongLength ? 1 : 0)
            + (CountCharacterKinds(password) >= VarietyForPoint ? 1 : 0);

        return points switch
        {
            0 => PasswordStrength.Weak,
            1 => PasswordStrength.Fair,
            _ => PasswordStrength.Strong,
        };
    }

    /// <summary>Counts how many of lower case, upper case, digits and "other" (symbols, spaces, other scripts) occur.</summary>
    /// <param name="password">The password.</param>
    /// <returns>A number from 0 to 4.</returns>
    private static int CountCharacterKinds(string password)
    {
        uint kinds = 0;
        foreach (char c in password)
        {
            kinds |= KindOf(c);
        }

        return System.Numerics.BitOperations.PopCount(kinds);
    }

    /// <summary>Gives a character the bit of its kind: 1 lower case, 2 upper case, 4 digit, 8 anything else.</summary>
    /// <param name="c">The character.</param>
    /// <returns>One of 1, 2, 4 or 8.</returns>
    private static uint KindOf(char c)
    {
        if (char.IsLower(c))
        {
            return 1;
        }

        if (char.IsUpper(c))
        {
            return 2;
        }

        return char.IsDigit(c) ? 4u : 8u;
    }
}
