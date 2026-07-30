using System;
using System.ComponentModel;

namespace MediaBrowser.Model.SyncPlay;

/// <inheritdoc />
public class SyncPlayGroupInviteRequiredUpdate : GroupUpdate<string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPlayGroupInviteRequiredUpdate"/> class.
    /// </summary>
    /// <param name="groupId">The groupId.</param>
    /// <param name="data">The data.</param>
    public SyncPlayGroupInviteRequiredUpdate(Guid groupId, string data) : base(groupId, data)
    {
    }

    /// <inheritdoc />
    [DefaultValue(GroupUpdateType.GroupInviteRequired)]
    public override GroupUpdateType Type => GroupUpdateType.GroupInviteRequired;
}
