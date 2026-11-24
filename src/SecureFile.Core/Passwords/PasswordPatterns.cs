namespace SecureFile.Core.Passwords;

/// <summary>Detects the three most common ways a long-looking password is still easy to guess: repeats, runs and well-known words.</summary>
/// <remarks>
/// <para>
/// Length and variety alone call <c>Password1234</c>, <c>aaaaaaaaaaaaaaaa</c> and <c>abcdefghijklmnop</c> "strong". An
/// attacker's first guesses are exactly these, so the evaluator caps them at "weak". This is a small built-in list and
/// three cheap rules, not a full dictionary or a guess-count estimator (a real one, such as zxcvbn, would be a bigger
/// dependency). It only ever lowers a rating and is advice, never a blocker.
/// </para>
/// <para>Complexity: O(n) time and space in the password length n.</para>
/// </remarks>
internal static class PasswordPatterns
{
    /// <summary>At most this many different characters (ignoring case) is "a repeat": <c>aaaaaaaa</c>, <c>abababab</c>, <c>11223344</c>.</summary>
    private const int MaxDistinctForRepeat = 4;

    /// <summary>A rising or falling run (<c>abcdefgh</c>, <c>87654321</c>) covering at least half of the password is "a sequence".</summary>
    private const double SequenceShare = 0.5;

    /// <summary>Very common passwords and password bases, compared ignoring case, with everything but letters removed.</summary>
    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passw", "pass", "qwerty", "qwertyuiop", "azerty", "azertyuiop", "asdfghjkl", "zxcvbnm", "letmein",
        "welcome", "admin", "administrator", "iloveyou", "monkey", "dragon", "football", "baseball", "basketball",
        "sunshine", "princess", "trustno", "secret", "hello", "freedom", "whatever", "shadow", "superman", "batman",
        "master", "login", "starwars", "computer", "internet", "michael", "jordan", "hunter", "soccer", "ranger",
        "harley", "hockey", "buster", "thomas", "robert", "charlie", "summer", "winter", "spring", "autumn",
        "changeme", "default", "test", "testing", "guest", "user", "root", "security", "matrix", "cheese",
        "pepper", "flower", "mustang", "killer", "jessica", "ashley", "jennifer", "hannah", "daniel", "andrew",
        "mypassword", "secretpassword", "abcdef", "abcdefgh", "abc",
    };

    /// <summary>Tells whether the password is a repeat, a run, or a well-known word (with digits or symbols added).</summary>
    /// <param name="password">The password as typed.</param>
    /// <returns><see langword="true"/> when it is easy to guess for one of these reasons.</returns>
    internal static bool IsPredictable(string password)
    {
        return HasFewDistinctCharacters(password) || IsMostlyASequence(password) || IsCommonWord(password);
    }

    /// <summary>Checks for very few different characters, ignoring case.</summary>
    /// <param name="password">The password.</param>
    /// <returns><see langword="true"/> for repeats such as <c>aaaaaaaa</c>.</returns>
    private static bool HasFewDistinctCharacters(string password)
    {
        return password.Select(char.ToUpperInvariant).Distinct().Count() <= MaxDistinctForRepeat;
    }

    /// <summary>Checks whether one rising or falling run of neighbouring characters makes up most of the password.</summary>
    /// <param name="password">The password.</param>
    /// <returns><see langword="true"/> for <c>abcdefgh</c>, <c>12345678</c>, <c>Abcdefghijkl1!</c>.</returns>
    private static bool IsMostlyASequence(string password)
    {
        return LongestRun(password.ToUpperInvariant()) >= password.Length * SequenceShare;
    }

    /// <summary>Finds the longest stretch where each character is the next (+1) or previous (-1) one of the character before it.</summary>
    /// <param name="text">The text, already in one case.</param>
    /// <returns>The length of the longest rising or falling run; at least 1 for a non-empty text.</returns>
    private static int LongestRun(string text)
    {
        int longest = 1;
        int run = 1;
        int direction = 0;
        for (int i = 1; i < text.Length; i++)
        {
            int step = text[i] - text[i - 1];
            bool continues = Math.Abs(step) == 1 && (direction == 0 || step == direction);
            run = continues ? run + 1 : 1;
            direction = continues ? step : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    /// <summary>Checks the letters of the password (lower case, digits and symbols removed) against the common list.</summary>
    /// <param name="password">The password.</param>
    /// <returns><see langword="true"/> for <c>Password1234</c> and <c>PASSWORD!!</c>; not for <c>P@ssword1</c> (the "@" is not a letter, so it reads "pssword").</returns>
    private static bool IsCommonWord(string password)
    {
        string letters = new(password.Where(char.IsLetter).ToArray());
        return CommonWords.Contains(letters);
    }
}
