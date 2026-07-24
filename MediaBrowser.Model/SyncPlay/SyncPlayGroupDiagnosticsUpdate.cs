using System;
using System.ComponentModel;

namespace MediaBrowser.Model.SyncPlay;

/// <inheritdoc />
public class SyncPlayGroupDiagnosticsUpdate : GroupUpdate<GroupDiagnosticsUpdate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPlayGroupDiagnosticsUpdate"/> class.
    /// </summary>
    /// <param name="groupId">The groupId.</param>
    /// <param name="data">The data.</param>
    public SyncPlayGroupDiagnosticsUpdate(Guid groupId, GroupDiagnosticsUpdate data) : base(groupId, data)
    {
    }

    /// <inheritdoc />
    [DefaultValue(GroupUpdateType.GroupDiagnostics)]
    public override GroupUpdateType Type => GroupUpdateType.GroupDiagnostics;
}
