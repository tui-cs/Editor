namespace Ted;

/// <summary>Choices available when ted asks how to handle unsaved changes.</summary>
public enum SaveChangesChoice
{
    /// <summary>Cancel the pending action and keep editing.</summary>
    Cancel,

    /// <summary>Discard unsaved changes and continue the pending action.</summary>
    Discard,

    /// <summary>Save unsaved changes before continuing the pending action.</summary>
    Save
}
