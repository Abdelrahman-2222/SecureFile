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
        EnterPassword(window, "ui-test-pass"); // not relying on the password box keeping its text
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

    /// <summary>Closes the app (killing it if a dialog keeps it open) and removes the temp folder without hiding a test failure.</summary>
    [Fact]
    public void AboutDialog_OpensWithF1_ShowsTheVersionAndHasAccessibleControls()
    {
        Assert.SkipUnless(IsEnabled(), "Set SECUREFILE_UI_TESTS=1 to run the UI tests.");
        Window window = StartApp();

        window.SetForeground();
        Keyboard.Press(VirtualKeyShort.F1);

        Window[] dialogs = Retry.WhileEmpty(() => window.ModalWindows, Timeout, TimeSpan.FromMilliseconds(200)).Result ?? [];
        Assert.True(dialogs.Length > 0, "The About dialog did not open.");
        AutomationElement version = Find(dialogs[0], "AboutVersion");
        Assert.StartsWith("Version ", version.Name, StringComparison.Ordinal);
        AutomationElement[] buttons = dialogs[0].FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        Assert.All(buttons, button => Assert.False(string.IsNullOrWhiteSpace(button.Name)));
        Keyboard.Press(VirtualKeyShort.ESCAPE);
    }

    /// <summary>Closes the app (killing it if a dialog keeps it open) and removes the temp folder without hiding a test failure.</summary>
    public void Dispose()
    {
        if (_app is not null)
        {
            if (!_app.Close())
            {
                _app.Kill();
            }

            _app.Dispose();
        }

        _automation?.Dispose();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A file may still be held for a moment; a leftover temp folder must not turn into a test failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: cleanup is best effort.
        }
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

        string exe = Path.Combine(root, "src", "SecureFile.Wpf", "bin", configuration, "net10.0-windows", "SecureFile.exe");
        return File.Exists(exe) ? exe : throw new FileNotFoundException($"SecureFile.exe was not built at '{exe}'. Build the solution in the same configuration first.");
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

    /// <summary>
    /// Waits for the Open/Save dialog, brings it to the front, and only then types the path and Enter. If the dialog
    /// never appears, the test fails at once instead of typing into the main window (where Enter and Esc have meaning).
    /// </summary>
    private static void TypeInDialog(Window window, string path)
    {
        Window[] dialogs = Retry.WhileEmpty(() => window.ModalWindows, Timeout, TimeSpan.FromMilliseconds(200)).Result ?? [];
        Assert.True(dialogs.Length > 0, "The file dialog did not open within the time limit.");
        Window dialog = dialogs[0];
        dialog.SetForeground();
        AutomationElement? fileNameBox = Retry.WhileNull(
            () => dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)),
            Timeout,
            TimeSpan.FromMilliseconds(200)).Result;
        Assert.NotNull(fileNameBox); // the dialog is ready for keys once its edit controls exist; it focuses the file name itself
        Keyboard.Type(path);
        Keyboard.Press(VirtualKeyShort.ENTER);
    }

    /// <summary>Waits until the status text starts with the expected words; the element is looked up again each time.</summary>
    private static void WaitForStatus(Window window, string expectedStart)
    {
        string last = string.Empty;
        RetryResult<bool> result = Retry.WhileFalse(
            () =>
            {
                last = Find(window, "StatusMessage").Name;
                return last.StartsWith(expectedStart, StringComparison.Ordinal);
            },
            Timeout,
            TimeSpan.FromMilliseconds(200),
            ignoreException: true);
        Assert.True(result.Result, $"Expected the status to start with \"{expectedStart}\" but it was \"{last}\".");
    }
}
