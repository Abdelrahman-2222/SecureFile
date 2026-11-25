using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;
using SecureFile.Presentation.Dialogs;
using SecureFile.Presentation.Errors;

namespace SecureFile.Presentation.Tests;

/// <summary>Returns prepared answers instead of showing dialogs, and remembers what it was asked.</summary>
internal sealed class FakeFileDialogService : IFileDialogService
{
    public string? OpenResult { get; set; }

    public string? SaveResult { get; set; }

    public string? SuggestedSavePath { get; private set; }

    public Task<string?> ShowOpenFileDialogAsync()
    {
        return Task.FromResult(OpenResult);
    }

    /// <summary>When set, the Save dialog stays "open" until this task completes (a non-modal dialog, as on Linux).</summary>
    public Task<string?>? PendingSave { get; set; }

    public Task<string?> ShowSaveFileDialogAsync(string suggestedFilePath)
    {
        SuggestedSavePath = suggestedFilePath;
        return PendingSave ?? Task.FromResult(SaveResult);
    }
}

/// <summary>Records each call; finishes at once, throws a prepared exception, or waits until the test releases it.</summary>
internal sealed class FakeFileEncryptionService : IFileEncryptionService
{
    public List<(string Operation, string Input, string Output, string Password, bool Overwrite)> Calls { get; } = [];

    public Exception? ExceptionToThrow { get; set; }

    /// <summary>When set, the operation stays running until this task completes.</summary>
    public Task? WaitFor { get; set; }

    public Task EncryptAsync(string inputFilePath, string outputFilePath, string password, bool overwriteExisting, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        return RunAsync("Encrypt", inputFilePath, outputFilePath, password, overwriteExisting, cancellationToken);
    }

    public Task DecryptAsync(string inputFilePath, string outputFilePath, string password, bool overwriteExisting, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        return RunAsync("Decrypt", inputFilePath, outputFilePath, password, overwriteExisting, cancellationToken);
    }

    private async Task RunAsync(string operation, string input, string output, string password, bool overwrite, CancellationToken cancellationToken)
    {
        Calls.Add((operation, input, output, password, overwrite));
        if (WaitFor is not null)
        {
            await WaitFor.WaitAsync(cancellationToken);
        }

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }
    }
}

/// <summary>Remembers the path it was asked to show; returns a prepared answer.</summary>
internal sealed class FakeFolderOpener : IFolderOpener
{
    public bool Result { get; set; } = true;

    public string? OpenedPath { get; private set; }

    public Task<bool> OpenContainingFolderAsync(string filePath)
    {
        OpenedPath = filePath;
        return Task.FromResult(Result);
    }
}

/// <summary>Collects the exceptions it is given instead of writing a log file.</summary>
internal sealed class FakeErrorLog : IErrorLog
{
    public List<Exception> Exceptions { get; } = [];

    public void Write(Exception exception)
    {
        Exceptions.Add(exception);
    }

    public void WriteExpected(Exception exception)
    {
        Exceptions.Add(exception);
        ExpectedCount++;
    }

    /// <summary>How many of <see cref="Exceptions"/> were logged as expected failures (no stack trace).</summary>
    public int ExpectedCount { get; private set; }
}

/// <summary>Returns details made from the path, or throws a prepared exception, like the real reader's contract.</summary>
internal sealed class FakeFileDetailsReader : IFileDetailsReader
{
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>When set, decides whether the file counts as encrypted, whatever its name.</summary>
    public bool? IsEncryptedOverride { get; set; }

    public FileDetails Read(string filePath)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        // By default the fake decides "encrypted" by name; a test sets the override to model content that differs from the
        // name (a renamed encrypted file, or a damaged ".enc"). The real reader's content check has its own tests.
        bool encrypted = IsEncryptedOverride ?? filePath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase);
        return new FileDetails(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), 1536, DateTime.UnixEpoch, encrypted);
    }
}

/// <summary>A clock that moves only when the test says so, for the progress throttle (backlog C18).</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        return _ticks;
    }

    public void Advance(TimeSpan duration)
    {
        _ticks += duration.Ticks;
    }
}
