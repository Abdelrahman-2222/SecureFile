using SecureFile.Core.Passwords;

namespace SecureFile.Core.Tests.Passwords;

/// <summary>Mutation-driven tests (Step 5.3) for <see cref="PasswordPatterns"/>: every listed word really is recognised.</summary>
public sealed class PasswordPatternsTests
{
    // Fourteen digits with no rising or falling step and more than four different digits, so that ONLY the word list
    // can make "word + tail" predictable (not the repeat rule, not the run rule).
    private const string UnpredictableTail = "73915028461739";

    public static TheoryData<string> Words()
    {
        var data = new TheoryData<string>();
        foreach (string word in PasswordPatterns.CommonWords)
        {
            data.Add(word);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Words))]
    public void IsPredictable_EveryListedWordWithDigitsAdded_IsRecognised(string word)
    {
        Assert.True(PasswordPatterns.IsPredictable(word + UnpredictableTail));
    }

    // Backlog C3: look-alike digits and symbols are read as the letters they imitate. The tail has no character that
    // could be read as a letter and no pattern, so only the look-alike reading can make these predictable.
    [Theory]
    [InlineData("p@ssword")]    // @ = a
    [InlineData("pa$$word")]    // $ = s
    [InlineData("pas5word")]    // 5 = s
    [InlineData("passw0rd")]    // 0 = o
    [InlineData("l3tmein")]     // 3 = e
    [InlineData("1loveyou")]    // 1 = i
    [InlineData("mus7ang")]     // 7 = t
    [InlineData("P@ssw0rd1")]    // look-alikes plus a digit suffix (review CORE-2)
    [InlineData("P@ssw0rd2024")]
    [InlineData("W3lc0me2024!")]
    [InlineData("4dmin")]       // 4 = a
    public void IsPredictable_LookAlikeCharactersReadAsLetters_IsRecognised(string disguisedWord)
    {
        Assert.True(PasswordPatterns.IsPredictable(disguisedWord + "26892689268926"));
    }

    [Fact]
    public void IsPredictable_DisguisedWordWithOtherLettersAdded_IsNotRecognised()
    {
        Assert.False(PasswordPatterns.IsPredictable("P@ssw0rd-Tx9q"));
        Assert.False(PasswordPatterns.IsPredictable("Tr0ub4dor&3x"));
    }

    [Fact]
    public void IsPredictable_DigitsOnlyWithNoPattern_IsNotPredictable()
    {
        // Also guards against an empty word in the list: it would match every password that has no letters.
        Assert.False(PasswordPatterns.IsPredictable(UnpredictableTail));
    }

    [Fact]
    public void IsPredictable_ExactlyFourDifferentCharactersRepeated_IsPredictable()
    {
        Assert.True(PasswordPatterns.IsPredictable("aXb9aXb9aXb9"));
        Assert.False(PasswordPatterns.IsPredictable("aXb9cXb9aXd9"));  // five different characters
    }
}
