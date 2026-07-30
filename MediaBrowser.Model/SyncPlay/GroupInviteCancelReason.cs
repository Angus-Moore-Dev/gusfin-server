namespace MediaBrowser.Model.SyncPlay;

/// <summary>
/// Enum GroupInviteCancelReason. Gusfin extension: why a previously delivered invite is no longer actionable.
/// </summary>
public enum GroupInviteCancelReason
{
    /// <summary>
    /// The invite was accepted from another of the user's sessions.
    /// </summary>
    AcceptedElsewhere = 0,

    /// <summary>
    /// The invite was declined from another of the user's sessions.
    /// </summary>
    DeclinedElsewhere = 1,

    /// <summary>
    /// The group no longer exists.
    /// </summary>
    GroupClosed = 2,

    /// <summary>
    /// The invite was revoked by a group member.
    /// </summary>
    Revoked = 3,

    /// <summary>
    /// The invite expired.
    /// </summary>
    Expired = 4
}
