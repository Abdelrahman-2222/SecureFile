using System.Security.Cryptography;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SecureFile.Wpf.UiTests;

/// <summary>
/// End-to-end tests of the real app window (test catalog T-40 and the automatic part of T-41/T-42): they start
/// <c>SecureFile.exe</c> and use it like a person with a keyboard.
/// </summary>
/// <remarks>
/// They run only with <c>SECUREFILE_UI_TESTS=1</c> because they need an unlocked desktop and take over the keyboard
/// while they run. Do not touch the keyboard during the run.
/// </remarks>
public sealed class SecureFileAppTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SecureFile-ui-" + Guid.NewGuid().ToString("N"));
    private Application? _app;
    private UIA3Automation? _automation;

    public SecureFileAppTests()
    {
        Directory.CreateDirectory(_folder);
    }

    [Fact]
    public async Task EncryptThenDecrypt_ThroughTheWindow_RestoresTheOriginalBytes()
    {
        Assert.SkipUnless(IsEnabled(), "Set SECUREFILE_UI_TESTS=1 to run the UI tests.");
        string original = Path.Combine(_folder, "report.bin");
        string encrypted = original + ".enc";
        string restored = Path.Combine(_folder, "restored.bin");
        byte[] content = RandomNumberGenerator.GetBytes(50_000);
        await File.WriteAllBytesAsync(original, content, TestContext.Current.CancellationToken);
        Window window = StartApp();

        ChooseFile(window, original);
        EnterPassword(window, "ui-test-pass");
        RunWithSaveDialog(window, "EncryptButton", encrypted);
        WaitForStatus(window, "Encrypted copy saved");

        ChooseFile(window, encrypted);
        RunWithSaveDialog(window, "DecryptButton", restored);
        WaitForStatus(window, "Decrypted file saved");

        Assert.Equal(content, await File.ReadAllBytesAsync(restored, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Decrypt_WithWrongPassword_ShowsTheFriendlyMessageAndWritesNothing()
    {
        Assert.SkipUnless(IsEnabled(), "Set SECUREFILE_UI_TESTS=1 to run the UI tests.");
        string original = Path.Combine(_folder, "a.txt");
        string encrypted = original + ".enc";
        string output = Path.Combine(_folder, "out.txt");
        await File.WriteAllTextAsync(original, "secret text", TestContext.Current.CancellationToken);
        Window window = StartApp();
        ChooseFile(window, original);
        EnterPassword(window, "right-pass");
        RunWithSaveDialog(window, "EncryptButton", encrypted);
        WaitForStatus(window, "Encrypted copy saved");

        ChooseFile(window, encrypted);
        EnterPassword(window, "wrong-pass");
        RunWithSaveDialog(window, "DecryptButton", output);
        WaitForStatus(window, "Wrong password");

        Assert.False(File.Exists(output));
    }

    [Fact]
    public void EveryControl_HasAnAccessibleName()
    {
        Assert.SkipUnless(IsEnabled(), "Set SECUREFILE_UI_TESTS=1 to run the UI tests.");
        Window window = StartApp();

        AutomationElement[] controls = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)
            .Or(cf.ByControlType(ControlType.Edit))
            .Or(cf.ByControlType(ControlType.ProgressBar)));

        Assert.NotEmpty(controls);
        foreach (AutomationElement control in controls)
        {
            Assert.False(string.IsNullOrWhiteSpace(control.Name), $"A {control.ControlType} has no accessible name (id '{control.AutomationId}').");
        }
    }

    public void Dispose()
    {
        _app?.Close();
        _automation?.Dispose();
        _app?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private static bool IsEnabled()
    {
        return Environment.GetEnvironmentVariable("SECUREFILE_UI_TESTS") == "1";
    }

    /// <summary>Finds the app's exe next to the matching build configuration (Debug or Release).</summary>
    /// <returns>The full path of <c>SecureFile.exe</c>.</returns>
    private static string FindExe()
    {
        string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string configuration = new DirectoryInfo(baseDir).Parent!.Name; // .../bin/<Configuration>/net10.0-windows
        string root = baseDir;
        while (!File.Exists(Path.Combine(root, "SecureFile.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Repository root not found.");
        }

        return Path.Combine(root, "src", "SecureFile.Wpf", "bin", configuration, "net10.0-windows", "SecureFile.exe");
    }

    private Window StartApp()
    {
        var automation = new UIA3Automation();
        _automation = automation;
        var app = Application.Launch(FindExe());
        _app = app;
        Window window = app.GetMainWindow(automation, Timeout)
            ?? throw new InvalidOperationException("The main window did not appear.");
        window.SetForeground();
        return window;
    }

    private static AutomationElement Find(Window window, string automationId)
    {
        return Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), Timeout).Result
            ?? throw new InvalidOperationException($"Control '{automationId}' not found.");
    }

    /// <summary>Ctrl+O, type the path in the Open dialog, Enter, and wait until the file is shown.</summary>
    private static void ChooseFile(Window window, string path)
    {
        window.SetForeground();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);
        TypeInDialog(window, path);
        WaitForStatus(window, "Enter the password");
    }

    private static void EnterPassword(Window window, string password)
    {
        AutomationElement box = Find(window, "PasswordInput");
        box.Patterns.Value.Pattern.SetValue(password);
    }

    /// <summary>Invokes a button that opens the Save dialog and types the output path into it.</summary>
    private static void RunWithSaveDialog(Window window, string buttonId, string outputPath)
    {
        Find(window, buttonId).Patterns.Invoke.Pattern.Invoke();
        TypeInDialog(window, outputPath);
    }

    private static void TypeInDialog(Window window, string path)
    {
        Retry.WhileEmpty(() => window.ModalWindows, Timeout, TimeSpan.FromMilliseconds(200));
        Thread.Sleep(500); // the dialog needs a moment before it accepts keys
        Keyboard.Type(path);
        Keyboard.Press(VirtualKeyShort.ENTER);
    }

    private static void WaitForStatus(Window window, string expectedStart)
    {
        AutomationElement status = Find(window, "StatusMessage");
        RetryResult<bool> result = Retry.WhileFalse(
            () => status.Name.StartsWith(expectedStart, StringComparison.Ordinal),
            Timeout,
            TimeSpan.FromMilliseconds(200));
        Assert.True(result.Result, $"Expected the status to start with \"{expectedStart}\" but it was \"{status.Name}\".");
    }
}
