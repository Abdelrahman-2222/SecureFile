namespace SecureFile.Presentation.Main;

/// <summary>Keyboard rules shared by both windows (review items 11 and 30).</summary>
/// <remarks>A second file for the same class, only to keep each file small.</remarks>
public sealed partial class MainViewModel
{
    /// <summary>
    /// Gets a value indicating whether Enter in the first password box should move the focus to the confirmation box
    /// instead of starting the operation.
    /// </summary>
    /// <remarks>
    /// While a new password is chosen, Enter in the first box used to do nothing until the second box was filled, so a
    /// keyboard user was stuck. Now Enter goes on to the confirmation; once both match, it starts encryption as before.
    /// When decrypting there is no confirmation, so Enter always starts the operation.
    /// </remarks>
    public bool EnterMovesToConfirm => IsEncrypting && !RunPrimaryCommand.CanExecute(null);
}
