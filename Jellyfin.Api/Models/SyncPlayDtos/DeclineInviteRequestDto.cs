using System;

namespace Jellyfin.Api.Models.SyncPlayDtos;

/// <summary>
/// Class DeclineInviteRequestDto. Gusfin extension.
/// </summary>
public class DeclineInviteRequestDto
{
    /// <summary>
    /// Gets or sets the identifier of the group whose invite is being declined.
    /// </summary>
    /// <value>The group identifier.</value>
    public Guid GroupId { get; set; }
}
