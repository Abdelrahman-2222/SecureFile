using SecureFile.Core.Files;
using SecureFile.Presentation.Dialogs;

namespace SecureFile.Presentation.Tests;

/// <summary>Returns prepared answers instead of showing dialogs, and remembers what it was asked.</summary>
internal sealed class FakeFileDialogService : IFileDialogService
{
    public string? OpenResult { get; set; }

    public string? SaveResult { get; set; }

    public string? SuggestedSavePath { get; private set; }

    public string? ShowOpenFileDialog()
    {
        return OpenResult;
    }

    public string? ShowSaveFileDialog(string suggestedFilePath)
    {
        SuggestedSavePath = suggestedFilePath;
        return SaveResult;
    }
}

/// <summary>Returns details made from the path, or throws a prepared exception, like the real reader's contract.</summary>
internal sealed class FakeFileDetailsReader : IFileDetailsReader
{
    public Exception? ExceptionToThrow { get; set; }

    public FileDetails Read(string filePath)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return new FileDetails(filePath, Path.GetFileName(filePath), Path.GetExtension(filePath), 1536, DateTime.UnixEpoch);
    }
}
