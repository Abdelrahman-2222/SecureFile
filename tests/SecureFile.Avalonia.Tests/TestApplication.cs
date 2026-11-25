using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(SecureFile.Avalonia.Tests.TestApplication))]

namespace SecureFile.Avalonia.Tests;

/// <summary>Starts the real <see cref="App"/> (theme, colours) on Avalonia's headless platform: no screen needed.</summary>
/// <remarks>
/// The tests use <see cref="HeadlessUnitTestSession"/> directly instead of the Avalonia.Headless.XUnit attributes: that
/// package is built against xUnit v3 3.x and fails test discovery under the xUnit v3 4.x used here (MissingMethodException).
/// The session is framework-neutral: each test hands its body to <see cref="RunOnUiThreadAsync"/>.
/// </remarks>
internal static class TestApplication
{
    /// <summary>Called by the headless session once per test run.</summary>
    /// <returns>The configured builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    /// <summary>Runs a test body on the single Avalonia UI thread of this test run.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>A task that completes when the body has finished on the UI thread.</returns>
    public static Task RunOnUiThreadAsync(Func<Task> body)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly);
        return session.Dispatch(
            async () =>
            {
                await body();
                return true;
            },
            TestContext.Current.CancellationToken);
    }
}
