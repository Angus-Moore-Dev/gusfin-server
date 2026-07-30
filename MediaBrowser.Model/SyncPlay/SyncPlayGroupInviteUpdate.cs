using System;
using System.ComponentModel;

namespace MediaBrowser.Model.SyncPlay;

/// <inheritdoc />
public class SyncPlayGroupInviteUpdate : GroupUpdate<GroupInviteInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPlayGroupInviteUpdate"/> class.
    /// </summary>
    /// <param name="groupId">The groupId.</param>
    /// <param name="data">The data.</param>
    public SyncPlayGroupInviteUpdate(Guid groupId, GroupInviteInfo data) : base(groupId, data)
    {
    }

    /// <inheritdoc />
    [DefaultValue(GroupUpdateType.GroupInvite)]
    public override GroupUpdateType Type => GroupUpdateType.GroupInvite;
}
