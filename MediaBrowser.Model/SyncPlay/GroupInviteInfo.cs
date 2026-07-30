using System;

namespace MediaBrowser.Model.SyncPlay;

/// <summary>
/// Class GroupInviteInfo. Gusfin extension: an invite to a SyncPlay group, addressed to a user.
/// </summary>
public class GroupInviteInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupInviteInfo"/> class.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="groupName">The group name.</param>
    /// <param name="invitedByUserId">The identifier of the inviting user.</param>
    /// <param name="invitedByUserName">The name of the inviting user.</param>
    /// <param name="expiresAt">The UTC time at which the invite expires.</param>
    /// <param name="participantCount">The number of users currently in the group.</param>
    public GroupInviteInfo(Guid groupId, string groupName, Guid invitedByUserId, string invitedByUserName, DateTime expiresAt, int participantCount)
    {
        GroupId = groupId;
        GroupName = groupName;
        InvitedByUserId = invitedByUserId;
        InvitedByUserName = invitedByUserName;
        ExpiresAt = expiresAt;
        ParticipantCount = participantCount;
    }

    /// <summary>
    /// Gets the group identifier.
    /// </summary>
    /// <value>The group identifier.</value>
    public Guid GroupId { get; }

    /// <summary>
    /// Gets the group name.
    /// </summary>
    /// <value>The group name.</value>
    public string GroupName { get; }

    /// <summary>
    /// Gets the identifier of the inviting user.
    /// </summary>
    /// <value>The identifier of the inviting user.</value>
    public Guid InvitedByUserId { get; }

    /// <summary>
    /// Gets the name of the inviting user.
    /// </summary>
    /// <value>The name of the inviting user.</value>
    public string InvitedByUserName { get; }

    /// <summary>
    /// Gets the UTC time at which the invite expires.
    /// </summary>
    /// <value>The UTC time at which the invite expires.</value>
    public DateTime ExpiresAt { get; }

    /// <summary>
    /// Gets the number of users currently in the group.
    /// </summary>
    /// <value>The number of users currently in the group.</value>
    public int ParticipantCount { get; }
}
