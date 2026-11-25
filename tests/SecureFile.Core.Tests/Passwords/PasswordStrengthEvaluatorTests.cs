using SecureFile.Core.Passwords;

namespace SecureFile.Core.Tests.Passwords;

/// <summary>Tests for <see cref="PasswordStrengthEvaluator"/> (plan Step 1.8: empty, short, long, Unicode) and its pattern checks.</summary>
public sealed class PasswordStrengthEvaluatorTests
{
    [Theory]
    [InlineData("", PasswordStrength.Empty)]
    [InlineData("a", PasswordStrength.TooShort)]
    [InlineData("Ab1!xyz", PasswordStrength.TooShort)]       // 7 characters, even though varied
    [InlineData("abcdefgh", PasswordStrength.Weak)]          // exactly the minimum
    [InlineData("hjkmnpqr", PasswordStrength.Weak)]          // minimum length, one kind, no pattern
    [InlineData("hjkmnpqr1", PasswordStrength.Weak)]         // two kinds, still short
    [InlineData("Hjkmnp7!x", PasswordStrength.Fair)]         // 9 characters but 4 kinds: one point
    [InlineData("hjkmnpqrstvw", PasswordStrength.Fair)]      // 12 characters, one kind: one point
    [InlineData("blue river coffee lamp", PasswordStrength.Strong)] // long passphrase with spaces
    [InlineData("Hjkmnpqrstvw7!", PasswordStrength.Strong)]  // 14 characters, 4 kinds: two points
    [InlineData("hjkmnpqrstvwxzbd", PasswordStrength.Strong)] // 16 characters: length alone gives two points
    [InlineData("hamada67hjbmnbcg", PasswordStrength.Strong)] // a typical typed password, no pattern
    public void Evaluate_RatesByLengthAndVariety(string password, PasswordStrength expected)
    {
        Assert.Equal(expected, PasswordStrengthEvaluator.Evaluate(password));
    }

    // Mutation tests (Step 5.3): exactly three kinds count as variety, each kind is told apart from the others, and a
    // kind that appears twice is still counted once.
    [Theory]
    [InlineData("aB3kqzvm")]    // lower (6, an even count), upper, digit: exactly three kinds
    [InlineData("kq!zvm3x")]    // lower, symbol, digit
    [InlineData("kqZ!vmxy")]    // lower, upper, symbol
    public void Evaluate_ExactlyThreeKindsAndShort_IsFair(string password)
    {
        Assert.Equal(PasswordStrength.Fair, PasswordStrengthEvaluator.Evaluate(password));
    }

    [Fact]
    public void Evaluate_RunCoversExactlyHalf_IsPredictable()
    {
        // "abcde" is 5 of 10 characters after a step that is not a run: half is already predictable.
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthEvaluator.Evaluate("kqabcdeQ7z"));
    }

    [Fact]
    public void Evaluate_ZigZagOverFiveLetters_IsNotARun()
    {
        // Up four, down four, up four, down three: every step is +/-1, but the direction changes, so no run is long.
        Assert.Equal(PasswordStrength.Strong, PasswordStrengthEvaluator.Evaluate("abcdedcbabcdedcb"));
    }

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaa")]    // repeat
    [InlineData("abababababababab")]    // two characters repeated
    [InlineData("1122334411223344")]    // four characters repeated
    [InlineData("abcdefghijklmnop")]    // alphabet run
    [InlineData("ABCDEFGHIJKL")]        // upper case run
    [InlineData("1234567890123456")]    // digit run (the step from 9 to 0 breaks it, the longer part still covers over half)
    [InlineData("98765432109876")]      // falling run
    [InlineData("Abcdefghijkl1!")]      // run plus decoration
    [InlineData("Password1234")]        // well-known word plus digits
    [InlineData("PASSWORD!!!!")]        // same, other case and symbols
    [InlineData("qwertyuiop123")]       // keyboard row
    [InlineData("MyPassword2026")]      // common phrase with digits
    [InlineData("P@ssw0rd2024")]        // look-alikes plus a year: was rated Strong (review CORE-2)
    [InlineData("W3lc0me2024!")]
    public void Evaluate_PredictablePasswords_AreWeakHoweverLongTheyLook(string password)
    {
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthEvaluator.Evaluate(password));
    }

    [Theory]
    [InlineData("Passw0rd-Tx9q")]       // the look-alike reading gives "passwordtxq": other letters were added, so it is not the common word
    [InlineData("correct horse battery staple")]
    [InlineData("blue river coffee lamp")]
    public void Evaluate_ReasonablePassphrases_AreNotPunished(string password)
    {
        Assert.NotEqual(PasswordStrength.Weak, PasswordStrengthEvaluator.Evaluate(password));
    }

    [Fact]
    public void Evaluate_SpacesCountAsCharactersAndAreNeverTrimmed()
    {
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthEvaluator.Evaluate("        ")); // 8 spaces: long enough, but a repeat
        Assert.Equal(PasswordStrength.TooShort, PasswordStrengthEvaluator.Evaluate("  ab  ")); // not trimmed to 2, not 8
    }

    [Fact]
    public void Evaluate_UnicodePassword_CountsCharactersAndTreatsOtherScriptsAsAKind()
    {
        // Arabic letters have no case: they count as "other". Eight different ones reach the minimum.
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthEvaluator.Evaluate("كلمةسرية"));
        Assert.Equal(PasswordStrength.Strong, PasswordStrengthEvaluator.Evaluate("كلمة سر طويلة جدا 123"));
    }

    [Fact]
    public void Evaluate_VeryLongRepeatedPassword_IsWeakAndDoesNotThrow()
    {
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthEvaluator.Evaluate(new string('x', 1024)));
    }

    [Fact]
    public void Evaluate_VeryLongVariedPassword_IsStrong()
    {
        string password = string.Concat(Enumerable.Range(0, 200).Select(i => "k" + (i * 7919 % 10007)));

        Assert.Equal(PasswordStrength.Strong, PasswordStrengthEvaluator.Evaluate(password));
    }

    [Fact]
    public void Evaluate_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PasswordStrengthEvaluator.Evaluate(null!));
    }
}
