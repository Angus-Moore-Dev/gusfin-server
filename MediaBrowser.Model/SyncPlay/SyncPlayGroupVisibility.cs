namespace MediaBrowser.Model.SyncPlay;

/// <summary>
/// Enum SyncPlayGroupVisibility. Gusfin extension.
/// </summary>
public enum SyncPlayGroupVisibility
{
    /// <summary>
    /// Group is listed to, and joinable by, every user with SyncPlay access.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Group is hidden from listings and joinable only by members and invitees.
    /// </summary>
    Private = 1
}
