using System.ComponentModel.DataAnnotations;
using MediaBrowser.Model.SyncPlay;

namespace Jellyfin.Api.Models.SyncPlayDtos;

/// <summary>
/// Class NewGroupRequestDto.
/// </summary>
public class NewGroupRequestDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewGroupRequestDto"/> class.
    /// </summary>
    public NewGroupRequestDto()
    {
        GroupName = string.Empty;
        Visibility = SyncPlayGroupVisibility.Public;
    }

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    /// <value>The name of the new group.</value>
    [StringLength(200, ErrorMessage = "Group name must not exceed 200 characters.")]
    public string GroupName { get; set; }

    /// <summary>
    /// Gets or sets the group visibility. Gusfin extension. Defaults to public.
    /// </summary>
    /// <value>The visibility of the new group.</value>
    public SyncPlayGroupVisibility Visibility { get; set; }
}
