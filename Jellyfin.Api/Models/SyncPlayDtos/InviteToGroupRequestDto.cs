using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.SyncPlayDtos;

/// <summary>
/// Class InviteToGroupRequestDto. Gusfin extension.
/// </summary>
public class InviteToGroupRequestDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InviteToGroupRequestDto"/> class.
    /// </summary>
    public InviteToGroupRequestDto()
    {
        UserIds = Array.Empty<Guid>();
    }

    /// <summary>
    /// Gets or sets the identifiers of the users to invite.
    /// </summary>
    /// <value>The identifiers of the users to invite.</value>
    [Required]
    [MaxLength(20)]
    public IReadOnlyList<Guid> UserIds { get; set; }
}
