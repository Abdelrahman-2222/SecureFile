using SecureFile.Core.Passwords;

namespace SecureFile.Core.Tests.Passwords;

/// <summary>Tests for <see cref="PasswordStrengthEvaluator"/> (plan Step 1.8: empty, short, long, Unicode).</summary>
public sealed class PasswordStrengthEvaluatorTests
{
    [Theory]
    [InlineData("", PasswordStrength.Empty)]
    [InlineData("a", PasswordStrength.TooShort)]
    [InlineData("Ab1!xyz", PasswordStrength.TooShort)]       // 7 characters, even though varied
    [InlineData("abcdefgh", PasswordStrength.Weak)]          // exactly the minimum, one kind
    [InlineData("abcdefgh1", PasswordStrength.Weak)]         // two kinds, still short
    [InlineData("Abcdefg1!", PasswordStrength.Fair)]         // 9 characters but 4 kinds: one point
    [InlineData("abcdefghijkl", PasswordStrength.Fair)]      // 12 characters, one kind: one point
    [InlineData("blue river coffee lamp", PasswordStrength.Strong)] // long passphrase with spaces: points for length 12, 16 and variety
    [InlineData("Abcdefghijkl1!", PasswordStrength.Strong)]  // 14 characters, 4 kinds: two points
    [InlineData("abcdefghijklmnop", PasswordStrength.Strong)] // 16 characters: length alone gives two points
    public void Evaluate_RatesByLengthAndVariety(string password, PasswordStrength expected)
    {
        Assert.Equal(expected, PasswordStrengthEvaluator.Evaluate(password));
    }

    [Fact]
    public void Evaluate_SpacesCountAsCharactersAndAreNeverTrimmed()
    {
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthEvaluator.Evaluate("        ")); // 8 spaces: long enough, one kind
        Assert.Equal(PasswordStrength.TooShort, PasswordStrengthEvaluator.Evaluate("  ab  ")); // not trimmed to 2, not 8
    }

    [Fact]
    public void Evaluate_UnicodePassword_CountsCharactersAndTreatsOtherScriptsAsAKind()
    {
        // Arabic letters have no case: they count as "other". Eight of them reach the minimum.
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthEvaluator.Evaluate("كلمةسرية"));
        Assert.Equal(PasswordStrength.Strong, PasswordStrengthEvaluator.Evaluate("كلمة سر طويلة جدا 123"));
    }

    [Fact]
    public void Evaluate_VeryLongPassword_IsStrongAndDoesNotThrow()
    {
        Assert.Equal(PasswordStrength.Strong, PasswordStrengthEvaluator.Evaluate(new string('x', 1024)));
    }

    [Fact]
    public void Evaluate_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PasswordStrengthEvaluator.Evaluate(null!));
    }
}
