using System;
using System.ComponentModel;

namespace MediaBrowser.Model.SyncPlay;

/// <inheritdoc />
public class SyncPlayGroupInviteDeclinedUpdate : GroupUpdate<string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPlayGroupInviteDeclinedUpdate"/> class.
    /// </summary>
    /// <param name="groupId">The groupId.</param>
    /// <param name="data">The data.</param>
    public SyncPlayGroupInviteDeclinedUpdate(Guid groupId, string data) : base(groupId, data)
    {
    }

    /// <inheritdoc />
    [DefaultValue(GroupUpdateType.GroupInviteDeclined)]
    public override GroupUpdateType Type => GroupUpdateType.GroupInviteDeclined;
}
