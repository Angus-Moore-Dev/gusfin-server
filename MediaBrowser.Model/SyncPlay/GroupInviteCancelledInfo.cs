using System;

namespace MediaBrowser.Model.SyncPlay;

/// <summary>
/// Class GroupInviteCancelledInfo. Gusfin extension: tells a user that a delivered invite is no longer actionable.
/// </summary>
public class GroupInviteCancelledInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupInviteCancelledInfo"/> class.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="reason">The reason the invite was cancelled.</param>
    public GroupInviteCancelledInfo(Guid groupId, GroupInviteCancelReason reason)
    {
        GroupId = groupId;
        Reason = reason;
    }

    /// <summary>
    /// Gets the group identifier.
    /// </summary>
    /// <value>The group identifier.</value>
    public Guid GroupId { get; }

    /// <summary>
    /// Gets the reason the invite was cancelled.
    /// </summary>
    /// <value>The reason the invite was cancelled.</value>
    public GroupInviteCancelReason Reason { get; }
}
