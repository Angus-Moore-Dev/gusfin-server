using System;

namespace Jellyfin.Api.Models.SyncPlayDtos;

/// <summary>
/// Class DiagnosticsRequestDto. Gusfin extension: periodic playback diagnostics report.
/// </summary>
public class DiagnosticsRequestDto
{
    /// <summary>
    /// Gets or sets when the request has been made by the client.
    /// </summary>
    /// <value>The date of the request.</value>
    public DateTime When { get; set; }

    /// <summary>
    /// Gets or sets the position ticks.
    /// </summary>
    /// <value>The position ticks.</value>
    public long PositionTicks { get; set; }

    /// <summary>
    /// Gets or sets the difference between the client's playback position and the group position, in milliseconds.
    /// </summary>
    /// <value>The playback diff, in milliseconds. Positive means the client is behind the group.</value>
    public double PlaybackDiffMillis { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the client playback is unpaused.
    /// </summary>
    /// <value>The client playback status.</value>
    public bool IsPlaying { get; set; }
}
