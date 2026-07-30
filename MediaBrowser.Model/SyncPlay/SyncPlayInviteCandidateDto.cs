using System;

namespace MediaBrowser.Model.SyncPlay;

/// <summary>
/// Class SyncPlayInviteCandidateDto. Gusfin extension: an online user that a group member may invite.
/// </summary>
public class SyncPlayInviteCandidateDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPlayInviteCandidateDto"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="isInvited">Whether the user already has a pending invite to the group.</param>
    public SyncPlayInviteCandidateDto(Guid userId, string userName, bool isInvited)
    {
        UserId = userId;
        UserName = userName;
        IsInvited = isInvited;
    }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    /// <value>The user identifier.</value>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the user name.
    /// </summary>
    /// <value>The user name.</value>
    public string UserName { get; }

    /// <summary>
    /// Gets a value indicating whether the user already has a pending invite to the group.
    /// </summary>
    /// <value><c>true</c> if the user already has a pending invite; <c>false</c> otherwise.</value>
    public bool IsInvited { get; }
}
