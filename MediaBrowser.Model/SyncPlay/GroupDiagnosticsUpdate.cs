using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.SyncPlay;

/// <summary>
/// Class GroupDiagnosticsUpdate. Gusfin extension: snapshot of the playback diagnostics of a SyncPlay group.
/// </summary>
public class GroupDiagnosticsUpdate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupDiagnosticsUpdate"/> class.
    /// </summary>
    /// <param name="positionTicks">The estimated group position ticks.</param>
    /// <param name="when">When the snapshot was taken.</param>
    /// <param name="isPlaying">Whether the group is playing.</param>
    /// <param name="members">The per-member diagnostics.</param>
    public GroupDiagnosticsUpdate(long positionTicks, DateTime when, bool isPlaying, IReadOnlyList<MemberDiagnosticsInfo> members)
    {
        PositionTicks = positionTicks;
        When = when;
        IsPlaying = isPlaying;
        Members = members;
    }

    /// <summary>
    /// Gets the estimated group position ticks at the time of the snapshot.
    /// </summary>
    /// <value>The position ticks.</value>
    public long PositionTicks { get; }

    /// <summary>
    /// Gets when the snapshot was taken, in server UTC time.
    /// </summary>
    /// <value>The date of the snapshot.</value>
    public DateTime When { get; }

    /// <summary>
    /// Gets a value indicating whether the group state is playing.
    /// </summary>
    /// <value><c>true</c> if the group is playing; <c>false</c> otherwise.</value>
    public bool IsPlaying { get; }

    /// <summary>
    /// Gets the per-member diagnostics.
    /// </summary>
    /// <value>The members.</value>
    public IReadOnlyList<MemberDiagnosticsInfo> Members { get; }
}
