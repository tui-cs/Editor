namespace Ted;

public sealed partial class TedApp
{
    private void ShowFindReplaceDialog (bool selectReplaceTab)
    {
        if (App is null)
        {
            throw new InvalidOperationException ("Cannot show find/replace when Application is not running.");
        }

        using FindReplaceDialog dialog = new (Editor, selectReplaceTab);
        App.Run (dialog);
    }
}
