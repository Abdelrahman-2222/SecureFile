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
