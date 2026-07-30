using System;
using System.ComponentModel;

namespace MediaBrowser.Model.SyncPlay;

/// <inheritdoc />
public class SyncPlayGroupInviteCancelledUpdate : GroupUpdate<GroupInviteCancelledInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPlayGroupInviteCancelledUpdate"/> class.
    /// </summary>
    /// <param name="groupId">The groupId.</param>
    /// <param name="data">The data.</param>
    public SyncPlayGroupInviteCancelledUpdate(Guid groupId, GroupInviteCancelledInfo data) : base(groupId, data)
    {
    }

    /// <inheritdoc />
    [DefaultValue(GroupUpdateType.GroupInviteCancelled)]
    public override GroupUpdateType Type => GroupUpdateType.GroupInviteCancelled;
}
